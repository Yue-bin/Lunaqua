namespace Lunaqua.Infrastructure.OpenList;

/// <summary>
/// OpenList 的业务错误：<c>/api/*</c> 恒返回 HTTP 200，错误在 JSON 的 <c>code</c> 字段。
/// </summary>
public sealed class OpenListException : Exception
{
    public OpenListException(int code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>业务错误码；0 表示响应不是 OpenList 的 JSON envelope。</summary>
    public int Code { get; }

    /// <summary>资源不存在（fs/get 对不存在的路径返回 500，部分部署返回 404）。</summary>
    public bool IsNotFound => Code is 404 or 500;
}
