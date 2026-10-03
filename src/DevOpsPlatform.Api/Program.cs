using DevOpsPlatform.Api.Converters;
using DevOpsPlatform.Api.Hubs;
using DevOpsPlatform.Api.Middleware;
using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using DevOpsPlatform.Infrastructure.Services;
using DevOpsPlatform.Infrastructure.Watch;
using DevOpsPlatform.LabEngine;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

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

// JSON: Ulid se serializa como string
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new UlidJsonConverter());
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Database
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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

// Auth JWT (tokens propios emitidos por ITokenService)
var jwtKey = TokenService.BuildKey(builder.Configuration);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "devops-platform";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "devops-platform";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = jwtKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("admin"))
    .AddPolicy("InstructorOrAdmin", policy => policy.RequireRole("instructor", "admin"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins:0"] ?? "https://aprende-devops.edrs.xyz",
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

// HTTP para conectores del watcher
builder.Services.AddHttpClient();

// Redis: multiplexer compartido por hubs y orquestador de labs
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// Custom services
builder.Services.AddScoped<IKnowledgeGraphRepository, DevOpsPlatform.Infrastructure.Repositories.KnowledgeGraphRepository>();
builder.Services.AddScoped<IContainerRuntime, DevOpsPlatform.Infrastructure.ContainerRuntime.DockerContainerRuntime>();
builder.Services.AddScoped<ILabOrchestrator, LabOrchestrator>();
builder.Services.AddScoped<ILabService, LabService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<ICertificationService, CertificationService>();
builder.Services.AddScoped<IInsightService, InsightService>();
builder.Services.AddScoped<IImpactService, ImpactService>();
builder.Services.AddScoped<IUpdateService, UpdateService>();
builder.Services.AddScoped<IRegressionService, RegressionService>();
builder.Services.AddScoped<ITechnologyWatchService, TechnologyWatchService>();
builder.Services.AddScoped<ISourceConnector, GitHubConnector>();
builder.Services.AddScoped<ISourceConnector, DockerHubConnector>();
builder.Services.AddScoped<ISourceConnector, RssConnector>();
builder.Services.AddScoped<ISourceConnector, HtmlConnector>();
builder.Services.AddScoped<ISourceConnector, NpmConnector>();
builder.Services.AddScoped<ISourceConnector, PyPIConnector>();
builder.Services.AddHostedService<TerminalStreamManager>();
builder.Services.AddSingleton<MarkdownRenderingService>();
builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddSingleton<YamlContentService>();

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

static Ulid UserIdOf(HttpContext ctx)
{
    // JwtBearer mapea "sub" a NameIdentifier por defecto: aceptar ambos
    var sub = ctx.User.FindFirst("sub")?.Value
        ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("Sin claim sub.");
    return Ulid.Parse(sub);
}

static string Meta(KnowledgeNode node, string key)
{
    try
    {
        return node.Metadata.RootElement.ValueKind == JsonValueKind.Object
            && node.Metadata.RootElement.TryGetProperty(key, out var v)
            ? v.GetString() ?? ""
            : "";
    }
    catch { return ""; }
}

// API Routes
var api = app.MapGroup("/api").RequireAuthorization();

// Auth (publico)
var auth = app.MapGroup("/api/auth").AllowAnonymous();
auth.MapPost("/register", async (RegisterRequest req, PlatformDbContext db, IPasswordHasher hasher, ITokenService tokens, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
        return Results.BadRequest(new { error = "Correo invalido." });
    if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
        return Results.BadRequest(new { error = "La contrasena debe tener al menos 8 caracteres." });

    var email = req.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(u => u.Email == email, ct))
        return Results.Conflict(new { error = "El correo ya esta registrado." });

    var user = new ApplicationUser
    {
        Id = Ulid.NewUlid(),
        Email = email,
        PasswordHash = hasher.Hash(req.Password),
        DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email : req.DisplayName.Trim()
    };
    db.Users.Add(user);
    await db.SaveChangesAsync(ct);

    var session = await tokens.CreateSessionAsync(user, ct);
    return Results.Ok(new AuthResponse
    {
        AccessToken = session.AccessToken,
        RefreshToken = session.RefreshToken,
        User = session.User
    });
});

auth.MapPost("/login", async (LoginRequest req, PlatformDbContext db, IPasswordHasher hasher, ITokenService tokens, CancellationToken ct) =>
{
    var email = req.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    if (user == null || !user.IsActive || !hasher.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var updated = user with { LastLoginAt = DateTimeOffset.UtcNow };
    db.Entry(user).CurrentValues.SetValues(updated);
    await db.SaveChangesAsync(ct);

    var session = await tokens.CreateSessionAsync(user, ct);
    return Results.Ok(new AuthResponse
    {
        AccessToken = session.AccessToken,
        RefreshToken = session.RefreshToken,
        User = session.User
    });
});

auth.MapPost("/refresh", async (RefreshRequest req, ITokenService tokens, CancellationToken ct) =>
{
    var session = await tokens.RefreshSessionAsync(req.RefreshToken, ct);
    if (session == null) return Results.Unauthorized();
    return Results.Ok(new AuthResponse
    {
        AccessToken = session.AccessToken,
        RefreshToken = session.RefreshToken,
        User = session.User
    });
});

auth.MapPost("/logout", async (RefreshRequest req, ITokenService tokens, CancellationToken ct) =>
{
    await tokens.RevokeSessionAsync(req.RefreshToken, ct);
    return Results.Ok();
});

api.MapGet("/auth/me", async (HttpContext ctx, PlatformDbContext db, CancellationToken ct) =>
{
    var user = await db.Users.FindAsync([UserIdOf(ctx)], ct);
    return user == null
        ? Results.NotFound()
        : Results.Ok(new UserDto { Id = user.Id, Email = user.Email, DisplayName = user.DisplayName, Roles = user.Roles });
});

// Learning Engine
var learning = api.MapGroup("/learning");
learning.MapGet("/technologies", async (IKnowledgeGraphRepository repo, CancellationToken ct) =>
{
    var techs = await repo.GetByTypeAsync(NodeType.Technology, ct);
    return Results.Ok(techs.Select(t => new { slug = t.Slug, name = t.Title, version = t.Version }));
});

learning.MapGet("/technologies/{slug}", async (string slug, IKnowledgeGraphRepository repo, CancellationToken ct) =>
{
    var tech = await repo.GetBySlugAsync("Technology", slug, null, ct);
    if (tech == null) return Results.NotFound();
    var children = await repo.GetChildrenAsync(tech.Id, ct);
    return Results.Ok(new
    {
        name = tech.Title,
        description = Meta(tech, "description"),
        levels = new[] { "basic", "intermediate", "advanced" },
        nodes = children.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            level = Meta(n, "level"),
            type = n.Type.ToString()
        })
    });
});

learning.MapGet("/nodes/{id}", async (Ulid id, IKnowledgeGraphRepository repo, PlatformDbContext db, CancellationToken ct) =>
{
    var node = await repo.GetByIdAsync(id, ct);
    if (node == null) return Results.NotFound();

    string? markdown = null;
    try
    {
        if (node.Content?.RootElement.ValueKind == JsonValueKind.Object
            && node.Content.RootElement.TryGetProperty("markdown", out var m))
            markdown = m.GetString();
    }
    catch { markdown = null; }

    var tech = Meta(node, "technology");
    Ulid? labId = null;
    if (!string.IsNullOrEmpty(tech))
    {
        var labs = await db.LabEnvironments.Where(l => l.IsActive).ToListAsync(ct);
        labId = labs.FirstOrDefault(l => LabMetaEquals(l.Metadata, tech))?.Id;
    }

    var children = await repo.GetChildrenAsync(node.Id, ct);
    return Results.Ok(new
    {
        title = node.Title,
        content = markdown ?? Meta(node, "description"),
        labId,
        children = children.Select(c => new { id = c.Id, title = c.Title })
    });
});

learning.MapGet("/nodes/{id}/children", async (Ulid id, IKnowledgeGraphRepository repo, CancellationToken ct) =>
{
    var children = await repo.GetChildrenAsync(id, ct);
    return Results.Ok(children.Select(c => new { id = c.Id, title = c.Title, type = c.Type.ToString() }));
});

learning.MapGet("/roadmap", async (IKnowledgeGraphRepository repo, CancellationToken ct) =>
{
    var techs = await repo.GetByTypeAsync(NodeType.Technology, ct);
    return Results.Ok(new
    {
        technologies = techs.Select(t => new { slug = t.Slug, name = t.Title, version = t.Version })
    });
});

learning.MapPost("/progress", async (ProgressUpdateRequest req, HttpContext ctx, IProgressService progress, CancellationToken ct) =>
    Results.Ok(await progress.UpdateProgressAsync(UserIdOf(ctx), req, ct)));

learning.MapGet("/progress", async (HttpContext ctx, IProgressService progress, CancellationToken ct) =>
    Results.Ok(await progress.GetProgressAsync(UserIdOf(ctx), ct)));

learning.MapGet("/skills", async (HttpContext ctx, IProgressService progress, CancellationToken ct) =>
    Results.Ok(await progress.GetSkillsAsync(UserIdOf(ctx), ct)));

// Lab Engine
var labs = api.MapGroup("/labs");
labs.MapGet("/", async (string? technology, string? type, ILabService labService, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
{
    LabType? labType = null;
    if (!string.IsNullOrEmpty(type) && Enum.TryParse<LabType>(type, true, out var parsed))
        labType = parsed;
    return Results.Ok(await labService.GetLabsAsync(technology, labType, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize));
});

labs.MapGet("/{id}", async (Ulid id, ILabService labService, CancellationToken ct) =>
{
    var lab = await labService.GetLabAsync(id);
    return lab == null ? Results.NotFound() : Results.Ok(lab);
});

labs.MapPost("/start", async (LabStartRequest req, HttpContext ctx, ILabOrchestrator orchestrator, CancellationToken ct) =>
{
    var userId = UserIdOf(ctx);
    var session = await orchestrator.StartLabAsync(req.LabEnvironmentId, userId, ct);
    var terminal = await orchestrator.ConnectTerminalAsync(session.AttemptId, 120, 30, ct);
    return Results.Ok(new LabStartResponse
    {
        AttemptId = session.AttemptId,
        ContainerId = session.ContainerId,
        WebSocketUrl = terminal.WebSocketUrl,
        SessionToken = terminal.SessionToken,
        ExpiresAt = session.ExpiresAt,
        Cols = terminal.Cols,
        Rows = terminal.Rows
    });
});

labs.MapPost("/{attemptId}/stop", async (Ulid attemptId, ILabOrchestrator orchestrator, CancellationToken ct) =>
{
    await orchestrator.StopLabAsync(attemptId, ct);
    return Results.Ok();
});

labs.MapPost("/{attemptId}/validate", async (Ulid attemptId, ILabOrchestrator orchestrator, CancellationToken ct) =>
    Results.Ok(await orchestrator.ValidateLabAsync(attemptId, ct)));

labs.MapGet("/{attemptId}/terminal/token", async (Ulid attemptId, ILabOrchestrator orchestrator, CancellationToken ct) =>
    Results.Ok(await orchestrator.ConnectTerminalAsync(attemptId, 120, 30, ct)));

// Assessment Engine
var assessment = api.MapGroup("/assessment");
assessment.MapGet("/exams", async (IExamService exams, CancellationToken ct) =>
    Results.Ok(await exams.GetExamsAsync(ct)));

assessment.MapGet("/exams/{id}", async (Ulid id, IExamService exams, CancellationToken ct) =>
{
    var exam = await exams.GetExamAsync(id, ct);
    return exam == null ? Results.NotFound() : Results.Ok(exam);
});

assessment.MapPost("/exams/start", async (ExamStartRequest req, HttpContext ctx, IExamService exams, CancellationToken ct) =>
    Results.Ok(await exams.StartExamAsync(UserIdOf(ctx), req.ExamId, ct)));

assessment.MapPost("/exams/submit", async (ExamSubmitRequest req, HttpContext ctx, IExamService exams, CancellationToken ct) =>
    Results.Ok(await exams.SubmitExamAsync(UserIdOf(ctx), req, ct)));

assessment.MapGet("/exams/{attemptId}/result", async (Ulid attemptId, HttpContext ctx, IExamService exams, CancellationToken ct) =>
{
    var result = await exams.GetResultAsync(attemptId, UserIdOf(ctx), ct);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

// Certification Engine
var cert = api.MapGroup("/certifications");
cert.MapGet("/", async (ICertificationService certs, CancellationToken ct) =>
    Results.Ok(await certs.GetCertificationsAsync(ct)));

cert.MapGet("/{id}/readiness", async (Ulid id, HttpContext ctx, ICertificationService certs, CancellationToken ct) =>
{
    var readiness = await certs.GetReadinessAsync(UserIdOf(ctx), id, ct);
    return readiness == null ? Results.NotFound() : Results.Ok(readiness);
});

// Insights
var insights = api.MapGroup("/insights");
insights.MapGet("/", async (HttpContext ctx, IInsightService service, CancellationToken ct) =>
    Results.Ok(await service.GetInsightsAsync(UserIdOf(ctx), ct)));

insights.MapPost("/{id}/read", async (Ulid id, HttpContext ctx, IInsightService service, CancellationToken ct) =>
{
    await service.MarkReadAsync(UserIdOf(ctx), id, ct);
    return Results.Ok();
});

insights.MapPost("/{id}/dismiss", async (Ulid id, HttpContext ctx, IInsightService service, CancellationToken ct) =>
{
    await service.DismissAsync(UserIdOf(ctx), id, ct);
    return Results.Ok();
});

// Admin
var admin = api.MapGroup("/admin").RequireAuthorization("AdminOnly");
admin.MapGet("/tech-changes", async (PlatformDbContext db, CancellationToken ct) =>
    Results.Ok(await db.TechnologyChanges.OrderByDescending(c => c.DetectedAt).Take(100).ToListAsync(ct)));

admin.MapPost("/tech-changes/check", async (string? technology, ITechnologyWatchService watcher, CancellationToken ct) =>
    Results.Ok(await watcher.CheckAllAsync(technology, ct)));

admin.MapGet("/impact-assessments", async (IImpactService impact, CancellationToken ct) =>
    Results.Ok(await impact.ListAsync(ct)));

admin.MapPost("/update-proposals", async (CreateUpdateProposalRequest req, HttpContext ctx, IUpdateService updates, CancellationToken ct) =>
    Results.Ok(await updates.CreateAsync(req.ChangeId, req, UserIdOf(ctx), ct)));

admin.MapPost("/update-proposals/{id}/approve", async (Ulid id, HttpContext ctx, IUpdateService updates, CancellationToken ct) =>
    Results.Ok(await updates.ApproveAsync(id, UserIdOf(ctx), ct)));

admin.MapPost("/lab-regression/run", async (RunRegressionRequest req, IRegressionService regression, CancellationToken ct) =>
    Results.Ok(await regression.RunAsync(req.SuiteId?.ToString(), req.TechnologyVersion, ct)));

admin.MapGet("/lab-regression/runs", async (IRegressionService regression, CancellationToken ct) =>
    Results.Ok(await regression.RecentRunsAsync(20, ct)));

// Base de datos: crear esquema si no existe y sembrar contenido inicial
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    // MVP sin migraciones EF (no hay tooling en este entorno): EnsureCreated crea el esquema.
    // Cuando el proyecto tenga migraciones, cambiar a MigrateAsync.
    await db.Database.EnsureCreatedAsync();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(db, hasher, log);
}

app.Run();

static bool LabMetaEquals(JsonDocument meta, string technology)
{
    try
    {
        return meta.RootElement.ValueKind == JsonValueKind.Object
            && meta.RootElement.TryGetProperty("technology", out var v)
            && string.Equals(v.GetString(), technology, StringComparison.OrdinalIgnoreCase);
    }
    catch { return false; }
}
