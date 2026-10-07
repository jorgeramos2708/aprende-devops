namespace DevOpsPlatform.LabEngine;

using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using System.Linq;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class TerminalStreamManager : BackgroundService
{
    private readonly IContainerRuntime _containerRuntime;
    private readonly ILogger<TerminalStreamManager> _logger;
    private readonly ConcurrentDictionary<Guid, TerminalSession> _sessions = new();

    public TerminalStreamManager(IContainerRuntime containerRuntime, ILogger<TerminalStreamManager> logger)
    {
        _containerRuntime = containerRuntime;
        _logger = logger;
    }

    public async Task<TerminalSession> StartSessionAsync(Guid attemptId, string containerId, int cols, int rows, CancellationToken ct = default)
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
        session.AttachTask = Task.Run(() => RunAttachLoopAsync(session, ct), ct);

        return session;
    }

    public async Task StopSessionAsync(Guid attemptId)
    {
        if (_sessions.TryRemove(attemptId, out var session))
        {
            session.CancellationTokenSource.Cancel();
            session.InputChannel.Writer.Complete();
            await session.AttachTask;
        }
    }

    public async Task SendInputAsync(Guid attemptId, string input)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            await session.InputChannel.Writer.WriteAsync(input);
        }
    }

    public async Task ResizeAsync(Guid attemptId, int cols, int rows)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            session.Cols = cols;
            session.Rows = rows;
            // Send resize escape sequence
            await session.InputChannel.Writer.WriteAsync($"[8;{rows};{cols}t");
        }
    }

    public IAsyncEnumerable<TerminalOutput> GetOutputAsync(Guid attemptId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(attemptId, out var session))
        {
            return session.OutputChannel.Reader.ReadAllAsync(ct);
        }
        return EmptyOutput();
    }

    private static async IAsyncEnumerable<TerminalOutput> EmptyOutput()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    private async Task RunAttachLoopAsync(TerminalSession session, CancellationToken ct)
    {
        try
        {
            var stream = await _containerRuntime.AttachTerminalAsync(
                session.ContainerId, session.Cols, session.Rows, ct);

            var buffer = new byte[4096];
            var token = session.CancellationTokenSource.Token;
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);
            var inputTask = session.InputChannel.Reader.ReadAsync(token).AsTask();

            while (!token.IsCancellationRequested)
            {
                var completedTask = await Task.WhenAny(readTask, inputTask);

                if (completedTask == readTask)
                {
                    var bytesRead = await readTask;
                    if (bytesRead == 0) break; // EOF

                    var output = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await session.OutputChannel.Writer.WriteAsync(new TerminalOutput { Data = output }, token);
                    if (token.IsCancellationRequested) break;
                    readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);
                }
                else
                {
                    var input = await inputTask;
                    if (token.IsCancellationRequested) break;

                    // Write input to container stdin
                    var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                    await stream.WriteAsync(inputBytes, 0, inputBytes.Length, token);
                    await stream.FlushAsync(token);

                    inputTask = session.InputChannel.Reader.ReadAsync(token).AsTask();
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
                Data = $"[Connection lost: {ex.Message}]",
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

    public class TerminalSession
    {
        public Guid AttemptId { get; set; }
        public string ContainerId { get; set; } = string.Empty;
        public int Cols { get; set; }
        public int Rows { get; set; }
        public System.Threading.Channels.Channel<string> InputChannel { get; set; } = null!;
        public System.Threading.Channels.Channel<TerminalOutput> OutputChannel { get; set; } = null!;
        public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
        public Task AttachTask { get; set; } = Task.CompletedTask;
    }
}
