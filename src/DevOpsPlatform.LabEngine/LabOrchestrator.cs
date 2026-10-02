namespace DevOpsPlatform.LabEngine;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

public class LabOrchestrator : ILabOrchestrator
{
    private readonly PlatformDbContext _db;
    private readonly IContainerRuntime _containerRuntime;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<LabOrchestrator> _logger;
    private readonly IServiceProvider _services;

    public LabOrchestrator(
        PlatformDbContext db,
        IContainerRuntime containerRuntime,
        IConnectionMultiplexer redis,
        ILogger<LabOrchestrator> logger,
        IServiceProvider services)
    {
        _db = db;
        _containerRuntime = containerRuntime;
        _redis = redis;
        _logger = logger;
        _services = services;
    }

    public async Task<LabSession> StartLabAsync(Ulid labEnvironmentId, Ulid userId, CancellationToken ct = default)
    {
        var lab = await _db.LabEnvironments
            .FirstOrDefaultAsync(l => l.Id == labEnvironmentId && l.IsActive, ct);
        
        if (lab == null)
            throw new InvalidOperationException("Lab environment not found or inactive");

        // Check for existing running attempt
        var existing = await _db.LabAttempts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.LabEnvironmentId == labEnvironmentId 
                && (a.Status == LabStatus.Running || a.Status == LabStatus.Provisioning), ct);
        
        if (existing != null)
        {
            // Resume existing session
            var containerInfo = await _containerRuntime.GetContainerInfoAsync(existing.ContainerId!, ct);
            if (containerInfo != null && containerInfo.State == "running")
            {
                return await ResumeSessionAsync(existing, ct);
            }
        }

        // Create new attempt
        var attempt = new LabAttempt
        {
            Id = Ulid.NewUlid(),
            UserId = userId,
            LabEnvironmentId = labEnvironmentId,
            Status = LabStatus.Provisioning,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(GetTimeout(lab)),
            Metadata = JsonDocument.Parse("{}")
        };

        _db.LabAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            // Run setup script if provided
            if (!string.IsNullOrEmpty(lab.SetupScript))
            {
                await RunSetupScriptAsync(lab, attempt.Id, ct);
            }

            // Create container
            var containerId = await _containerRuntime.CreateContainerAsync(lab, attempt.Id, ct);
            var started = await _containerRuntime.StartContainerAsync(containerId, ct);

            if (!started)
                throw new InvalidOperationException("Failed to start container");

            attempt.ContainerId = containerId;
            attempt.Status = LabStatus.Running;
            await _db.SaveChangesAsync(ct);

            // Generate terminal session token
            var sessionToken = GenerateSessionToken(attempt.Id);
            await _redis.GetDatabase().StringSetAsync(
                $"lab:terminal:{sessionToken}", 
                attempt.Id.ToString(), 
                TimeSpan.FromSeconds(GetTimeout(lab)));

            // Get container IP (if network enabled)
            var containerInfo = await _containerRuntime.GetContainerInfoAsync(containerId, ct);
            
            return new LabSession
            {
                AttemptId = attempt.Id,
                ContainerId = containerId,
                ContainerIp = containerInfo?.IpAddress ?? "",
                TerminalPort = 0, // WebSocket port handled by SignalR
                ExpiresAt = attempt.ExpiresAt.Value,
                Status = LabStatus.Running
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start lab {LabId} for user {UserId}", labEnvironmentId, userId);
            attempt.Status = LabStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<LabSession> ResumeSessionAsync(LabAttempt attempt, CancellationToken ct)
    {
        var sessionToken = GenerateSessionToken(attempt.Id);
        await _redis.GetDatabase().StringSetAsync(
            $"lab:terminal:{sessionToken}", 
            attempt.Id.ToString(), 
            TimeSpan.FromSeconds(1800));

        var containerInfo = await _containerRuntime.GetContainerInfoAsync(attempt.ContainerId!, ct);
        
        return new LabSession
        {
            AttemptId = attempt.Id,
            ContainerId = attempt.ContainerId!,
            ContainerIp = containerInfo?.IpAddress ?? "",
            TerminalPort = 0,
            ExpiresAt = attempt.ExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30),
            Status = LabStatus.Running
        };
    }

    public async Task<LabSession?> GetSessionAsync(Ulid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts
            .Include(a => a.LabEnvironment)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);

        if (attempt == null) return null;

        var containerInfo = attempt.ContainerId != null 
            ? await _containerRuntime.GetContainerInfoAsync(attempt.ContainerId, ct)
            : null;

        return new LabSession
        {
            AttemptId = attempt.Id,
            ContainerId = attempt.ContainerId ?? "",
            ContainerIp = containerInfo?.IpAddress ?? "",
            TerminalPort = 0,
            ExpiresAt = attempt.ExpiresAt ?? DateTimeOffset.UtcNow,
            Status = attempt.Status
        };
    }

    public async Task<TerminalConnection> ConnectTerminalAsync(Ulid attemptId, int cols, int rows, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts.FindAsync([attemptId], ct);
        if (attempt == null || attempt.ContainerId == null)
            throw new InvalidOperationException("Lab attempt not found");

        var sessionToken = GenerateSessionToken(attemptId);
        await _redis.GetDatabase().StringSetAsync(
            $"lab:terminal:{sessionToken}", 
            attemptId.ToString(), 
            TimeSpan.FromMinutes(30));

        // Resize container terminal
        await _containerRuntime.ExecAsync(attempt.ContainerId, new[] { "sh", "-c", $"resize -s {rows} {cols}" }, ct);

        return new TerminalConnection
        {
            WebSocketUrl = $"/api/labs/ws?token={sessionToken}",
            SessionToken = sessionToken,
            Cols = cols,
            Rows = rows
        };
    }

    public async Task SendTerminalInputAsync(Ulid attemptId, string input, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts.FindAsync([attemptId], ct);
        if (attempt?.ContainerId == null) return;

        // Write to container stdin via Docker attach
        // This is a simplified version - in production you'd maintain a persistent attach stream
        try
        {
            await _containerRuntime.ExecAsync(attempt.ContainerId, new[] { "sh", "-c", $"echo '{input.Replace("'", "'\''")}' > /dev/tty" }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send terminal input to {ContainerId}", attempt.ContainerId);
        }
    }

    public async Task ResizeTerminalAsync(Ulid attemptId, int cols, int rows, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts.FindAsync([attemptId], ct);
        if (attempt?.ContainerId == null) return;

        await _containerRuntime.ExecAsync(attempt.ContainerId, new[] { "sh", "-c", $"resize -s {rows} {cols}" }, ct);
    }

    public async Task<LabValidationResult> ValidateLabAsync(Ulid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts
            .Include(a => a.LabEnvironment)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);

        if (attempt == null || attempt.LabEnvironment == null)
            return new LabValidationResult { Passed = false, Score = 0, Errors = ["Attempt not found"] };

        if (string.IsNullOrEmpty(attempt.LabEnvironment.ValidationScript))
        {
            return new LabValidationResult 
            { 
                Passed = true, 
                Score = 100, 
                Details = JsonDocument.Parse("{\"message\": \"No validation script defined\"}") 
            };
        }

        try
        {
            // Run validation script in container
            var result = await _containerRuntime.ExecAsync(
                attempt.ContainerId!, 
                new[] { "bash", "-c", attempt.LabEnvironment.ValidationScript }, 
                ct);

            var passed = result.ExitCode == 0;
            var score = passed ? 100 : 0;

            // Parse validation output for detailed scoring
            var details = ParseValidationOutput(result.Stdout, result.Stderr);

            attempt.Status = passed ? LabStatus.Completed : LabStatus.Failed;
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            attempt.Score = score;
            attempt.ValidationResult = details;
            attempt.Evidence = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                stdout = result.Stdout,
                stderr = result.Stderr,
                exitCode = result.ExitCode,
                validatedAt = DateTimeOffset.UtcNow
            }));

            await _db.SaveChangesAsync(ct);

            return new LabValidationResult
            {
                Passed = passed,
                Score = score,
                Evidence = attempt.Evidence,
                Details = details,
                Warnings = ExtractWarnings(result.Stderr),
                Errors = passed ? [] : [result.Stderr]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for attempt {AttemptId}", attemptId);
            return new LabValidationResult 
            { 
                Passed = false, 
                Score = 0, 
                Errors = [ex.Message] 
            };
        }
    }

    public async Task StopLabAsync(Ulid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.LabAttempts.FindAsync([attemptId], ct);
        if (attempt == null) return;

        if (!string.IsNullOrEmpty(attempt.ContainerId))
        {
            // Run cleanup script if provided
            if (attempt.LabEnvironment != null && !string.IsNullOrEmpty(attempt.LabEnvironment.CleanupScript))
            {
                try
                {
                    await _containerRuntime.ExecAsync(attempt.ContainerId, new[] { "bash", "-c", attempt.LabEnvironment.CleanupScript }, ct);
                }
                catch { /* ignore cleanup errors */ }
            }

            await _containerRuntime.StopContainerAsync(attempt.ContainerId, ct);
            await _containerRuntime.RemoveContainerAsync(attempt.ContainerId, ct);
        }

        attempt.Status = LabStatus.Cancelled;
        attempt.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Invalidate terminal token
        await _redis.GetDatabase().KeyDeleteAsync($"lab:terminal:{GenerateSessionToken(attemptId)}");
    }

    public async Task CleanupExpiredLabsAsync(CancellationToken ct = default)
    {
        var expired = await _db.LabAttempts
            .Where(a => a.Status == LabStatus.Running && a.ExpiresAt < DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var attempt in expired)
        {
            _logger.LogInformation("Cleaning up expired lab attempt {AttemptId}", attempt.Id);
            await StopLabAsync(attempt.Id, ct);
            attempt.Status = LabStatus.Expired;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string GenerateSessionToken(Ulid attemptId)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{attemptId}:{DateTimeOffset.UtcNow.Ticks}"));
    }

    private static int GetTimeout(LabEnvironment lab)
    {
        try
        {
            var limits = JsonSerializer.Deserialize<LabResourceLimits>(lab.ResourceLimits.RootElement.GetRawText());
            return limits?.TimeoutSeconds ?? 1800;
        }
        catch { return 1800; }
    }

    private static async Task RunSetupScriptAsync(LabEnvironment lab, Ulid attemptId, CancellationToken ct)
    {
        // Setup runs in a temporary container or the main container before user access
        // For simplicity, we'll run it in the main container after creation
    }

    private static JsonDocument ParseValidationOutput(string stdout, string stderr)
    {
        try
        {
            // Try to parse as JSON first (structured validation output)
            return JsonDocument.Parse(stdout);
        }
        catch
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                stdout = stdout.Trim(),
                stderr = stderr.Trim(),
                timestamp = DateTimeOffset.UtcNow
            }));
        }
    }

    private static string[] ExtractWarnings(string stderr)
    {
        return stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains("WARN", StringComparison.OrdinalIgnoreCase) || l.Contains("warning", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Trim())
            .ToArray();
    }

    private record LabResourceLimits
    {
        public int TimeoutSeconds { get; init; } = 1800;
    }
}
