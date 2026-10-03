using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.ContainerRuntime;
using DevOpsPlatform.Infrastructure.Data;
using DevOpsPlatform.LabEngine;
using DevOpsPlatform.Workers.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Database=devops_platform;Username=postgres;Password=postgres";
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddScoped<IKnowledgeGraphRepository, DevOpsPlatform.Infrastructure.Repositories.KnowledgeGraphRepository>();
builder.Services.AddScoped<IContainerRuntime, DockerContainerRuntime>();
builder.Services.AddScoped<ILabOrchestrator, LabOrchestrator>();
builder.Services.AddScoped<IBackgroundJobService, BackgroundJobService>();
builder.Services.AddScoped<LabCleanupJob>();
builder.Services.AddScoped<InsightGenerationJob>();
builder.Services.AddScoped<CertificationReadinessJob>();
builder.Services.AddScoped<LabRegressionJob>();

var app = builder.Build();

RecurringJobs.Configure();

await app.RunAsync();
