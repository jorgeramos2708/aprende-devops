namespace DevOpsPlatform.Api.Middleware;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.Extensions.Caching.Distributed;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, PlatformDbContext db)
    {
        var userId = context.User?.FindFirst("sub")?.Value;
        var originalBody = context.Response.Body;
        
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        if (context.Request.Method != "GET" && context.Request.Method != "HEAD")
        {
            var audit = new AuditLog
            {
                Id = Guid.CreateVersion7(),
                UserId = Guid.TryParse(userId, out var uid) ? uid : null,
                Action = $"{context.Request.Method} {context.Request.Path}",
                EntityType = ExtractEntityType(context.Request.Path),
                EntityId = ExtractEntityId(context.Request.Path),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync();
        }

        responseBody.Position = 0;
        await responseBody.CopyToAsync(originalBody);
    }

    private static string? ExtractEntityType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments?.Length >= 3 && segments[0] == "api")
            return segments[1].TrimEnd('s');
        return null;
    }

    private static Guid? ExtractEntityId(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments?.Length >= 4 && segments[0] == "api" && Guid.TryParse(segments[3], out var id))
            return id;
        return null;
    }
}

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, IDistributedCache cache, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.Value ?? "";
        var key = $"ratelimit:{ip}:{path}";

        var countStr = await _cache.GetAsync(key);
        var count = countStr != null ? int.Parse(System.Text.Encoding.UTF8.GetString(countStr)) : 0;

        var limit = path.Contains("/auth/") ? 10 : 100;
        var window = TimeSpan.FromMinutes(1);

        if (count >= limit)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _cache.SetAsync(key, System.Text.Encoding.UTF8.GetBytes((count + 1).ToString()), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window
        });

        await _next(context);
    }
}
