using Lunaqua.Domain.Strategies;

namespace Lunaqua.Domain;

/// <summary>类型 → 策略的查表（规格书 §6.1）。</summary>
public sealed class ModTypeStrategies
{
    private readonly Dictionary<string, IModTypeStrategy> _strategies;

    public ModTypeStrategies(IEnumerable<IModTypeStrategy>? strategies = null)
    {
        var all = strategies?.ToList()
            ?? [new BepInExModStrategy(), new UvfsModStrategy(), new UvfsFrameworkModStrategy()];

        _strategies = all.ToDictionary(strategy => strategy.Type, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IModTypeStrategy> All => _strategies.Values;

    public IModTypeStrategy? Find(string type) =>
        _strategies.TryGetValue(type, out var strategy) ? strategy : null;

    public IModTypeStrategy Get(string type) =>
        Find(type) ?? throw new NotSupportedException($"不支持的 mod 类型：{type}");
}
