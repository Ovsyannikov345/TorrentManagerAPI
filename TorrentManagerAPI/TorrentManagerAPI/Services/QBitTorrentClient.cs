using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TorrentManagerAPI.Configuration;

namespace TorrentManagerAPI.Services;

public class QBitTorrentClient : IQBitTorrentClient
{
    private static readonly HashSet<string> CompletedStates =
    [
        "uploading", "stalledUP", "pausedUP", "queuedUP", "forcedUP", "checkingUP"
    ];

    private readonly HttpClient _httpClient;
    private readonly QBitTorrentOptions _options;
    private readonly ILogger<QBitTorrentClient> _logger;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private bool _isAuthenticated;
    private bool? _useLegacyPauseResumeEndpoints;

    public QBitTorrentClient(
        HttpClient httpClient,
        IOptions<QBitTorrentOptions> options,
        ILogger<QBitTorrentClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task AddTorrentAsync(Stream torrentFileStream, string torrentFileName, string tag, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var content = new MultipartFormDataContent
        {
            { new StreamContent(torrentFileStream), "torrents", torrentFileName },
            { new StringContent(tag), "tags" }
        };

        var response = await _httpClient.PostAsync("api/v2/torrents/add", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }
    }

    public async Task<QBitTorrentInfo?> GetTorrentByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"api/v2/torrents/info?tag={Uri.EscapeDataString(tag)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }

        var torrents = await DeserializeAsync<List<QBitTorrentTorrent>>(response, cancellationToken);
        var torrent = torrents?.FirstOrDefault();
        if (torrent is null)
        {
            return null;
        }

        return new QBitTorrentInfo(torrent.Hash, torrent.Name, torrent.Progress, torrent.State);
    }

    public async Task<bool> IsTorrentCompleteAsync(string hash, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"api/v2/torrents/info?hashes={hash}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }

        var torrents = await DeserializeAsync<List<QBitTorrentTorrent>>(response, cancellationToken);
        var torrent = torrents?.FirstOrDefault();
        if (torrent is null)
        {
            return false;
        }

        return torrent.Progress >= 1.0 && CompletedStates.Contains(torrent.State);
    }

    public async Task<QBitTorrentContent> GetTorrentContentAsync(string hash, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var propertiesResponse = await _httpClient.GetAsync($"api/v2/torrents/properties?hash={hash}", cancellationToken);

        if (!propertiesResponse.IsSuccessStatusCode)
        {
            var body = await propertiesResponse.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }

        var properties = await DeserializeAsync<QBitTorrentProperties>(propertiesResponse, cancellationToken)
            ?? throw new InvalidOperationException($"Could not read torrent properties for hash '{hash}'.");

        var filesResponse = await _httpClient.GetAsync($"api/v2/torrents/files?hash={hash}", cancellationToken);

        if (!filesResponse.IsSuccessStatusCode)
        {
            var body = await filesResponse.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }

        var files = await DeserializeAsync<List<QBitTorrentFileEntry>>(filesResponse, cancellationToken) ?? [];

        var torrentFiles = files
            .Select(f => new QBitTorrentFile(f.Name, f.Size, f.Progress))
            .ToList();

        return new QBitTorrentContent(properties.SavePath, properties.ContentPath, torrentFiles);
    }

    public async Task SetTorrentLocationAsync(string hash, string location, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        //await StopTorrentAsync(hash, cancellationToken);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hashes"] = hash,
            ["location"] = location
        });

        var response = await _httpClient.PostAsync("api/v2/torrents/setLocation", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }

        _logger.LogInformation("Set torrent {Hash} location to {Location}", hash, location);
    }

    public async Task RenameFolderAsync(
        string hash,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hash"] = hash,
            ["oldPath"] = oldPath,
            ["newPath"] = newPath
        });

        var response = await _httpClient.PostAsync("api/v2/torrents/renameFolder", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }
    }

    public async Task RenameFileAsync(
        string hash,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hash"] = hash,
            ["oldPath"] = oldPath,
            ["newPath"] = newPath
        });

        var response = await _httpClient.PostAsync("api/v2/torrents/renameFile", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(body);

            throw new InvalidOperationException();
        }
    }

    //public async Task ResumeTorrentAsync(string hash, CancellationToken cancellationToken = default)
    //{
    //    await EnsureAuthenticatedAsync(cancellationToken);
    //    await StartTorrentAsync(hash, cancellationToken);
    //}

    //private async Task StopTorrentAsync(string hash, CancellationToken cancellationToken)
    //{
    //    if (_useLegacyPauseResumeEndpoints == true)
    //    {
    //        await PostTorrentControlAsync("pause", hash, cancellationToken);
    //        return;
    //    }

    //    var response = await PostTorrentControlAsync("stop", hash, cancellationToken, throwOnFailure: false);
    //    if (response.StatusCode == HttpStatusCode.NotFound)
    //    {
    //        _logger.LogDebug("qBittorrent pause endpoint not found; falling back to legacy /torrents/pause");
    //        _useLegacyPauseResumeEndpoints = true;
    //        await PostTorrentControlAsync("pause", hash, cancellationToken);
    //        return;
    //    }

    //    _useLegacyPauseResumeEndpoints = false;
    //    response.EnsureSuccessStatusCode();
    //}

    //private async Task StartTorrentAsync(string hash, CancellationToken cancellationToken)
    //{
    //    if (_useLegacyPauseResumeEndpoints == true)
    //    {
    //        await PostTorrentControlAsync("resume", hash, cancellationToken);
    //        return;
    //    }

    //    var response = await PostTorrentControlAsync("start", hash, cancellationToken, throwOnFailure: false);
    //    if (response.StatusCode == HttpStatusCode.NotFound)
    //    {
    //        _logger.LogDebug("qBittorrent resume endpoint not found; falling back to legacy /torrents/resume");
    //        _useLegacyPauseResumeEndpoints = true;
    //        await PostTorrentControlAsync("resume", hash, cancellationToken);
    //        return;
    //    }

    //    _useLegacyPauseResumeEndpoints = false;
    //    response.EnsureSuccessStatusCode();
    //}

    private async Task<HttpResponseMessage> PostTorrentControlAsync(
        string action,
        string hash,
        CancellationToken cancellationToken,
        bool throwOnFailure = true)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hashes"] = hash
        });

        var response = await _httpClient.PostAsync($"api/v2/torrents/{action}", content, cancellationToken);

        if (throwOnFailure)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"qBittorrent request to 'torrents/{action}' failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }
        }

        return response;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_isAuthenticated)
        {
            return;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_isAuthenticated)
            {
                return;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = _options.Username,
                ["password"] = _options.Password
            });

            var response = await _httpClient.PostAsync("api/v2/auth/login", content, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("qBittorrent authentication failed. Check username and password.");
            }

            response.EnsureSuccessStatusCode();
            _isAuthenticated = true;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream);
    }

    private sealed class QBitTorrentTorrent
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("progress")]
        public double Progress { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
    }

    private sealed class QBitTorrentProperties
    {
        [JsonPropertyName("save_path")]
        public string SavePath { get; set; } = string.Empty;

        [JsonPropertyName("content_path")]
        public string ContentPath { get; set; } = string.Empty;
    }

    private sealed class QBitTorrentFileEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("progress")]
        public double Progress { get; set; }
    }
}
