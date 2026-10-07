namespace DevOpsPlatform.Api.Hubs;

using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

public class TerminalHub : Hub
{
    private readonly ILabOrchestrator _orchestrator;
    private readonly ILogger<TerminalHub> _logger;
    private readonly IConnectionMultiplexer _redis;

    public TerminalHub(ILabOrchestrator orchestrator, ILogger<TerminalHub> logger, IConnectionMultiplexer redis)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _redis = redis;
    }

    public override async Task OnConnectedAsync()
    {
        var sessionToken = Context.GetHttpContext()?.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(sessionToken))
        {
            Context.Abort();
            return;
        }

        // Validate token and get attempt ID
        var attemptIdStr = await _redis.GetDatabase().StringGetAsync($"lab:terminal:{sessionToken}");
        if (!attemptIdStr.HasValue || !Guid.TryParse(attemptIdStr, out var attemptId))
        {
            Context.Abort();
            return;
        }

        // Store attempt ID in connection context
        Context.Items["AttemptId"] = attemptId;
        
        // Join group for this lab attempt
        await Groups.AddToGroupAsync(Context.ConnectionId, $"lab:{attemptId}");
        
        _logger.LogInformation("Terminal connected: {ConnectionId} for attempt {AttemptId}", Context.ConnectionId, attemptId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("AttemptId", out var attemptIdObj) && attemptIdObj is Guid attemptId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"lab:{attemptId}");
            _logger.LogInformation("Terminal disconnected: {ConnectionId} for attempt {AttemptId}", Context.ConnectionId, attemptId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendInput(TerminalInput input)
    {
        if (!Context.Items.TryGetValue("AttemptId", out var attemptIdObj) || attemptIdObj is not Guid attemptId)
            return;

        var session = await _orchestrator.GetSessionAsync(attemptId);
        if (session == null)
            return;

        // Forward input to container via Docker exec/attach
        // This is handled by the LabOrchestrator which manages the terminal stream
        await _orchestrator.SendTerminalInputAsync(attemptId, input.Data);
    }

    public async Task Resize(TerminalResizeRequest resize)
    {
        if (!Context.Items.TryGetValue("AttemptId", out var attemptIdObj) || attemptIdObj is not Guid attemptId)
            return;

        await _orchestrator.ResizeTerminalAsync(attemptId, resize.Cols, resize.Rows);
    }
}

public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{uid}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
