using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>本机已装 mod 的查询与状态合并（磁盘实况优先）。</summary>
public sealed class InstalledModService
{
    private readonly CredentialStore _credentials;
    private readonly InstallEngine _engine;
    private readonly ILogger _log;

    public InstalledModService(CredentialStore credentials, InstallEngine engine, ILogger? log = null)
    {
        _credentials = credentials;
        _engine = engine;
        _log = log ?? Log.ForContext<InstalledModService>();
    }

    /// <summary>列出全部已装 mod；凭证里的 Enabled 以磁盘实况修正。</summary>
    public IReadOnlyList<InstalledMod> List(GameContext game)
    {
        var installed = _credentials.Enumerate(game);
        var result = new List<InstalledMod>(installed.Count);

        foreach (var credential in installed)
        {
            var enabled = _engine.DetectEnabled(game, credential);
            result.Add(enabled == credential.Enabled ? credential : credential with { Enabled = enabled });
        }

        return result;
    }

    public InstalledMod? Find(GameContext game, string modId) =>
        List(game).FirstOrDefault(item => string.Equals(item.Id, modId, StringComparison.Ordinal));

    /// <summary>算出某个站上 mod 的安装状态。</summary>
    public ModInstallationState GetState(GameContext game, CatalogEntry entry)
    {
        var installed = Find(game, entry.Info.Id);
        return new ModInstallationState(installed, entry.Info.Latest, installed?.IgnoredVersion);
    }
}
