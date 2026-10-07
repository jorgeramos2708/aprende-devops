namespace DevOpsPlatform.LabEngine;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public interface ILabService
{
    Task<PagedResult<LabEnvironment>> GetLabsAsync(string? technology = null, LabType? type = null, int page = 1, int pageSize = 20);
    Task<LabEnvironment?> GetLabAsync(Guid id);
    Task<LabEnvironment> CreateLabAsync(CreateLabRequest request, Guid createdBy);
    Task<LabEnvironment> UpdateLabAsync(Guid id, UpdateLabRequest request);
    Task DeleteLabAsync(Guid id);
    Task<IReadOnlyList<LabAttempt>> GetUserAttemptsAsync(Guid userId, Guid? labId = null);
}

public class LabService : ILabService
{
    private readonly PlatformDbContext _db;

    public LabService(PlatformDbContext db) => _db = db;

    public async Task<PagedResult<LabEnvironment>> GetLabsAsync(string? technology = null, LabType? type = null, int page = 1, int pageSize = 20)
    {
        var query = _db.LabEnvironments.Where(l => l.IsActive);

        if (type.HasValue)
        {
            query = query.Where(l => l.LabType == type.Value);
        }

        // El filtro por tecnologia vive dentro del jsonb Metadata: se evalua en memoria
        // (el catalogo de labs es pequeno; evita traduccion SQL no soportada).
        var candidates = await query.OrderBy(l => l.Name).ToListAsync();

        if (!string.IsNullOrEmpty(technology))
        {
            candidates = candidates.Where(l => MetaEquals(l.Metadata, "technology", technology)).ToList();
        }

        var total = candidates.Count;
        var items = candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<LabEnvironment>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static bool MetaEquals(JsonDocument meta, string key, string value)
    {
        try
        {
            return meta.RootElement.ValueKind == JsonValueKind.Object
                && meta.RootElement.TryGetProperty(key, out var v)
                && string.Equals(v.GetString(), value, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public async Task<LabEnvironment?> GetLabAsync(Guid id)
        => await _db.LabEnvironments.FirstOrDefaultAsync(l => l.Id == id);

    public async Task<LabEnvironment> CreateLabAsync(CreateLabRequest request, Guid createdBy)
    {
        var lab = new LabEnvironment
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant().Replace(" ", "-"),
            Description = request.Description,
            LabType = request.LabType,
            BaseImage = request.BaseImage,
            DockerCompose = request.DockerCompose,
            ResourceLimits = JsonDocument.Parse(JsonSerializer.Serialize(request.ResourceLimits)),
            ValidationScript = request.ValidationScript,
            SetupScript = request.SetupScript,
            CleanupScript = request.CleanupScript,
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                technology = request.Technology,
                topic = request.Topic,
                level = request.Level,
                estimatedTimeMinutes = request.EstimatedTimeMinutes,
                createdBy = createdBy.ToString(),
                createdAt = DateTimeOffset.UtcNow
            })),
            IsActive = true
        };

        _db.LabEnvironments.Add(lab);
        await _db.SaveChangesAsync();
        return lab;
    }

    public async Task<LabEnvironment> UpdateLabAsync(Guid id, UpdateLabRequest request)
    {
        var lab = await _db.LabEnvironments.FindAsync(id);
        if (lab == null) throw new InvalidOperationException("Lab not found");

        lab.Name = request.Name ?? lab.Name;
        lab.Description = request.Description ?? lab.Description;
        lab.LabType = request.LabType ?? lab.LabType;
        lab.BaseImage = request.BaseImage ?? lab.BaseImage;
        lab.DockerCompose = request.DockerCompose ?? lab.DockerCompose;
        
        if (request.ResourceLimits != null)
            lab.ResourceLimits = JsonDocument.Parse(JsonSerializer.Serialize(request.ResourceLimits));
        
        lab.ValidationScript = request.ValidationScript ?? lab.ValidationScript;
        lab.SetupScript = request.SetupScript ?? lab.SetupScript;
        lab.CleanupScript = request.CleanupScript ?? lab.CleanupScript;
        
        if (request.Metadata != null)
            lab.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(request.Metadata));

        await _db.SaveChangesAsync();
        return lab;
    }

    public async Task DeleteLabAsync(Guid id)
    {
        var lab = await _db.LabEnvironments.FindAsync(id);
        if (lab == null) throw new InvalidOperationException("Lab not found");

        lab.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<LabAttempt>> GetUserAttemptsAsync(Guid userId, Guid? labId = null)
    {
        var query = _db.LabAttempts
            .Include(a => a.LabEnvironment)
            .Where(a => a.UserId == userId);

        if (labId.HasValue)
            query = query.Where(a => a.LabEnvironmentId == labId.Value);

        return await query.OrderByDescending(a => a.StartedAt).ToListAsync();
    }
}

public record CreateLabRequest
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public LabType LabType { get; init; }
    public string BaseImage { get; init; } = string.Empty;
    public string? DockerCompose { get; init; }
    public LabResourceLimits ResourceLimits { get; init; } = new();
    public string? ValidationScript { get; init; }
    public string? SetupScript { get; init; }
    public string? CleanupScript { get; init; }
    public string Technology { get; init; } = string.Empty;
    public string? Topic { get; init; }
    public string Level { get; init; } = "basic";
    public int EstimatedTimeMinutes { get; init; } = 30;
}

public record UpdateLabRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public LabType? LabType { get; init; }
    public string? BaseImage { get; init; }
    public string? DockerCompose { get; init; }
    public LabResourceLimits? ResourceLimits { get; init; }
    public string? ValidationScript { get; init; }
    public string? SetupScript { get; init; }
    public string? CleanupScript { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record LabResourceLimits
{
    public string Cpus { get; init; } = "0.5";
    public string Memory { get; init; } = "512m";
    public int Pids { get; init; } = 100;
    public int TimeoutSeconds { get; init; } = 1800;
    public bool UseGVisor { get; init; } = true;
}
