using System.Collections.Concurrent;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Serilog;

namespace Lunaqua.Services;

/// <summary>
/// 站点目录仓储：把「目录 + info.json」变成可用的 mod 目录（规格书 §6.2）。
/// 本站没有搜索索引，搜索在内存里对已缓存的目录做（规格书 §5.2）。
/// </summary>
public sealed class ModRepository
{
    private readonly OpenListClient _client;
    private readonly ILogger _log;

    public ModRepository(OpenListClient client, ILogger? log = null)
    {
        _client = client;
        _log = log ?? Log.ForContext<ModRepository>();
    }

    /// <summary>最近一次扫描结果（供搜索/详情复用）。</summary>
    public ModCatalog Catalog { get; private set; } = ModCatalog.Empty;

    /// <summary>扫描站点根目录，逐个目录取 info.json 解析。</summary>
    public async Task<ModCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        var root = await _client.ListAsync("/", cancellationToken: cancellationToken);
        var directories = root.Entries
            .Where(entry => entry.IsDirectory && !entry.Name.StartsWith('.'))
            .Select(entry => entry.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        _log.Information("站点根目录 {Count} 个目录，开始读取 info.json", directories.Count);

        var entries = new ConcurrentBag<CatalogEntry>();
        var issues = new ConcurrentBag<CatalogIssue>();

        await Parallel.ForEachAsync(
            directories,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _client.Options.MaxConcurrency), CancellationToken = cancellationToken },
            async (name, token) =>
            {
                var (entry, issue) = await LoadOneAsync(name, token);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
                else if (issue is not null)
                {
                    issues.Add(issue);
                }
            });

        Catalog = new ModCatalog(
            [.. entries.OrderBy(entry => entry.Info.Name, StringComparer.CurrentCulture)],
            [.. issues.OrderBy(issue => issue.DirectoryName, StringComparer.Ordinal)],
            DateTimeOffset.Now);

        _log.Information("目录扫描完成：{Mods} 个 mod，{Issues} 个目录有问题", Catalog.ModCount, Catalog.Issues.Count);
        return Catalog;
    }

    /// <summary>读取某个 mod 目录的 readme.md；没有就返回 null。</summary>
    public async Task<string?> ReadReadmeAsync(string directoryName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.ReadTextAsync($"/{directoryName}/readme.md", cancellationToken);
        }
        catch (OpenListException ex) when (ex.IsNotFound)
        {
            return null;
        }
    }

    private async Task<(CatalogEntry? Entry, CatalogIssue? Issue)> LoadOneAsync(string directoryName, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await _client.ReadTextAsync($"/{directoryName}/info.json", cancellationToken);
        }
        catch (OpenListException ex) when (ex.IsNotFound)
        {
            // 没有 info.json 的目录不是可管理 mod（规格书 §3）
            _log.Debug("目录 {Directory} 没有 info.json，跳过", directoryName);
            return (null, null);
        }
        catch (OpenListException ex)
        {
            return (null, new CatalogIssue(directoryName, [$"读取 info.json 失败：{ex.Message}"]));
        }

        var result = MetadataParser.Parse(json, directoryName);
        if (!result.IsValid)
        {
            _log.Warning("目录 {Directory} 的 info.json 校验失败：{Errors}", directoryName, string.Join("；", result.Errors));
            return (null, new CatalogIssue(directoryName, result.Errors));
        }

        foreach (var warning in result.Warnings)
        {
            _log.Debug("目录 {Directory} 的 info.json 警告：{Warning}", directoryName, warning);
        }

        return (new CatalogEntry(result.Info!, directoryName, result.Warnings), null);
    }
}
