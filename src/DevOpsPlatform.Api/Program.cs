using DevOpsPlatform.Api.Hubs;
using DevOpsPlatform.Api.Middleware;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using DevOpsPlatform.Infrastructure.Services;
using DevOpsPlatform.LabEngine;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DevOps Learning Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

// Database
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.UseNetTopologySuite()));

// Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "devops-platform:";
});

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "", options =>
    {
        options.Configuration.AbortOnConnectFail = false;
    });

// OpenIddict
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<PlatformDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetIntrospectionEndpointUris("/connect/introspect");
        options.SetUserinfoEndpointUris("/connect/userinfo");
        options.SetRevocationEndpointUris("/connect/revocation");
        
        options.AllowPasswordFlow();
        options.AllowRefreshTokenFlow();
        options.AllowAuthorizationCodeFlow();
        
        options.RegisterScopes("api", "openid", "profile", "email", "roles");
        
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();
        
        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough()
            .EnableStatusCodePagesIntegration();
    })
    .AddValidation(options =>
    {
        options.AddAudiences("api");
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("admin"))
    .AddPolicy("InstructorOrAdmin", policy => policy.RequireRole("instructor", "admin"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins:0"] ?? "https://learn.edrs.xyz",
                builder.Configuration["AllowedOrigins:1"] ?? "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// Health
builder.Services.AddHealthChecks();

// Redis: multiplexer compartido por hubs y orquestador de labs
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// Custom services
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.IKnowledgeGraphRepository, DevOpsPlatform.Infrastructure.Repositories.KnowledgeGraphRepository>();
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.IContainerRuntime, DevOpsPlatform.Infrastructure.ContainerRuntime.DockerContainerRuntime>();
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.ILabOrchestrator, DevOpsPlatform.LabEngine.LabOrchestrator>();
builder.Services.AddScoped<DevOpsPlatform.LabEngine.ILabService, DevOpsPlatform.LabEngine.LabService>();
builder.Services.AddHostedService<DevOpsPlatform.LabEngine.TerminalStreamManager>();
builder.Services.AddSingleton<DevOpsPlatform.Infrastructure.Services.MarkdownRenderingService>();
builder.Services.AddSingleton<DevOpsPlatform.Infrastructure.Services.MinioStorageService>();
builder.Services.AddSingleton<DevOpsPlatform.Infrastructure.Services.YamlContentService>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<TerminalHub>("/api/labs/ws");
app.MapHub<NotificationHub>("/api/notifications/hub");

app.MapHealthChecks("/health");

// API Routes
var api = app.MapGroup("/api").RequireAuthorization();

// TODO(auth): reintroducir cuando existan Identity + ITokenService.
// Los endpoints /api/auth/register, /api/auth/login y /api/auth/refresh
// requieren UserManager<IdentityUser>, SignInManager<IdentityUser> e ITokenService,
// aun no implementados (ver DTOs RegisterRequest/LoginRequest/RefreshRequest al final).

// Learning Engine (solo lectura: lo que IKnowledgeGraphRepository soporta hoy)
var learning = api.MapGroup("/learning");
learning.MapGet("/technologies", async (IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/technologies/{slug}", async (string slug, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/nodes/{id}", async (Ulid id, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/nodes/{id}/children", async (Ulid id, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/roadmap", async (IKnowledgeGraphRepository repo) => { /* ... */ });
// TODO(progress): POST /progress y GET /progress requieren IProgressService (no existe aun).

// Lab Engine
var labs = api.MapGroup("/labs");
labs.MapGet("/", async (ILabService labService) => { /* ... */ });
labs.MapGet("/{id}", async (Ulid id, ILabService labService) => { /* ... */ });
labs.MapPost("/start", async (LabStartRequest req, HttpContext ctx, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapPost("/{attemptId}/stop", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapPost("/{attemptId}/validate", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapGet("/{attemptId}/terminal/token", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });

// TODO(assessment): /api/assessment requiere IExamService + DTOs ExamStartRequest/ExamSubmitRequest (no existen aun).
// TODO(certifications): /api/certifications requiere ICertificationService (no existe aun).
// TODO(insights): /api/insights requiere IInsightService (no existe aun).

// Admin
// TODO(admin): /api/admin requiere ITechWatcherService, IImpactService, IUpdateService,
// CreateUpdateProposalRequest, RunRegressionRequest e IRegressionService (no existen aun).

// Migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

// DTOs for auth
record RegisterRequest(string Email, string Password, string DisplayName);
record LoginRequest(string Email, string Password);
record RefreshRequest(string RefreshToken);
