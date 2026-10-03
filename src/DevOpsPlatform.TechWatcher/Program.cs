using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using DevOpsPlatform.Infrastructure.Watch;
using DevOpsPlatform.TechWatcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WatchPipeline = DevOpsPlatform.Infrastructure.Watch.TechnologyWatchService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient();
builder.Services.AddScoped<ISourceConnector, GitHubConnector>();
builder.Services.AddScoped<ISourceConnector, DockerHubConnector>();
builder.Services.AddScoped<ISourceConnector, RssConnector>();
builder.Services.AddScoped<ISourceConnector, HtmlConnector>();
builder.Services.AddScoped<ISourceConnector, NpmConnector>();
builder.Services.AddScoped<ISourceConnector, PyPIConnector>();
builder.Services.AddScoped<IImpactService, ImpactService>();
builder.Services.AddScoped<ITechnologyWatchService, WatchPipeline>();
builder.Services.AddHostedService<TechnologyWatcherService>();

var app = builder.Build();

// Run once and exit (for scheduled runs) or run as service
if (args.Length > 0 && args[0] == "--once")
{
    var watcher = app.Services.GetRequiredService<TechnologyWatcherService>();
    await watcher.RunOnceAsync(args.Length > 1 ? args[1] : null, CancellationToken.None);
}
else
{
    await app.RunAsync();
}
