namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using Markdig;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using StackExchange.Redis;
using System.Text.Json;

public class RedisCacheService : IDistributedCache
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer muxer, ILogger<RedisCacheService> logger)
    {
        _db = muxer.GetDatabase();
        _logger = logger;
    }

    public byte[]? Get(string key) => _db.StringGet(key);
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default) => await _db.StringGetAsync(key);
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _db.StringSet(key, value, options.AbsoluteExpirationRelativeToNow ?? TimeSpan.FromHours(1));
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => await _db.StringSetAsync(key, value, options.AbsoluteExpirationRelativeToNow ?? TimeSpan.FromHours(1));
    public void Refresh(string key) => _db.KeyExpire(key, TimeSpan.FromHours(1));
    public async Task RefreshAsync(string key, CancellationToken token = default) => await _db.KeyExpireAsync(key, TimeSpan.FromHours(1));
    public void Remove(string key) => _db.KeyDelete(key);
    public async Task RemoveAsync(string key, CancellationToken token = default) => await _db.KeyDeleteAsync(key);
}

public class MinioStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IConfiguration config, ILogger<MinioStorageService> logger)
    {
        var endpoint = config["MinIO:Endpoint"] ?? "minio:9000";
        var accessKey = config["MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = config["MinIO:SecretKey"] ?? "minioadmin";
        _bucket = config["MinIO:Bucket"] ?? "devops-platform";

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(false)
            .Build();

        _logger = logger;
        EnsureBucketAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketAsync()
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket));
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket));
            _logger.LogInformation("Created bucket {Bucket}", _bucket);
        }
    }

    public async Task<string> UploadAsync(string objectName, Stream data, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType), ct);
        return objectName;
    }

    public async Task<Stream> DownloadAsync(string objectName, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(objectName), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(objectName), ct);
    }

    public string GetPresignedUrl(string objectName, TimeSpan expiry)
    {
        return _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithExpiry((int)expiry.TotalSeconds)).GetAwaiter().GetResult();
    }
}

public class MarkdownRenderingService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderingService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseEmphasisExtras()
            .UseGridTables()
            .UseTaskLists()
            .UseYamlFrontMatter()
            .Build();
    }

    public string Render(string markdown)
    {
        return Markdown.ToHtml(markdown, _pipeline);
    }

    public (Dictionary<string, string> FrontMatter, string Content) ParseFrontMatter(string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown, _pipeline);
        var frontMatter = new Dictionary<string, string>();
        var contentBuilder = new System.Text.StringBuilder();
        bool inFrontMatter = false;

        foreach (var line in markdown.Split('\n'))
        {
            if (line.Trim() == "---")
            {
                inFrontMatter = !inFrontMatter;
                continue;
            }
            if (inFrontMatter)
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    frontMatter[parts[0].Trim()] = parts[1].Trim();
            }
            else
            {
                contentBuilder.AppendLine(line);
            }
        }

        return (frontMatter, contentBuilder.ToString().Trim());
    }
}

public class YamlContentService
{
    public T Deserialize<T>(string yaml) where T : class
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        return deserializer.Deserialize<T>(yaml);
    }

    public string Serialize<T>(T obj)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
        return serializer.Serialize(obj);
    }
}
