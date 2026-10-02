namespace DevOpsPlatform.Infrastructure.ContainerRuntime;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
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
            CpuCount = (long)Math.Ceiling(double.Parse(resourceLimits.Cpus)),
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
        var execCreate = await _client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            AttachStdin = false,
            Cmd = command,
            Tty = false
        }, ct);

        var execStart = await _client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters(), ct);

        // Read output
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await _client.Exec.StartAndWaitAsync(execCreate.ID, false, stdout, stderr, ct);

        var inspect = await _client.Exec.InspectContainerExecAsync(execCreate.ID, ct);

        return new ContainerExecResult
        {
            ExitCode = inspect.ExitCode ?? -1,
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
            Stderr = true,
            Tty = true
        };

        var stream = await _client.Containers.AttachContainerAsync(containerId, attachParams, ct);
        
        // Resize terminal
        await _client.Containers.ResizeContainerTTYAsync(containerId, new ContainerResizeParameters { Height = (ushort)rows, Width = (ushort)cols }, ct);
        
        return stream;
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
                State = container.State?.StateString ?? "",
                Labels = container.Config?.Labels ?? new(),
                CreatedAt = DateTimeOffset.Parse(container.Created)
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
            Id = c.ID,
            Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "",
            Image = c.Image,
            Status = c.Status,
            State = c.State,
            Labels = c.Labels,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(c.Created)
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
}
