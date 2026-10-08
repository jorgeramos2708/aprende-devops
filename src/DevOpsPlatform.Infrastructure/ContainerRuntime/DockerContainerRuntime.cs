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
    private readonly DockerSocketExecClient _execClient;
    private readonly ILogger<DockerContainerRuntime> _logger;

    public DockerContainerRuntime(IConfiguration config, ILogger<DockerContainerRuntime> logger)
    {
        var dockerHost = config["Docker:Host"] ?? "unix:///var/run/docker.sock";
        _client = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
        _execClient = new DockerSocketExecClient(dockerHost, logger);
        _logger = logger;
    }

    public async Task<string> CreateContainerAsync(LabEnvironment lab, Guid attemptId, CancellationToken ct = default)
    {
        // Pull si la imagen no esta en el host (Docker.DotNet no auto-descarga en create)
        await EnsureImageAsync(lab.BaseImage, ct);

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
            // "none" | "bridge" (default) | nombre de red docker. Egress por defecto (los labs de apt/redes lo necesitan)
            NetworkMode = resourceLimits.Network,
            SecurityOpt = new List<string> { "no-new-privileges:true" },
            CapDrop = new List<string> { "ALL" },
            // CHOWN: los setup de los labs ajustan propiedad de archivos; DAC_OVERRIDE para /home/lab tmpfs
            CapAdd = new List<string> { "CAP_CHOWN", "CAP_DAC_OVERRIDE" },
            Tmpfs = new Dictionary<string, string>
            {
                ["/tmp"] = "size=100m,noexec,nosuid",
                ["/home/lab"] = "size=200m"
            },
            ReadonlyRootfs = false
        };

        // Runtime alterno: "sysbox-runc" (labs anidados docker/k8s) o "runsc" (gVisor), solo si el host los tiene
        if (!string.IsNullOrWhiteSpace(resourceLimits.Runtime))
        {
            hostConfig.Runtime = resourceLimits.Runtime;
        }

        var createParams = new CreateContainerParameters
        {
            Image = lab.BaseImage,
            // Proceso principal inerte: el usuario entra por exec con TTY (AttachExecShellAsync)
            Cmd = new List<string> { "sleep", "infinity" },
            Labels = labels,
            HostConfig = hostConfig,
            Env = new List<string>
            {
                $"LAB_ATTEMPT_ID={attemptId}",
                $"LAB_TYPE={lab.LabType}",
                $"LAB_TIMEOUT={resourceLimits.TimeoutSeconds}"
            },
            WorkingDir = "/home/lab",
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
        // Via socket crudo (Docker.DotNet no puede hackear respuestas chunked de Engine 25+)
        var result = await _execClient.RunToCompletionAsync(containerId, command, ct);
        return new ContainerExecResult
        {
            ExitCode = result.ExitCode,
            Stdout = result.Stdout,
            Stderr = result.Stderr
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

    public async Task<TerminalExecSession> AttachExecShellAsync(string containerId, int cols, int rows, CancellationToken ct = default)
    {
        // PTY real via socket crudo: funciona igual en Linux y Docker Desktop/Engine 29
        var exec = await _execClient.StartInteractiveAsync(containerId, cols, rows, ct);
        return new TerminalExecSession { ExecId = exec.ExecId, Stream = exec.Raw };
    }

    public async Task<bool> ResizeExecAsync(string execId, int cols, int rows, CancellationToken ct = default)
    {
        try
        {
            await _client.Exec.ResizeContainerExecTtyAsync(execId, new ContainerResizeParameters
            {
                Height = rows,
                Width = cols
            }, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo redimensionar exec {ExecId}", execId);
            return false;
        }
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

    /// <summary>Descarga la imagen si no existe localmente (Zot propio primero).</summary>
    private async Task EnsureImageAsync(string image, CancellationToken ct)
    {
        try
        {
            await _client.Images.InspectImageAsync(image, ct);
            return; // ya esta en el host
        }
        catch { /* no existe localmente: descargar */ }

        _logger.LogInformation("Descargando imagen de lab {Image}...", image);
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image },
            authConfig: null,
            progress: new Progress<JSONMessage>(),
            ct);
        _logger.LogInformation("Imagen {Image} lista", image);
    }

    private static long ParseMemory(string memory)
    {
        memory = memory.ToLowerInvariant().Trim();
        if (memory.EndsWith("g")) return long.Parse(memory[..^1]) * 1024 * 1024 * 1024;
        if (memory.EndsWith("m")) return long.Parse(memory[..^1]) * 1024 * 1024;
        if (memory.EndsWith("k")) return long.Parse(memory[..^1]) * 1024;
        return long.Parse(memory);
    }

    public record LabResourceLimits
    {
        public string Cpus { get; init; } = "0.5";
        public string Memory { get; init; } = "512m";
        public int Pids { get; init; } = 100;
        public int TimeoutSeconds { get; init; } = 1800;
        public string? Runtime { get; init; }      // "sysbox-runc" | "runsc" (opcional)
        public string Network { get; init; } = "bridge";
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
