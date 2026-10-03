namespace DevOpsPlatform.Infrastructure.Watch;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class TechnologyWatchService : ITechnologyWatchService
{
    private readonly PlatformDbContext _db;
    private readonly IEnumerable<ISourceConnector> _connectors;
    private readonly IImpactService _impact;
    private readonly ILogger<TechnologyWatchService> _logger;

    public TechnologyWatchService(
        PlatformDbContext db,
        IEnumerable<ISourceConnector> connectors,
        IImpactService impact,
        ILogger<TechnologyWatchService> logger)
    {
        _db = db;
        _connectors = connectors;
        _impact = impact;
        _logger = logger;
    }

    public async Task<WatchSummary> CheckAllAsync(string? technologyFilter, CancellationToken ct = default)
    {
        var query = _db.TechnologySources.Where(s => s.IsActive);
        if (!string.IsNullOrEmpty(technologyFilter))
            query = query.Where(s => s.Technology == technologyFilter);

        var sources = await query.ToListAsync(ct);
        _logger.LogInformation("Revisando {Count} fuentes oficiales", sources.Count);

        var detected = 0;
        foreach (var source in sources)
        {
            try
            {
                var connector = _connectors.FirstOrDefault(c => c.SourceType == source.SourceType);
                if (connector == null)
                {
                    _logger.LogWarning("Sin conector para {SourceType}", source.SourceType);
                    continue;
                }

                var result = await connector.CheckAsync(source, ct);
                if (result.Error != null)
                {
                    _logger.LogError("Error revisando {Tech} via {Type}: {Error}", source.Technology, source.SourceType, result.Error);
                    continue;
                }

                if (result.HasChanges && result.CurrentVersion != source.LastVersion)
                {
                    var change = new TechnologyChange
                    {
                        Id = Ulid.NewUlid(),
                        Technology = source.Technology,
                        SourceId = source.Id,
                        ChangeType = Classify(source.LastVersion, result.CurrentVersion),
                        PreviousVersion = source.LastVersion,
                        NewVersion = result.CurrentVersion,
                        Summary = $"{source.Technology} cambio de {source.LastVersion ?? "?"} a {result.CurrentVersion}",
                        Details = result.RawData ?? JsonDocument.Parse("{}"),
                        Severity = Rate(source.LastVersion, result.CurrentVersion),
                        Status = "pending"
                    };

                    _db.TechnologyChanges.Add(change);

                    source.LastVersion = result.CurrentVersion;
                    source.LastContentHash = result.ContentHash;
                    source.LastCheckedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);

                    await _impact.EnsureForChangeAsync(change.Id, ct);
                    detected++;
                    _logger.LogInformation("Cambio detectado {Tech}: {Prev} -> {New}", source.Technology, change.PreviousVersion, change.NewVersion);
                }
                else
                {
                    source.LastCheckedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo revisando fuente de {Tech}", source.Technology);
            }
        }

        _logger.LogInformation("Revision completa: {Detected} cambios", detected);
        return new WatchSummary(sources.Count, detected);
    }

    private static string Classify(string? prev, string? current)
    {
        if (string.IsNullOrEmpty(prev)) return "new_version";
        if (string.IsNullOrEmpty(current)) return "deprecation";

        var prevParts = prev.Split('.', '-', '+');
        var currParts = current.Split('.', '-', '+');

        if (currParts.Length > 0 && prevParts.Length > 0
            && int.TryParse(currParts[0], out var currMajor) && int.TryParse(prevParts[0], out var prevMajor)
            && currMajor > prevMajor) return "breaking_change";

        if (currParts.Length > 1 && prevParts.Length > 1
            && int.TryParse(currParts[1], out var currMinor) && int.TryParse(prevParts[1], out var prevMinor)
            && currMinor > prevMinor) return "new_feature";

        return "patch";
    }

    private static string Rate(string? prev, string? current) => Classify(prev, current) switch
    {
        "breaking_change" => "critical",
        "deprecation" => "high",
        "security" => "critical",
        "new_version" => "medium",
        "new_feature" => "medium",
        _ => "low"
    };
}
