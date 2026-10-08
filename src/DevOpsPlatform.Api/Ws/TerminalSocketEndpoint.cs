namespace DevOpsPlatform.Api.Ws;

using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.LabEngine;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// WebSocket crudo para la terminal de labs. Auth: token de un solo uso emitido por
/// /api/labs/{attemptId}/terminal/token y guardado en Redis (lab:terminal:{token}).
/// Protocolo JSON que espera web/src/components/Terminal.tsx:
///   entrante: { type: "input", data } | { type: "resize", cols, rows }
///   saliente: { type: "output", data } | { type: "error", message }
/// La sesion de terminal sobrevive a desconexiones WS (recargar pestaña maneja).
/// </summary>
public static class TerminalSocketEndpoint
{
    public static async Task HandleAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var token = ctx.Request.Query["token"].ToString();
        var redis = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var attemptRaw = token.Length > 0
            ? await redis.GetDatabase().StringGetAsync($"lab:terminal:{token}")
            : StackExchange.Redis.RedisValue.Null;

        if (!attemptRaw.HasValue || !Guid.TryParse(attemptRaw.ToString(), out var attemptId))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var manager = ctx.RequestServices.GetRequiredService<TerminalStreamManager>();

        // Si no hay sesion activa (p. ej. tras recargar), la recreamos desde el intento
        if (!manager.IsActive(attemptId))
        {
            using var scope = ctx.RequestServices.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ILabOrchestrator>();
            await orchestrator.ConnectTerminalAsync(attemptId, 120, 30, ctx.RequestAborted);
        }

        using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
        var reader = manager.Subscribe(attemptId);
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("TerminalWS");
        logger.LogInformation("WS terminal conectado para intento {AttemptId}", attemptId);

        // Salida de la terminal -> navegador
        var sendTask = Task.Run(async () =>
        {
            try
            {
                // OJO: no usar ctx.RequestAborted aqui; tras el upgrade 101 no es confiable.
                while (await reader.WaitToReadAsync())
                {
                    while (reader.TryRead(out var chunk))
                    {
                        var frame = JsonSerializer.Serialize(new { type = "output", data = chunk });
                        await socket.SendAsync(
                            Encoding.UTF8.GetBytes(frame),
                            WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Send de terminal cerrado para {AttemptId}", attemptId);
            }
        });

        // Entrada del navegador -> terminal
        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                // Un frame WS puede llegar fragmentado: acumular hasta EndOfMessage
                using var frameMs = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ctx.RequestAborted);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    frameMs.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (frameMs.Length == 0) continue;
                logger.LogDebug("WS frame entrante de {AttemptId}: {Bytes} bytes", attemptId, frameMs.Length);

                try
                {
                    using var doc = JsonDocument.Parse(frameMs.ToArray());
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "input" && root.TryGetProperty("data", out var d))
                    {
                        await manager.WriteAsync(attemptId, d.GetString() ?? "");
                    }
                    else if (type == "resize"
                        && root.TryGetProperty("cols", out var c) && root.TryGetProperty("rows", out var r))
                    {
                        await manager.ResizeAsync(attemptId, c.GetInt32(), r.GetInt32());
                    }
                }
                catch (JsonException) { /* frame no-JSON o incompleto: ignorar */ }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }

        manager.Unsubscribe(attemptId, reader);
        try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "cliente cerro", CancellationToken.None); }
        catch { /* ya cerrado */ }
        // NO StopAsync: la sesion vive para reconexiones; la janitor/lab stop la limpia
    }
}
