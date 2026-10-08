namespace DevOpsPlatform.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

// NOTA: la terminal de labs usa WebSocket crudo en Api/Ws/TerminalSocketEndpoint.cs
// (el frontend Terminal.tsx habla frames JSON propios, no el protocolo SignalR).

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
