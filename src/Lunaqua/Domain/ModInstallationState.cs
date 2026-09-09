namespace Lunaqua.Domain;

/// <summary>一个 mod 在本机的安装状态（凭证 + 磁盘实况）。</summary>
/// <param name="Installed">凭证；没装过为 null。</param>
/// <param name="Latest">站上最新版本。</param>
/// <param name="IgnoredVersion">玩家选择忽略的版本。</param>
public sealed record ModInstallationState(InstalledMod? Installed, SemVer Latest, string? IgnoredVersion)
{
    public bool IsInstalled => Installed is not null;

    public bool IsEnabled => Installed?.Enabled ?? false;

    public bool IsUnknownVersion => Installed?.Source == ModSource.UnknownVersion;

    public SemVer? InstalledVersion => Installed?.ParsedVersion;

    /// <summary>已装版本低于站上最新版（忽略过的版本不再提示）。</summary>
    public bool HasUpdate =>
        InstalledVersion is { } installed
        && installed < Latest
        && !string.Equals(IgnoredVersion, Latest.ToString(), StringComparison.Ordinal);

    public string StatusText
    {
        get
        {
            if (Installed is null)
            {
                return "未安装";
            }

            if (IsUnknownVersion)
            {
                return "未知版本 / 外部修改";
            }

            var state = IsEnabled ? "已装" : "已禁用";
            return HasUpdate
                ? $"{state} v{InstalledVersion} → 可更新 v{Latest}"
                : $"{state} v{InstalledVersion}";
        }
    }
}
