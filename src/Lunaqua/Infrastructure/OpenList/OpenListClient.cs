using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Lunaqua.Infrastructure.OpenList;

/// <summary>
/// 自研 OpenList 轻客户端（匿名只读，规格书 §5）。
/// 铁律：<c>/api/*</c> 恒 HTTP 200，成败看 JSON 的 <c>code</c>；解析不出 JSON 视为错误。
/// </summary>
public sealed class OpenListClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly OpenListOptions _options;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate;

    public OpenListClient(HttpClient http, OpenListOptions? options = null, ILogger? log = null)
    {
        _http = http;
        _options = options ?? new OpenListOptions();
        _log = log ?? Log.ForContext<OpenListClient>();
        _gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    public OpenListOptions Options => _options;

    /// <summary>站点公开设置。</summary>
    public async Task<OpenListSiteSettings> GetSiteSettingsAsync(CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<Dictionary<string, JsonElement>>("public/settings", new { }, cancellationToken);
        return OpenListSiteSettings.FromDictionary(data ?? []);
    }

    /// <summary>列目录（务必显式传 per_page）。</summary>
    public async Task<OpenListListResult> ListAsync(string path, int page = 1, int perPage = 0, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            path,
            password = string.Empty,
            page,
            per_page = perPage > 0 ? perPage : _options.PageSize,
            refresh = false,
        };

        var data = await PostAsync<FsListDataDto>("fs/list", body, cancellationToken) ?? new FsListDataDto();
        var entries = (data.Content ?? []).Select(ToEntry).ToList();
        return new OpenListListResult(entries, data.Total, data.Write, data.Provider);
    }

    /// <summary>取文件信息与带签名的直链（sign 每次重取，不要持久化）。</summary>
    public async Task<OpenListFileInfo> GetFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<FsEntryDto>("fs/get", new { path, password = string.Empty }, cancellationToken)
            ?? throw new OpenListException(0, $"fs/get 返回空数据：{path}");

        if (string.IsNullOrWhiteSpace(data.RawUrl))
        {
            throw new OpenListException(0, $"fs/get 没有返回 raw_url：{path}");
        }

        return new OpenListFileInfo(data.Name, data.Size, data.IsDirectory, data.RawUrl!, data.Modified, data.Sign);
    }

    /// <summary>读文本文件（没有 /api/fs/read，只能 fs/get 拿直链再 GET）。</summary>
    public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var info = await GetFileAsync(path, cancellationToken);
        using var buffer = new MemoryStream();
        await DownloadAsync(info.RawUrl, buffer, null, cancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>下载到目标流；直链端点用真 HTTP 状态码。</summary>
    public async Task DownloadAsync(string rawUrl, Stream destination, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, rawUrl), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenListException((int)response.StatusCode, $"下载失败：HTTP {(int)response.StatusCode}");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        long written = 0;

        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
            progress?.Report(written);
        }
    }

    private async Task<TDto?> PostAsync<TDto>(string endpoint, object body, CancellationToken cancellationToken)
    {
        var url = _options.ApiUrl(endpoint);
        var json = JsonSerializer.Serialize(body);

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            },
            cancellationToken);

        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenListException((int)response.StatusCode, $"HTTP {(int)response.StatusCode}：{Truncate(text)}");
        }

        OpenListEnvelope<TDto>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<OpenListEnvelope<TDto>>(text, JsonOptions);
        }
        catch (JsonException)
        {
            throw new OpenListException(0, $"响应不是 OpenList JSON（可能是前端页面）：{Truncate(text)}");
        }

        if (envelope is null)
        {
            throw new OpenListException(0, "响应为空。");
        }

        if (envelope.Code != 200)
        {
            var message = string.IsNullOrWhiteSpace(envelope.Message) ? $"站点返回 code {envelope.Code}" : envelope.Message!;
            throw new OpenListException(envelope.Code, message);
        }

        return envelope.Data;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.RetryCount + 1);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var request = requestFactory();
                    var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if ((int)response.StatusCode >= 500 && attempt < attempts)
                    {
                        var delay = RetryDelay(attempt);
                        _log.Warning("站点 HTTP {Status}，{Delay}ms 后重试（{Attempt}/{Attempts}）",
                            (int)response.StatusCode, delay.TotalMilliseconds, attempt, attempts);
                        response.Dispose();
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    return response;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested && attempt < attempts)
                {
                    var delay = RetryDelay(attempt);
                    _log.Warning(ex, "请求失败，{Delay}ms 后重试（{Attempt}/{Attempts}）", delay.TotalMilliseconds, attempt, attempts);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static TimeSpan RetryDelay(int attempt) => TimeSpan.FromMilliseconds(300 * Math.Pow(3, attempt - 1));

    private static OpenListEntry ToEntry(FsEntryDto dto) =>
        new(dto.Name, dto.Size, dto.IsDirectory, dto.Modified, dto.Sign, dto.Type);

    private static string Truncate(string text) => text.Length <= 120 ? text : text[..120] + "…";
}
