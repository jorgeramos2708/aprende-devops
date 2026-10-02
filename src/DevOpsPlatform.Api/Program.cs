using DevOpsPlatform.Api.Hubs;
using DevOpsPlatform.Api.Middleware;
using DevOpsPlatform.Infrastructure.Data;
using DevOpsPlatform.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Serilog;

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

// Custom services
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.IKnowledgeGraphRepository, DevOpsPlatform.Infrastructure.Repositories.KnowledgeGraphRepository>();
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.IContainerRuntime, DevOpsPlatform.Infrastructure.ContainerRuntime.DockerContainerRuntime>();
builder.Services.AddScoped<DevOpsPlatform.Core.Interfaces.ILabOrchestrator, DevOpsPlatform.LabEngine.LabOrchestrator>();
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

// Auth endpoints (public)
var auth = app.MapGroup("/api/auth").AllowAnonymous();
auth.MapPost("/register", async (HttpContext ctx, RegisterRequest req, UserManager<IdentityUser> userManager) => { /* ... */ });
auth.MapPost("/login", async (HttpContext ctx, LoginRequest req, SignInManager<IdentityUser> signInManager, ITokenService tokenService) => { /* ... */ });
auth.MapPost("/refresh", async (HttpContext ctx, RefreshRequest req, ITokenService tokenService) => { /* ... */ });

// Learning Engine
var learning = api.MapGroup("/learning");
learning.MapGet("/technologies", async (IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/technologies/{slug}", async (string slug, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/nodes/{id}", async (Ulid id, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/nodes/{id}/children", async (Ulid id, IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapGet("/roadmap", async (IKnowledgeGraphRepository repo) => { /* ... */ });
learning.MapPost("/progress", async (ProgressUpdateRequest req, HttpContext ctx, IProgressService progress) => { /* ... */ });
learning.MapGet("/progress", async (HttpContext ctx, IProgressService progress) => { /* ... */ });

// Lab Engine
var labs = api.MapGroup("/labs");
labs.MapGet("/", async (ILabService labService) => { /* ... */ });
labs.MapGet("/{id}", async (Ulid id, ILabService labService) => { /* ... */ });
labs.MapPost("/start", async (LabStartRequest req, HttpContext ctx, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapPost("/{attemptId}/stop", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapPost("/{attemptId}/validate", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });
labs.MapGet("/{attemptId}/terminal/token", async (Ulid attemptId, ILabOrchestrator orchestrator) => { /* ... */ });

// Assessment Engine
var assessment = api.MapGroup("/assessment");
assessment.MapGet("/exams", async (IExamService examService) => { /* ... */ });
assessment.MapGet("/exams/{id}", async (Ulid id, IExamService examService) => { /* ... */ });
assessment.MapPost("/exams/start", async (ExamStartRequest req, HttpContext ctx, IExamService examService) => { /* ... */ });
assessment.MapPost("/exams/submit", async (ExamSubmitRequest req, HttpContext ctx, IExamService examService) => { /* ... */ });
assessment.MapGet("/exams/{attemptId}/result", async (Ulid attemptId, IExamService examService) => { /* ... */ });

// Certification Engine
var cert = api.MapGroup("/certifications");
cert.MapGet("/", async (ICertificationService certService) => { /* ... */ });
cert.MapGet("/{id}/readiness", async (Ulid id, HttpContext ctx, ICertificationService certService) => { /* ... */ });

// Insights
var insights = api.MapGroup("/insights");
insights.MapGet("/", async (HttpContext ctx, IInsightService insightService) => { /* ... */ });
insights.MapPost("/{id}/read", async (Ulid id, IInsightService insightService) => { /* ... */ });
insights.MapPost("/{id}/dismiss", async (Ulid id, IInsightService insightService) => { /* ... */ });

// Admin
var admin = api.MapGroup("/admin").RequireAuthorization("AdminOnly");
admin.MapGet("/tech-changes", async (ITechWatcherService watcher) => { /* ... */ });
admin.MapPost("/tech-changes/check", async (ITechWatcherService watcher) => { /* ... */ });
admin.MapGet("/impact-assessments", async (IImpactService impact) => { /* ... */ });
admin.MapPost("/update-proposals", async (CreateUpdateProposalRequest req, IUpdateService update) => { /* ... */ });
admin.MapPost("/update-proposals/{id}/approve", async (Ulid id, IUpdateService update) => { /* ... */ });
admin.MapPost("/lab-regression/run", async (RunRegressionRequest req, IRegressionService regression) => { /* ... */ });

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
