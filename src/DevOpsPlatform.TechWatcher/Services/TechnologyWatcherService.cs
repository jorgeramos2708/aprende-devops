namespace DevOpsPlatform.TechWatcher.Services;

using DevOpsPlatform.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Host delgado: cada 6h (o una vez con --once) ejecuta el pipeline
/// compartido de Infrastructure (conectores + evaluacion de impacto).
/// </summary>
public class TechnologyWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TechnologyWatcherService> _logger;

    public TechnologyWatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<TechnologyWatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunOnceAsync(string? technologyFilter, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var watch = scope.ServiceProvider.GetRequiredService<ITechnologyWatchService>();
        var summary = await watch.CheckAllAsync(technologyFilter, ct);
        _logger.LogInformation("Revision watcher: {Checked} fuentes, {Detected} cambios",
            summary.SourcesChecked, summary.ChangesDetected);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(null, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Fallo la revision periodica del watcher");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
