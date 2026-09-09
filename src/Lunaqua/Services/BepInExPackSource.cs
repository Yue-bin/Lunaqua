using System.IO.Compression;
using System.Security.Cryptography;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Serilog;

namespace Lunaqua.Services;

/// <summary>取一份已解包的 BepInEx pack。</summary>
public interface IBepInExPackSource
{
    /// <summary>返回包含 pack 全部文件的目录；失败抛异常。</summary>
    Task<string> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>从站点下载 zip 再解包（生产用）。</summary>
public sealed class SiteBepInExPackSource : IBepInExPackSource
{
    private readonly OpenListClient _client;
    private readonly CacheStore _cache;
    private readonly BepInExPackManifest _manifest;
    private readonly string _workRoot;
    private readonly ILogger _log;

    public SiteBepInExPackSource(
        OpenListClient client,
        CacheStore cache,
        BepInExPackManifest manifest,
        string? workRoot = null,
        ILogger? log = null)
    {
        _client = client;
        _cache = cache;
        _manifest = manifest;
        _workRoot = workRoot ?? Path.Combine(AppPaths.Cache, "bepinex-pack");
        _log = log ?? Log.ForContext<SiteBepInExPackSource>();
    }

    public async Task<string> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_manifest.SourceZipSize <= 0 || string.IsNullOrWhiteSpace(_manifest.SourceZipSha256))
        {
            throw new InvalidOperationException("BepInEx pack 清单不完整，没法下载。");
        }

        // 1. zip 落到缓存（按清单里的 sha256 校验）
        string zipPath;
        if (_cache.TryGet(_manifest.SourceZipSha256, _manifest.SourceZipSize, out var cached))
        {
            zipPath = cached;
        }
        else
        {
            var info = await _client.GetFileAsync(_manifest.SourceZip, cancellationToken);
            zipPath = await _cache.StoreAsync(
                _manifest.SourceZipSha256,
                _manifest.SourceZipSize,
                (stream, token) => _client.DownloadAsync(info.RawUrl, stream, null, token),
                cancellationToken);
        }

        // 2. 解包到工作目录
        var target = Path.Combine(_workRoot, _manifest.Version);
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.CreateDirectory(target);
        ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
        _log.Information("BepInEx pack 已解包：{Target}", target);

        // 3. 逐文件自校验
        var errors = VerifyPack(_manifest, target);
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"BepInEx pack 校验失败：{string.Join("；", errors)}");
        }

        return target;
    }

    /// <summary>校验一个解包目录里的文件是否与清单一致；返回问题清单（空 = 通过）。</summary>
    public static IReadOnlyList<string> VerifyPack(BepInExPackManifest manifest, string directory)
    {
        var errors = new List<string>();

        foreach (var file in manifest.Files)
        {
            var full = Path.Combine(directory, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                errors.Add($"缺少文件 {file.Path}");
                continue;
            }

            var info = new FileInfo(full);
            if (info.Length != file.Size)
            {
                errors.Add($"{file.Path} 大小不符（期望 {file.Size}，实际 {info.Length}）");
                continue;
            }

            using var stream = File.OpenRead(full);
            var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{file.Path} sha256 不符");
            }
        }

        return errors;
    }
}
