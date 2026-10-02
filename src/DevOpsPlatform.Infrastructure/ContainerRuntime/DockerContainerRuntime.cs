namespace DevOpsPlatform.Infrastructure.ContainerRuntime;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

public class DockerContainerRuntime : IContainerRuntime
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerContainerRuntime> _logger;

    public DockerContainerRuntime(IConfiguration config, ILogger<DockerContainerRuntime> logger)
    {
        var dockerHost = config["Docker:Host"] ?? "unix:///var/run/docker.sock";
        _client = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
        _logger = logger;
    }

    public async Task<string> CreateContainerAsync(LabEnvironment lab, Ulid attemptId, CancellationToken ct = default)
    {
        var labels = new Dictionary<string, string>
        {
            ["lab.engine"] = "true",
            ["lab.attempt_id"] = attemptId.ToString(),
            ["lab.environment_id"] = lab.Id.ToString(),
            ["lab.type"] = lab.LabType.ToString().ToLowerInvariant()
        };

        var resourceLimits = JsonSerializer.Deserialize<LabResourceLimits>(lab.ResourceLimits.RootElement.GetRawText())
            ?? new LabResourceLimits();

        var hostConfig = new HostConfig
        {
            NanoCPUs = (long)(double.Parse(resourceLimits.Cpus, CultureInfo.InvariantCulture) * 1_000_000_000),
            Memory = ParseMemory(resourceLimits.Memory),
            PidsLimit = resourceLimits.Pids,
            AutoRemove = false,
            NetworkMode = "none", // Isolated by default; can be overridden per lab
            SecurityOpt = new List<string> { "no-new-privileges:true" },
            CapDrop = new List<string> { "ALL" },
            CapAdd = new List<string> { "CAP_DAC_OVERRIDE" },
            Tmpfs = new Dictionary<string, string>
            {
                ["/tmp"] = "size=100m,noexec,nosuid",
                ["/home/lab"] = "size=200m"
            },
            ReadonlyRootfs = false
        };

        // gVisor runtime for isolation
        if (resourceLimits.UseGVisor)
        {
            hostConfig.Runtime = "runsc";
        }

        var createParams = new CreateContainerParameters
        {
            Image = lab.BaseImage,
            Labels = labels,
            HostConfig = hostConfig,
            Env = new List<string>
            {
                $"LAB_ATTEMPT_ID={attemptId}",
                $"LAB_TYPE={lab.LabType}",
                $"LAB_TIMEOUT={resourceLimits.TimeoutSeconds}"
            },
            WorkingDir = "/workspace",
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            Tty = true,
            OpenStdin = true,
            StdinOnce = false
        };

        var response = await _client.Containers.CreateContainerAsync(createParams, ct);
        _logger.LogInformation("Created container {ContainerId} for attempt {AttemptId}", response.ID, attemptId);
        return response.ID;
    }

    public async Task<bool> StartContainerAsync(string containerId, CancellationToken ct = default)
    {
        return await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);
    }

    public async Task<bool> StopContainerAsync(string containerId, CancellationToken ct = default)
    {
        try
        {
            await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<bool> RemoveContainerAsync(string containerId, CancellationToken ct = default)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true, RemoveVolumes = true }, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<ContainerExecResult> ExecAsync(string containerId, string[] command, CancellationToken ct = default)
    {
        var execCreate = await _client.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            AttachStdin = false,
            Cmd = command,
            Tty = false
        }, ct);

        using var execStream = await _client.Exec.StartWithConfigContainerExecAsync(execCreate.ID, new ContainerExecStartParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,
            Detach = false
        }, ct);

        // Read output
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        await execStream.CopyOutputToAsync(Stream.Null, stdout, stderr, ct);

        var inspect = await _client.Exec.InspectContainerExecAsync(execCreate.ID, ct);

        return new ContainerExecResult
        {
            ExitCode = (int)inspect.ExitCode,
            Stdout = System.Text.Encoding.UTF8.GetString(stdout.ToArray()),
            Stderr = System.Text.Encoding.UTF8.GetString(stderr.ToArray())
        };
    }

    public async Task<Stream> AttachTerminalAsync(string containerId, int cols, int rows, CancellationToken ct = default)
    {
        var attachParams = new ContainerAttachParameters
        {
            Stream = true,
            Stdin = true,
            Stdout = true,
            Stderr = true
        };

        var stream = await _client.Containers.AttachContainerAsync(containerId, true, attachParams, ct);

        // Resize terminal
        await _client.Containers.ResizeContainerTtyAsync(containerId, new ContainerResizeParameters { Height = rows, Width = cols }, ct);

        return new MultiplexedStreamAdapter(stream);
    }

    public async Task<ContainerInfo?> GetContainerInfoAsync(string containerId, CancellationToken ct = default)
    {
        try
        {
            var container = await _client.Containers.InspectContainerAsync(containerId, ct);
            return new ContainerInfo
            {
                Id = container.ID,
                Name = container.Name.TrimStart('/'),
                Image = container.Config?.Image ?? "",
                Status = container.State?.Status ?? "",
                State = container.State?.Running == true ? "running" : container.State?.Status ?? "",
                IpAddress = container.NetworkSettings?.Networks?.Values.FirstOrDefault()?.IPAddress
                    ?? container.NetworkSettings?.IPAddress ?? "",
                Labels = container.Config?.Labels != null ? new Dictionary<string, string>(container.Config.Labels) : new(),
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(container.Created, DateTimeKind.Utc))
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(string? labelFilter = null, CancellationToken ct = default)
    {
        var filters = new Dictionary<string, IDictionary<string, bool>>();
        if (!string.IsNullOrEmpty(labelFilter))
        {
            filters["label"] = new Dictionary<string, bool> { [labelFilter] = true };
        }

        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = filters
        }, ct);

        return containers.Select(c => new ContainerInfo
        {
            Id = c.ID ?? "",
            Name = c.Names?.FirstOrDefault()?.TrimStart('/') ?? "",
            Image = c.Image ?? "",
            Status = c.Status ?? "",
            State = c.State ?? "",
            Labels = c.Labels != null ? new Dictionary<string, string>(c.Labels) : new(),
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(c.Created, DateTimeKind.Utc))
        }).ToList();
    }

    private static long ParseMemory(string memory)
    {
        memory = memory.ToLowerInvariant().Trim();
        if (memory.EndsWith("g")) return long.Parse(memory[..^1]) * 1024 * 1024 * 1024;
        if (memory.EndsWith("m")) return long.Parse(memory[..^1]) * 1024 * 1024;
        if (memory.EndsWith("k")) return long.Parse(memory[..^1]) * 1024;
        return long.Parse(memory);
    }

    private record LabResourceLimits
    {
        public string Cpus { get; init; } = "0.5";
        public string Memory { get; init; } = "512m";
        public int Pids { get; init; } = 100;
        public int TimeoutSeconds { get; init; } = 1800;
        public bool UseGVisor { get; init; } = true;
    }

    /// <summary>
    /// Adapta MultiplexedStream (no es un Stream) a System.IO.Stream para la terminal interactiva.
    /// </summary>
    private sealed class MultiplexedStreamAdapter : Stream
    {
        private readonly MultiplexedStream _inner;

        public MultiplexedStreamAdapter(MultiplexedStream inner) => _inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            var result = await _inner.ReadOutputAsync(buffer, offset, count, ct).ConfigureAwait(false);
            return result.EOF ? 0 : result.Count;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => _inner.WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _inner.WriteAsync(buffer, offset, count, ct);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
