namespace DevOpsPlatform.Infrastructure.ContainerRuntime;

using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// Cliente minimo de `exec` sobre el socket de Docker (unix o npipe).
///
/// Por que existe: Docker.DotNet hace "hijack" de la conexion para exec start/attach
/// y, desde Docker Engine 25+/29 (Transfer-Encoding: chunked), lanza
/// "cannot hijack chunked or content length stream". Aqui hablamos HTTP/1.1
/// directamente: chunked para respuestas normales, Upgrade: tcp para el stream.
/// </summary>
public sealed class DockerSocketExecClient
{
    private readonly string _socketPath; // "/var/run/docker.sock" o nombre de pipe
    private readonly bool _isNamedPipe;
    private readonly ILogger _logger;
    private const string ApiPrefix = "/v1.43";

    public DockerSocketExecClient(string dockerHost, ILogger logger)
    {
        var uri = new Uri(dockerHost);
        _logger = logger;
        if (uri.Scheme == "unix")
        {
            _isNamedPipe = false;
            _socketPath = uri.AbsolutePath;
        }
        else if (uri.Scheme == "npipe")
        {
            _isNamedPipe = true;
            // npipe://./pipe/docker_engine -> docker_engine
            _socketPath = uri.AbsolutePath.Trim('/').Replace("pipe/", "");
        }
        else
        {
            // tcp:// u otro no soportado en esta iteracion (nuestros hosts usan socket)
            throw new NotSupportedException($"Docker host no soportado por el cliente exec: {dockerHost}");
        }
    }

    /// <summary>exec interactivo con PTY -> stream crudo bidireccional listo para la terminal.</summary>
    public async Task<ExecStream> StartInteractiveAsync(string containerId, int cols, int rows, CancellationToken ct)
    {
        var execId = await CreateExecAsync(containerId, new List<string> { "sh", "-c", "exec bash -l 2>/dev/null || exec sh -l" },
            stdin: true, tty: true, cols, rows, ct);
        var stream = await ConnectAsync(ct);
        var body = "{\"Detach\":false,\"Tty\":true}";
        await WriteRequestAsync(stream, "POST", $"{ApiPrefix}/exec/{execId}/start", body, upgrade: true, ct);
        var head = await ReadHeadAsync(stream, ct);
        if (head.StatusCode != 101 && head.StatusCode != 200)
            throw new InvalidOperationException($"exec start respondio {head.StatusCode}");

        return new ExecStream(execId, stream, Pty: true);
    }

    /// <summary>exec no interactivo: corre a completar y devuelve stdout/stderr/exit code.</summary>
    public async Task<ExecRunResult> RunToCompletionAsync(string containerId, string[] cmd, CancellationToken ct)
    {
        var execId = await CreateExecAsync(containerId, cmd.ToList(), stdin: false, tty: false, cols: 0, rows: 0, ct);
        var stream = await ConnectAsync(ct);
        var body = "{\"Detach\":false,\"Tty\":false}";
        await WriteRequestAsync(stream, "POST", $"{ApiPrefix}/exec/{execId}/start", body, upgrade: true, ct);
        var head = await ReadHeadAsync(stream, ct);
        if (head.StatusCode != 101 && head.StatusCode != 200)
            throw new InvalidOperationException($"exec start respondio {head.StatusCode}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        await ReadMultiplexedAsync(stream, stdout, stderr, ct);
        var exitCode = await GetExitCodeAsync(execId, ct);
        await stream.DisposeAsync();
        return new ExecRunResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    public async Task<bool> ResizeAsync(string execId, int cols, int rows, CancellationToken ct)
    {
        try
        {
            var stream = await ConnectAsync(ct);
            await WriteRequestAsync(stream, "POST", $"{ApiPrefix}/exec/{execId}/resize?h={rows}&w={cols}", null, upgrade: false, ct);
            var head = await ReadHeadAsync(stream, ct);
            _ = await ReadBodyAsync(stream, head, ct);
            await stream.DisposeAsync();
            return head.StatusCode is 200 or 201 or 204;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resize de exec {ExecId} fallo", execId);
            return false;
        }
    }

    // ---------------------------------------------------------------- interno

    private async Task<string> CreateExecAsync(string containerId, List<string> cmd, bool stdin, bool tty, int cols, int rows, CancellationToken ct)
    {
        var env = new List<string> { "TERM=xterm-256color" };
        if (cols > 0 && rows > 0) { env.Add($"COLUMNS={cols}"); env.Add($"LINES={rows}"); }

        var payload = JsonSerializer.Serialize(new
        {
            AttachStdin = stdin,
            AttachStdout = true,
            AttachStderr = true,
            Tty = tty,
            Env = env,
            Cmd = cmd
        });

        var stream = await ConnectAsync(ct);
        await WriteRequestAsync(stream, "POST", $"{ApiPrefix}/containers/{containerId}/exec", payload, upgrade: false, ct);
        var head = await ReadHeadAsync(stream, ct);
        var body = await ReadBodyAsync(stream, head, ct);
        await stream.DisposeAsync();

        if (head.StatusCode != 201)
            throw new InvalidOperationException($"exec create respondio {head.StatusCode}: {Encoding.UTF8.GetString(body)}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("Id").GetString()!;
    }

    private async Task<int> GetExitCodeAsync(string execId, CancellationToken ct)
    {
        var stream = await ConnectAsync(ct);
        await WriteRequestAsync(stream, "GET", $"{ApiPrefix}/exec/{execId}/json", null, upgrade: false, ct);
        var head = await ReadHeadAsync(stream, ct);
        var body = await ReadBodyAsync(stream, head, ct);
        await stream.DisposeAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("ExitCode").GetInt32();
    }

    private async Task<Stream> ConnectAsync(CancellationToken ct)
    {
        if (_isNamedPipe)
        {
            var pipe = new NamedPipeClientStream(".", _socketPath, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ct);
            return pipe;
        }
        var endPoint = new UnixDomainSocketEndPoint(_socketPath);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(endPoint, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }

    private static async Task WriteRequestAsync(Stream stream, string method, string path, string? body, bool upgrade, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
        sb.Append("Host: localhost\r\n");
        if (upgrade) sb.Append("Connection: Upgrade\r\nUpgrade: tcp\r\n");
        else sb.Append("Connection: close\r\n");
        if (body != null)
        {
            sb.Append("Content-Type: application/json\r\n");
            sb.Append("Content-Length: ").Append(Encoding.UTF8.GetByteCount(body)).Append("\r\n");
        }
        sb.Append("\r\n");
        var headBytes = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(headBytes, ct);
        if (body != null) await stream.WriteAsync(Encoding.UTF8.GetBytes(body), ct);
        await stream.FlushAsync(ct);
    }

    private sealed record HttpHead(int StatusCode, Dictionary<string, string> Headers)
    {
        public bool IsChunked => Headers.TryGetValue("transfer-encoding", out var te)
                                 && te.Contains("chunked", StringComparison.OrdinalIgnoreCase);
        public int ContentLength => Headers.TryGetValue("content-length", out var cl) && int.TryParse(cl, out var n) ? n : -1;
    }

    private static async Task<HttpHead> ReadHeadAsync(Stream stream, CancellationToken ct)
    {
        var mem = new MemoryStream();
        var one = new byte[1];
        uint last4 = 0;
        var guard = 0;
        while (guard++ < 65536)
        {
            var n = await stream.ReadAsync(one, ct);
            if (n <= 0) throw new EndOfStreamException("EOF leyendo cabeceras HTTP");
            mem.WriteByte(one[0]);
            last4 = ((last4 << 8) | one[0]) & 0xFFFFFFFF;
            if (last4 == 0x0D0A0D0A) break; // \r\n\r\n
        }

        var text = Encoding.ASCII.GetString(mem.ToArray());
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var parts = lines[0].Split(' ');
        var status = parts.Length > 1 && int.TryParse(parts[1], out var s) ? s : 0;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var idx = line.IndexOf(':');
            if (idx > 0) headers[line[..idx].Trim().ToLowerInvariant()] = line[(idx + 1)..].Trim();
        }
        return new HttpHead(status, headers);
    }

    private static async Task<byte[]> ReadBodyAsync(Stream stream, HttpHead head, CancellationToken ct)
    {
        if (!head.IsChunked)
        {
            if (head.ContentLength <= 0) return [];
            return await ReadExactAsync(stream, head.ContentLength, ct);
        }

        // chunked: <hex>\r\n <datos> \r\n ... 0\r\n\r\n
        using var mem = new MemoryStream();
        while (true)
        {
            var sizeHex = (await ReadLineAsync(stream, ct)).Trim();
            if (!int.TryParse(sizeHex, System.Globalization.NumberStyles.HexNumber, null, out var size)) break;
            if (size == 0) { await ReadLineAsync(stream, ct); break; }
            mem.Write(await ReadExactAsync(stream, size, ct));
            await ReadLineAsync(stream, ct); // \r\n tras el chunk
        }
        return mem.ToArray();
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var one = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(one, ct);
            if (n <= 0) break;
            if (one[0] == (byte)'\n') break;
            if (one[0] != (byte)'\r') sb.Append((char)one[0]);
        }
        return sb.ToString();
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, count - read), ct);
            if (n <= 0) throw new EndOfStreamException($"EOF con {read}/{count} bytes");
            read += n;
        }
        return buf;
    }

    // Frames multiplexados [canal:1, 0,0,0, tam:4be][datos] hasta EOF.
    private static async Task ReadMultiplexedAsync(Stream stream, StringBuilder stdout, StringBuilder stderr, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                var header = await ReadExactAsync(stream, 8, ct);
                var channel = header[0];
                var sizeBytes = header[4..8];
                if (BitConverter.IsLittleEndian) Array.Reverse(sizeBytes);
                var size = BitConverter.ToInt32(sizeBytes, 0);
                if (size > 0)
                {
                    var payload = await ReadExactAsync(stream, size, ct);
                    var text = Encoding.UTF8.GetString(payload);
                    if (channel == 2) stderr.Append(text); else stdout.Append(text);
                }
            }
        }
        catch (EndOfStreamException) { /* EOF = proceso termino */ }
        catch (IOException) { /* socket cerrado */ }
    }

    public sealed record ExecStream(string ExecId, Stream Raw, bool Pty)
    {
        public Task WriteAsync(string data, CancellationToken ct = default)
            => Raw.WriteAsync(Encoding.UTF8.GetBytes(data), ct).AsTask();

        public async ValueTask DisposeAsync() => await Raw.DisposeAsync();
    }

    public sealed record ExecRunResult(int ExitCode, string Stdout, string Stderr);
}
