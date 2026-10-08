namespace DevOpsPlatform.LabEngine;

using DevOpsPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

/// <summary>
/// Administra sesiones de terminal persistentes (docker exec con TTY) por intento de lab.
/// Singleton: una sesion por intento; N suscriptores (pestanas del navegador) por intento
/// reciben la misma salida via canales de difusion.
/// </summary>
public class TerminalStreamManager : IAsyncDisposable
{
    private readonly IContainerRuntime _runtime;
    private readonly ILogger<TerminalStreamManager> _logger;
    private readonly ConcurrentDictionary<Guid, TerminalSession> _sessions = new();

    public TerminalStreamManager(IContainerRuntime runtime, ILogger<TerminalStreamManager> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    public bool IsActive(Guid attemptId) => _sessions.ContainsKey(attemptId);

    public async Task EnsureSessionAsync(Guid attemptId, string containerId, int cols, int rows, CancellationToken ct = default)
    {
        if (_sessions.ContainsKey(attemptId)) return;

        var exec = await _runtime.AttachExecShellAsync(containerId, cols, rows, ct);
        var session = new TerminalSession
        {
            AttemptId = attemptId,
            ContainerId = containerId,
            ExecId = exec.ExecId,
            Stream = exec.Stream,
            Cols = cols,
            Rows = rows,
            Cancel = new CancellationTokenSource()
        };

        if (!_sessions.TryAdd(attemptId, session))
        {
            // Carrera: otra pestaña la creo primero
            await exec.Stream.DisposeAsync();
            return;
        }

        session.PumpTask = Task.Run(() => PumpOutputAsync(session));
        _logger.LogInformation("Sesion de terminal iniciada para intento {AttemptId} (contenedor {ContainerId})", attemptId, containerId);
    }

    public ChannelReader<string> Subscribe(Guid attemptId)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            lock (session.Subscribers) { session.Subscribers.Add(channel); }
            // La sesion ya existia (reconexion): avivar el prompt para que el usuario no vea negro
            _ = WriteAsync(attemptId, "\r");
        }
        return channel.Reader;
    }

    public void Unsubscribe(Guid attemptId, ChannelReader<string> reader)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            lock (session.Subscribers)
            {
                session.Subscribers.RemoveAll(c => c.Reader == reader);
            }
        }
    }

    public async Task WriteAsync(Guid attemptId, string data)
    {
        if (!_sessions.TryGetValue(attemptId, out var session)) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        await session.WriteLock.WaitAsync();
        try
        {
            await session.Stream.WriteAsync(bytes, 0, bytes.Length);
            await session.Stream.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir en la terminal de {AttemptId}", attemptId);
        }
        finally { session.WriteLock.Release(); }
    }

    public async Task ResizeAsync(Guid attemptId, int cols, int rows)
    {
        if (!_sessions.TryGetValue(attemptId, out var session)) return;
        session.Cols = cols;
        session.Rows = rows;
        await _runtime.ResizeExecAsync(session.ExecId, cols, rows);
    }

    public async Task StopAsync(Guid attemptId)
    {
        if (!_sessions.Remove(attemptId, out var session)) return;
        session.Cancel.Cancel();
        lock (session.Subscribers)
        {
            foreach (var ch in session.Subscribers) ch.Writer.TryComplete();
            session.Subscribers.Clear();
        }
        try { await session.PumpTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* timeout ok */ }
        try { await session.Stream.DisposeAsync(); } catch { }
    }

    private async Task PumpOutputAsync(TerminalSession session)
    {
        var buffer = new byte[8192];
        try
        {
            while (!session.Cancel.IsCancellationRequested)
            {
                var read = await session.Stream.ReadAsync(buffer.AsMemory(0, buffer.Length), session.Cancel.Token);
                if (read <= 0) break; // EOF: shell finalizo

                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                lock (session.Subscribers)
                {
                    foreach (var ch in session.Subscribers) ch.Writer.TryWrite(text);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pump de terminal fallo para {AttemptId}", session.AttemptId);
            lock (session.Subscribers)
            {
                foreach (var ch in session.Subscribers)
                    ch.Writer.TryWrite("\r\n\x1b[31m[Error de conexion con el contenedor]\x1b[0m\r\n");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys.ToArray())
            await StopAsync(id);
    }

    private sealed class TerminalSession
    {
        public required Guid AttemptId { get; init; }
        public required string ContainerId { get; init; }
        public required string ExecId { get; init; }
        public required Stream Stream { get; init; }
        public required CancellationTokenSource Cancel { get; init; }
        public int Cols { get; set; }
        public int Rows { get; set; }
        public List<Channel<string>> Subscribers { get; } = new();
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public Task PumpTask { get; set; } = Task.CompletedTask;
    }
}
