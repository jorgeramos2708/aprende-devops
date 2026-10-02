namespace DevOpsPlatform.TechWatcher.Connectors;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Octokit;
using System.Text.Json;
using System.Xml.Linq;

public interface ISourceConnector
{
    string SourceType { get; }
    Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default);
}

public class GitHubConnector : ISourceConnector
{
    public string SourceType => "github_releases";
    private readonly HttpClient _http;

    public GitHubConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            // source.SourceUrl format: "owner/repo" or full URL
            var repo = source.SourceUrl.Replace("https://github.com/", "").Replace("https://api.github.com/repos/", "");
            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("DevOpsPlatform-TechWatcher/1.0");
            
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Try tags
                    url = $"https://api.github.com/repos/{repo}/tags";
                    response = await _http.GetAsync(url, ct);
                }
            }

            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"GitHub API: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            
            string? version = null;
            string? hash = null;

            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                // Tags response
                version = doc.RootElement[0].GetProperty("name").GetString();
            }
            else
            {
                // Release response
                version = doc.RootElement.GetProperty("tag_name").GetString();
            }

            hash = ComputeHash(json);

            return new SourceCheckResult
            {
                HasChanges = true, // Will be compared in service
                CurrentVersion = version?.TrimStart('v'),
                ContentHash = hash,
                RawData = doc
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public class DockerHubConnector : ISourceConnector
{
    public string SourceType => "docker_tags";
    private readonly HttpClient _http;

    public DockerHubConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            // source.SourceUrl format: "library/ubuntu" or "nginx"
            var repo = source.SourceUrl;
            var url = $"https://hub.docker.com/v2/repositories/{repo}/tags/?page_size=10&ordering=-last_updated";
            
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"Docker Hub API: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            
            var tags = doc.RootElement.GetProperty("results").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString())
                .Where(n => !string.IsNullOrEmpty(n) && n != "latest")
                .ToArray();

            var latest = tags.FirstOrDefault();
            var hash = ComputeHash(json);

            return new SourceCheckResult
            {
                HasChanges = true,
                CurrentVersion = latest,
                ContentHash = hash,
                RawData = doc
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public class RssConnector : ISourceConnector
{
    public string SourceType => "rss";
    private readonly HttpClient _http;

    public RssConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(source.SourceUrl, ct);
            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"RSS fetch: {response.StatusCode}" };

            var xml = await response.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xml);
            
            var latestItem = doc.Descendants("item").FirstOrDefault();
            if (latestItem == null)
                latestItem = doc.Descendants("entry").FirstOrDefault(); // Atom

            var title = latestItem?.Element("title")?.Value ?? latestItem?.Element("{http://www.w3.org/2005/Atom}title")?.Value;
            var link = latestItem?.Element("link")?.Value ?? latestItem?.Element("{http://www.w3.org/2005/Atom}link")?.Attribute("href")?.Value;
            var pubDate = latestItem?.Element("pubDate")?.Value ?? latestItem?.Element("{http://www.w3.org/2005/Atom}updated")?.Value;
            
            var hash = ComputeHash(xml);

            return new SourceCheckResult
            {
                HasChanges = true,
                CurrentVersion = title,
                ContentHash = hash,
                RawData = JsonDocument.Parse(JsonSerializer.Serialize(new { title, link, pubDate }))
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public class HtmlConnector : ISourceConnector
{
    public string SourceType => "html";
    private readonly HttpClient _http;

    public HtmlConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(source.SourceUrl, ct);
            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"HTML fetch: {response.StatusCode}" };

            var html = await response.Content.ReadAsStringAsync(ct);
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);

            // Use selectors from source configuration
            var selectors = source.Selectors?.RootElement.GetProperty("selectors");
            string? version = null;
            string? content = null;

            if (selectors.HasValue)
            {
                var versionSelector = selectors.Value.GetProperty("version").GetString();
                var contentSelector = selectors.Value.GetProperty("content").GetString();

                if (!string.IsNullOrEmpty(versionSelector))
                {
                    var el = document.QuerySelector(versionSelector);
                    version = el?.TextContent.Trim();
                }

                if (!string.IsNullOrEmpty(contentSelector))
                {
                    var el = document.QuerySelector(contentSelector);
                    content = el?.OuterHtml ?? el?.TextContent;
                }
            }

            var hash = ComputeHash(content ?? html);

            return new SourceCheckResult
            {
                HasChanges = true,
                CurrentVersion = version,
                ContentHash = hash,
                RawData = JsonDocument.Parse(JsonSerializer.Serialize(new { version, content: content?[..5000] }))
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public class NpmConnector : ISourceConnector
{
    public string SourceType => "npm";
    private readonly HttpClient _http;

    public NpmConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            var package = source.SourceUrl; // e.g., "terraform" or "@scope/package"
            var url = $"https://registry.npmjs.org/{package}/latest";
            
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"NPM API: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            
            var version = doc.RootElement.GetProperty("version").GetString();
            var hash = ComputeHash(json);

            return new SourceCheckResult
            {
                HasChanges = true,
                CurrentVersion = version,
                ContentHash = hash,
                RawData = doc
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public class PyPIConnector : ISourceConnector
{
    public string SourceType => "pypi";
    private readonly HttpClient _http;

    public PyPIConnector(HttpClient http) => _http = http;

    public async Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default)
    {
        try
        {
            var package = source.SourceUrl; // e.g., "ansible"
            var url = $"https://pypi.org/pypi/{package}/json";
            
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return new SourceCheckResult { HasChanges = false, Error = $"PyPI API: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            
            var version = doc.RootElement.GetProperty("info").GetProperty("version").GetString();
            var hash = ComputeHash(json);

            return new SourceCheckResult
            {
                HasChanges = true,
                CurrentVersion = version,
                ContentHash = hash,
                RawData = doc
            };
        }
        catch (Exception ex)
        {
            return new SourceCheckResult { HasChanges = false, Error = ex.Message };
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}
