using DevOpsPlatform.TechWatcher.Connectors;
using DevOpsPlatform.TechWatcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevOpsPlatform.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IGitHubConnector, GitHubConnector>();
builder.Services.AddSingleton<IDockerHubConnector, DockerHubConnector>();
builder.Services.AddSingleton<IRssConnector, RssConnector>();
builder.Services.AddSingleton<IHtmlConnector, HtmlConnector>();
builder.Services.AddSingleton<INpmConnector, NpmConnector>();
builder.Services.AddSingleton<IPyPIConnector, PyPIConnector>();
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
