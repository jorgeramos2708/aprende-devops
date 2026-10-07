namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            var parts = hash.Split('.');
            if (parts.Length != 4 || parts[0] != "v1") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}

public class TokenService : ITokenService
{
    private readonly PlatformDbContext _db;
    private readonly ILogger<TokenService> _logger;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _accessLifetime = TimeSpan.FromHours(1);
    private readonly TimeSpan _refreshLifetime = TimeSpan.FromDays(14);

    public TokenService(PlatformDbContext db, IConfiguration config, ILogger<TokenService> logger)
    {
        _db = db;
        _logger = logger;
        _issuer = config["Jwt:Issuer"] ?? "devops-platform";
        _audience = config["Jwt:Audience"] ?? "devops-platform";
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key (minimo 32 caracteres) en la configuracion.");
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public static SymmetricSecurityKey BuildKey(IConfiguration config)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key (minimo 32 caracteres) en la configuracion.");
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public async Task<AuthSession> CreateSessionAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var expiresAt = DateTimeOffset.UtcNow.Add(_refreshLifetime);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = expiresAt
        });
        await _db.SaveChangesAsync(ct);

        return new AuthSession(
            CreateAccessToken(user),
            refreshToken,
            expiresAt,
            user.ToDto());
    }

    public async Task<AuthSession?> RefreshSessionAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return null;
        var hash = HashToken(refreshToken);
        // Necesitamos el usuario: los RefreshToken no tienen navegacion, lo cargamos aparte
        var record = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (record == null || record.RevokedAt != null || record.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var user = await _db.Users.FindAsync([record.UserId], ct);
        if (user == null || !user.IsActive)
            return null;

        // Rotacion: revoca el anterior y crea uno nuevo
        record.RevokedAt = DateTimeOffset.UtcNow;
        record.ReplacedAt = DateTimeOffset.UtcNow;

        var next = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var expiresAt = DateTimeOffset.UtcNow.Add(_refreshLifetime);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = HashToken(next),
            ExpiresAt = expiresAt
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sesion rotada para usuario {UserId}", user.Id);
        return new AuthSession(CreateAccessToken(user), next, expiresAt, user.ToDto());
    }

    public async Task RevokeSessionAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var record = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (record != null && record.RevokedAt == null)
        {
            record.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private string CreateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_accessLifetime),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

internal static class UserMapping
{
    internal static Core.Models.UserDto ToDto(this ApplicationUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Roles = user.Roles
    };
}
