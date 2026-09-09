using System.Security.Cryptography;
using Serilog;

namespace Lunaqua.Services;

/// <summary>按 sha256 复用下载的文件（规格书 §7.5）。</summary>
public sealed class CacheStore
{
    private readonly ILogger _log;

    public CacheStore(string? root = null, ILogger? log = null)
    {
        Root = root ?? AppPaths.Cache;
        _log = log ?? Log.ForContext<CacheStore>();
    }

    public string Root { get; }

    /// <summary>缓存里是否已有这个文件（大小对得上才算命中）。</summary>
    public bool TryGet(string sha256, long size, out string path)
    {
        path = Path.Combine(Root, sha256);
        if (!File.Exists(path))
        {
            return false;
        }

        if (size > 0 && new FileInfo(path).Length != size)
        {
            _log.Warning("缓存大小对不上，丢弃：{Path}", path);
            File.Delete(path);
            return false;
        }

        return true;
    }

    /// <summary>把流写进缓存并校验哈希；校验失败会删掉临时文件。</summary>
    public Task<string> StoreAsync(Stream source, string sha256, long expectedSize, CancellationToken cancellationToken = default) =>
        StoreAsync(sha256, expectedSize, (destination, token) => source.CopyToAsync(destination, token), cancellationToken);

    /// <summary>由调用方负责写内容（下载大文件不必过内存）。</summary>
    public async Task<string> StoreAsync(
        string sha256,
        long expectedSize,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        var tempPath = Path.Combine(Root, $"{sha256}.tmp");
        var finalPath = Path.Combine(Root, sha256);

        try
        {
            await using (var destination = File.Create(tempPath))
            {
                await writer(destination, cancellationToken);
            }

            var actualSize = new FileInfo(tempPath).Length;
            if (expectedSize > 0 && actualSize != expectedSize)
            {
                throw new InvalidDataException($"文件大小不符：期望 {expectedSize}，实际 {actualSize}");
            }

            var actualHash = await ComputeSha256Async(tempPath, cancellationToken);
            if (!string.Equals(actualHash, sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"sha256 不符：期望 {sha256}，实际 {actualHash}");
            }

            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public long GetTotalSize()
    {
        if (!Directory.Exists(Root))
        {
            return 0;
        }

        return Directory.EnumerateFiles(Root).Sum(file => new FileInfo(file).Length);
    }

    /// <summary>清空缓存，返回删掉的字节数。</summary>
    public long Clear()
    {
        if (!Directory.Exists(Root))
        {
            return 0;
        }

        long freed = 0;
        foreach (var file in Directory.EnumerateFiles(Root))
        {
            var info = new FileInfo(file);
            freed += info.Length;
            info.Delete();
        }

        _log.Information("缓存已清理，释放 {Bytes} 字节", freed);
        return freed;
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await ComputeSha256Async(stream, cancellationToken);
    }

    public static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}
