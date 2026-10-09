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
        // Sesion existente pero muerta (pump termino: EOF, contenedor caido) -> recrear
        if (_sessions.TryGetValue(attemptId, out var muerta) && muerta.PumpTask.IsCompleted)
        {
            await StopAsync(attemptId);
        }
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
            await exec.Stream.DisposeAsync();
            return;
        }

        session.PumpTask = Task.Run(() => PumpOutputAsync(session));
        _logger.LogInformation("Sesion de terminal iniciada para intento {AttemptId} (contenedor {ContainerId}, exec {ExecId})",
            attemptId, containerId, exec.ExecId);
    }

    public ChannelReader<string> Subscribe(Guid attemptId)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            lock (session.Subscribers)
            {
                // Replay: la salida previa (incluido el prompt) se habia difundido antes de
                // que esta pestaña se suscribiera -> entregarla como primer frame
                if (!string.IsNullOrEmpty(session.OutputTail))
                    channel.Writer.TryWrite(session.OutputTail);
                session.Subscribers.Add(channel);
            }
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
                if (read <= 0)
                {
                    _logger.LogWarning("Pump {AttemptId}: EOF del stream exec", session.AttemptId);
                    break; // EOF: shell finalizo
                }

                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                _logger.LogInformation("Pump {AttemptId}: leidos {N} bytes", session.AttemptId, read);
                session.AppendTail(text);
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
            session.AppendTail("\r\n\x1b[31m[Error de conexion con el contenedor]\x1b[0m\r\n");
        }

        // La sesion murio (EOF/expirada): drenar suscriptores y retirarla para que
        // una futura reconexion la recree desde cero
        _sessions.TryRemove(session.AttemptId, out _);
        lock (session.Subscribers)
        {
            foreach (var ch in session.Subscribers) ch.Writer.TryComplete();
            session.Subscribers.Clear();
        }
        _logger.LogInformation("Sesion de terminal finalizada para intento {AttemptId}", session.AttemptId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys.ToArray())
            await StopAsync(id);
    }

    private sealed class TerminalSession
    {
        private const int TailLimit = 256 * 1024; // ultimos 256 KB de salida (prompt + contexto al reconectar)
        private string _tail = string.Empty;

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

        public string OutputTail => _tail;

        public void AppendTail(string text)
        {
            _tail = _tail.Length + text.Length > TailLimit
                ? _tail[(_tail.Length + text.Length - TailLimit)..] + text
                : _tail + text;
        }
    }
}
