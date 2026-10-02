namespace DevOpsPlatform.LabEngine;

using DevOpsPlatform.Core.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class TerminalStreamManager : BackgroundService
{
    private readonly IContainerRuntime _containerRuntime;
    private readonly ILogger<TerminalStreamManager> _logger;
    private readonly ConcurrentDictionary<Ulid, TerminalSession> _sessions = new();

    public TerminalStreamManager(IContainerRuntime containerRuntime, ILogger<TerminalStreamManager> logger)
    {
        _containerRuntime = containerRuntime;
        _logger = logger;
    }

    public async Task<TerminalSession> StartSessionAsync(Ulid attemptId, string containerId, int cols, int rows, CancellationToken ct = default)
    {
        var session = new TerminalSession
        {
            AttemptId = attemptId,
            ContainerId = containerId,
            Cols = cols,
            Rows = rows,
            InputChannel = System.Threading.Channels.Channel.CreateUnbounded<string>(),
            OutputChannel = System.Threading.Channels.Channel.CreateUnbounded<TerminalOutput>(),
            CancellationTokenSource = new CancellationTokenSource()
        };

        _sessions[attemptId] = session;

        // Start the Docker attach loop
        _ = Task.Run(() => RunAttachLoopAsync(session, ct), ct);

        return session;
    }

    public async Task StopSessionAsync(Ulid attemptId)
    {
        if (_sessions.TryRemove(attemptId, out var session))
        {
            session.CancellationTokenSource.Cancel();
            session.InputChannel.Writer.Complete();
            await session.AttachTask;
        }
    }

    public async Task SendInputAsync(Ulid attemptId, string input)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            await session.InputChannel.Writer.WriteAsync(input);
        }
    }

    public async Task ResizeAsync(Ulid attemptId, int cols, int rows)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            session.Cols = cols;
            session.Rows = rows;
            // Send resize escape sequence
            await session.InputChannel.Writer.WriteAsync($"[8;{rows};{cols}t");
        }
    }

    public IAsyncEnumerable<TerminalOutput> GetOutputAsync(Ulid attemptId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            return session.OutputChannel.Reader.ReadAllAsync(ct);
        }
        return AsyncEnumerable.Empty<TerminalOutput>();
    }

    private async Task RunAttachLoopAsync(TerminalSession session, CancellationToken ct)
    {
        try
        {
            var stream = await _containerRuntime.AttachTerminalAsync(
                session.ContainerId, session.Cols, session.Rows, ct);

            var buffer = new byte[4096];
            var inputTask = session.InputChannel.Reader.ReadAsync(session.CancellationTokenSource.Token);

            while (!session.CancellationTokenSource.Token.IsCancellationRequested)
            {
                var readTask = stream.ReadAsync(buffer, 0, buffer.Length, session.CancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(readTask.AsTask(), inputTask.AsTask());

                if (completedTask == readTask.AsTask())
                {
                    var bytesRead = readTask.Result;
                    if (bytesRead == 0) break; // EOF

                    var output = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await session.OutputChannel.Writer.WriteAsync(new TerminalOutput { Data = output });
                }
                else
                {
                    var input = inputTask.Result;
                    if (session.CancellationTokenSource.Token.IsCancellationRequested) break;

                    // Write input to container stdin
                    await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(input), 0, input.Length, session.CancellationTokenSource.Token);
                    await stream.FlushAsync(session.CancellationTokenSource.Token);
                    
                    inputTask = session.InputChannel.Reader.ReadAsync(session.CancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal attach loop failed for attempt {AttemptId}", session.AttemptId);
            await session.OutputChannel.Writer.WriteAsync(new TerminalOutput 
            { 
                Data = $"
[Connection lost: {ex.Message}]
", 
                IsError = true 
            });
        }
        finally
        {
            session.OutputChannel.Writer.Complete();
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Periodic cleanup of dead sessions
        return Task.CompletedTask;
    }

    private class TerminalSession
    {
        public Ulid AttemptId { get; set; }
        public string ContainerId { get; set; } = string.Empty;
        public int Cols { get; set; }
        public int Rows { get; set; }
        public System.Threading.Channels.Channel<string> InputChannel { get; set; } = null!;
        public System.Threading.Channels.Channel<TerminalOutput> OutputChannel { get; set; } = null!;
        public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
        public Task AttachTask { get; set; } = Task.CompletedTask;
    }
}
