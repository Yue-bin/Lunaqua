using Lunaqua.Domain;

namespace Lunaqua.Infrastructure.BepInEx;

/// <summary>.cfg 里的一条配置（值行 + 它上方的注释块）。</summary>
public sealed class CfgEntry
{
    public CfgEntry(
        string section,
        string key,
        string value,
        string? description,
        string rawType,
        ConfigValueKind kind,
        string? defaultValue,
        IReadOnlyList<string> acceptableValues,
        double? minimum,
        double? maximum,
        bool allowsMultiple,
        int valueLineIndex)
    {
        Section = section;
        Key = key;
        Value = value;
        Description = description;
        RawType = rawType;
        Kind = kind;
        DefaultValue = defaultValue;
        AcceptableValues = acceptableValues;
        Minimum = minimum;
        Maximum = maximum;
        AllowsMultiple = allowsMultiple;
        ValueLineIndex = valueLineIndex;
    }

    public string Section { get; }

    public string Key { get; }

    /// <summary>当前值（写回时只改这一行的值）。</summary>
    public string Value { get; internal set; }

    /// <summary><c>## …</c> 描述（可能是中文）。</summary>
    public string? Description { get; }

    /// <summary>原始类型名（Boolean / Int32 / Single / KeyboardShortcut / …）。</summary>
    public string RawType { get; }

    public ConfigValueKind Kind { get; }

    public string? DefaultValue { get; }

    public IReadOnlyList<string> AcceptableValues { get; }

    public double? Minimum { get; }

    public double? Maximum { get; }

    /// <summary>文件里写了「Multiple values can be set…」→ 多值。</summary>
    public bool AllowsMultiple { get; }

    /// <summary>值行在文件里的行号（0 起）。</summary>
    public int ValueLineIndex { get; internal set; }

    /// <summary>和默认值不一样（界面「只看已改动项」用）。</summary>
    public bool IsChanged => !string.Equals(Value, DefaultValue ?? string.Empty, StringComparison.Ordinal);

    public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Key : Description!;

    public override string ToString() => $"[{Section}] {Key} = {Value}";
}
