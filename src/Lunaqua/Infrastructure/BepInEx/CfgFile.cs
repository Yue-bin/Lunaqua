using System.Globalization;
using System.Text;
using Lunaqua.Domain;

namespace Lunaqua.Infrastructure.BepInEx;

/// <summary>
/// BepInEx <c>.cfg</c> 的行式解析/写回（规格书 §9）：
/// 保留注释、顺序、空行，写回只替换值行。
/// </summary>
public sealed class CfgFile
{
    private readonly List<string> _lines;
    private readonly List<CfgEntry> _entries;

    private CfgFile(string path, List<string> lines, List<CfgEntry> entries, string newLine, bool endsWithNewLine)
    {
        Path = path;
        _lines = lines;
        _entries = entries;
        NewLine = newLine;
        EndsWithNewLine = endsWithNewLine;
    }

    public string Path { get; }

    /// <summary><c>## Plugin GUID:</c> 里的 GUID（关联 mod 用）。</summary>
    public string? PluginGuid { get; private init; }

    /// <summary><c>## Settings file was created by …</c> 那一行。</summary>
    public string? PluginHeader { get; private init; }

    public IReadOnlyList<CfgEntry> Entries => _entries;

    public string NewLine { get; }

    public bool EndsWithNewLine { get; }

    public bool HasChanges => _entries.Any(entry => entry.IsChanged);

    public static CfgFile Load(string path) =>
        Parse(File.ReadAllText(path), path, DetectNewLine(File.ReadAllText(path)));

    public static CfgFile Parse(string text, string path = "", string? newLine = null)
    {
        var effectiveNewLine = newLine ?? DetectNewLine(text);
        var endsWithNewLine = text.EndsWith('\n');
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

        // 末尾换行会切出一个空串，去掉它（写回时按 EndsWithNewLine 补）
        if (endsWithNewLine && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var entries = new List<CfgEntry>();
        var section = string.Empty;
        string? pluginGuid = null;
        string? pluginHeader = null;
        var description = new List<string>();
        string? rawType = null;
        string? defaultValue = null;
        var acceptableValues = new List<string>();
        double? minimum = null;
        double? maximum = null;
        var allowsMultiple = false;

        void ResetBlock()
        {
            description.Clear();
            rawType = null;
            defaultValue = null;
            acceptableValues = [];
            minimum = null;
            maximum = null;
            allowsMultiple = false;
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                ResetBlock();
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                ResetBlock();
                continue;
            }

            if (trimmed.StartsWith("##", StringComparison.Ordinal))
            {
                var content = trimmed[2..].Trim();
                if (content.StartsWith("Plugin GUID:", StringComparison.OrdinalIgnoreCase))
                {
                    pluginGuid = content["Plugin GUID:".Length..].Trim();
                }
                else if (content.StartsWith("Settings file was created by", StringComparison.OrdinalIgnoreCase))
                {
                    pluginHeader = content;
                }
                else if (content.Length > 0)
                {
                    description.Add(content);
                }

                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                var content = trimmed[1..].Trim();
                if (content.StartsWith("Setting type:", StringComparison.OrdinalIgnoreCase))
                {
                    rawType = content["Setting type:".Length..].Trim();
                }
                else if (content.StartsWith("Default value:", StringComparison.OrdinalIgnoreCase))
                {
                    defaultValue = content["Default value:".Length..].Trim();
                }
                else if (content.StartsWith("Acceptable values:", StringComparison.OrdinalIgnoreCase))
                {
                    acceptableValues = [.. content["Acceptable values:".Length..]
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
                }
                else if (content.StartsWith("Acceptable value range:", StringComparison.OrdinalIgnoreCase))
                {
                    ParseRange(content["Acceptable value range:".Length..], ref minimum, ref maximum);
                }
                else if (content.Contains("Multiple values can be set", StringComparison.OrdinalIgnoreCase))
                {
                    allowsMultiple = true;
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();

                if (key.Length > 0)
                {
                    var type = rawType ?? "String";
                    entries.Add(new CfgEntry(
                        section,
                        key,
                        value,
                        description.Count > 0 ? string.Join(' ', description) : null,
                        type,
                        MapKind(type, acceptableValues, allowsMultiple),
                        defaultValue,
                        acceptableValues,
                        minimum,
                        maximum,
                        allowsMultiple,
                        index));
                }
            }

            ResetBlock();
        }

        return new CfgFile(path, lines, entries, effectiveNewLine, endsWithNewLine)
        {
            PluginGuid = pluginGuid,
            PluginHeader = pluginHeader,
        };
    }

    /// <summary>改一个条目的值（只动那一行）；返回是否真的改了。</summary>
    public bool SetValue(CfgEntry entry, string value)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(value);

        if (entry.ValueLineIndex < 0 || entry.ValueLineIndex >= _lines.Count)
        {
            return false;
        }

        if (string.Equals(entry.Value, value, StringComparison.Ordinal))
        {
            return false;
        }

        _lines[entry.ValueLineIndex] = $"{entry.Key} = {value}";
        entry.Value = value;
        return true;
    }

    /// <summary>恢复默认值。</summary>
    public bool Reset(CfgEntry entry) => SetValue(entry, entry.DefaultValue ?? string.Empty);

    public string Serialize()
    {
        var text = string.Join(NewLine, _lines);
        return EndsWithNewLine ? text + NewLine : text;
    }

    /// <summary>UTF-8 无 BOM 写回。</summary>
    public void Save(string? path = null)
    {
        var target = path ?? Path;
        File.WriteAllText(target, Serialize(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void ParseRange(string text, ref double? minimum, ref double? maximum)
    {
        // 形如 "From 0 to 100000" 或 "From -1.5 to 2.5"
        var parts = text.Split(" to ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var lowText = parts[0].StartsWith("From", StringComparison.OrdinalIgnoreCase)
            ? parts[0]["From".Length..].Trim()
            : parts[0];

        if (double.TryParse(lowText, NumberStyles.Float, CultureInfo.InvariantCulture, out var low))
        {
            minimum = low;
        }

        if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var high))
        {
            maximum = high;
        }
    }

    private static ConfigValueKind MapKind(string rawType, IReadOnlyList<string> acceptableValues, bool allowsMultiple)
    {
        if (allowsMultiple)
        {
            return ConfigValueKind.Flags;
        }

        return rawType switch
        {
            "Boolean" => ConfigValueKind.Boolean,
            "Int32" or "Int64" or "UInt32" or "UInt64" or "Int16" or "UInt16" or "Byte" or "SByte" or "IntPtr" =>
                ConfigValueKind.Integer,
            "Single" or "Float" => ConfigValueKind.Single,
            "Double" => ConfigValueKind.Double,
            "Decimal" => ConfigValueKind.Decimal,
            "String" => ConfigValueKind.String,
            "KeyboardShortcut" => ConfigValueKind.KeyboardShortcut,
            "Color" => ConfigValueKind.Color,
            _ => acceptableValues.Count > 0 ? ConfigValueKind.Enum : ConfigValueKind.Unknown,
        };
    }

    private static string DetectNewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
