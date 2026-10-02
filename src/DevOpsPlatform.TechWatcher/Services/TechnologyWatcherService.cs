namespace DevOpsPlatform.TechWatcher.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class TechnologyWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TechnologyWatcherService> _logger;
    private readonly IEnumerable<ISourceConnector> _connectors;

    public TechnologyWatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<TechnologyWatcherService> logger,
        IEnumerable<ISourceConnector> connectors)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _connectors = connectors;
    }

    public async Task RunOnceAsync(string? technologyFilter, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var sourcesQuery = db.TechnologySources.Where(s => s.IsActive);
        if (!string.IsNullOrEmpty(technologyFilter))
            sourcesQuery = sourcesQuery.Where(s => s.Technology == technologyFilter);

        var sources = await sourcesQuery.ToListAsync(ct);
        _logger.LogInformation("Checking {Count} technology sources", sources.Count);

        foreach (var source in sources)
        {
            try
            {
                var connector = _connectors.FirstOrDefault(c => c.SourceType == source.SourceType);
                if (connector == null)
                {
                    _logger.LogWarning("No connector for source type {SourceType}", source.SourceType);
                    continue;
                }

                var result = await connector.CheckAsync(source, ct);
                if (result.Error != null)
                {
                    _logger.LogError("Error checking {Technology} via {SourceType}: {Error}", source.Technology, source.SourceType, result.Error);
                    continue;
                }

                if (result.HasChanges && result.CurrentVersion != source.LastVersion)
                {
                    var change = new TechnologyChange
                    {
                        Id = Ulid.NewUlid(),
                        Technology = source.Technology,
                        SourceId = source.Id,
                        ChangeType = DetermineChangeType(source.LastVersion, result.CurrentVersion),
                        PreviousVersion = source.LastVersion,
                        NewVersion = result.CurrentVersion,
                        Summary = $"{source.Technology} updated from {source.LastVersion} to {result.CurrentVersion}",
                        Details = result.RawData ?? JsonDocument.Parse("{}"),
                        Severity = DetermineSeverity(source.LastVersion, result.CurrentVersion),
                        Status = "pending"
                    };

                    db.TechnologyChanges.Add(change);

                    // Update source
                    source.LastVersion = result.CurrentVersion;
                    source.LastContentHash = result.ContentHash;
                    source.LastCheckedAt = DateTimeOffset.UtcNow;

                    _logger.LogInformation("Detected change for {Technology}: {Prev} -> {New}", source.Technology, source.LastVersion, result.CurrentVersion);
                }
                else
                {
                    source.LastCheckedAt = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check source for {Technology}", source.Technology);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Technology watcher run completed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(null, stoppingToken);
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken); // Every 6 hours
        }
    }

    private static string DetermineChangeType(string? prev, string? current)
    {
        if (string.IsNullOrEmpty(prev)) return "new_version";
        if (string.IsNullOrEmpty(current)) return "deprecation";
        
        // Simple semantic version comparison
        var prevParts = prev.Split('.', '-', '+');
        var currParts = current.Split('.', '-', '+');
        
        if (currParts.Length > 0 && prevParts.Length > 0)
        {
            if (int.TryParse(currParts[0], out var currMajor) && int.TryParse(prevParts[0], out var prevMajor))
            {
                if (currMajor > prevMajor) return "breaking_change";
            }
            if (currParts.Length > 1 && prevParts.Length > 1)
            {
                if (int.TryParse(currParts[1], out var currMinor) && int.TryParse(prevParts[1], out var prevMinor))
                {
                    if (currMinor > prevMinor) return "new_feature";
                }
            }
        }
        return "patch";
    }

    private static string DetermineSeverity(string? prev, string? current)
    {
        var changeType = DetermineChangeType(prev, current);
        return changeType switch
        {
            "breaking_change" => "critical",
            "deprecation" => "high",
            "security" => "critical",
            "new_version" => "medium",
            "new_feature" => "medium",
            "patch" => "low",
            _ => "low"
        };
    }
}
