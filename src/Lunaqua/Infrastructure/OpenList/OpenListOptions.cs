namespace Lunaqua.Infrastructure.OpenList;

/// <summary>OpenList 站点连接参数。</summary>
public sealed class OpenListOptions
{
    /// <summary>默认站点（站长自运营）。</summary>
    public const string DefaultBaseUrl = "https://blog.monblog.top/openlist";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>列目录默认每页条数（必须显式传，服务端默认 30）。</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>并发请求上限（服务端有限流中间件，规格书 §5.2）。</summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>失败重试次数（指数退避）。</summary>
    public int RetryCount { get; set; } = 2;

    public string ApiUrl(string endpoint) => $"{BaseUrl.TrimEnd('/')}/api/{endpoint}";
}
