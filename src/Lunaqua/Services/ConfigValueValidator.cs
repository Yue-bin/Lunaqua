using System.Globalization;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;

namespace Lunaqua.Services;

/// <summary>校验结果。</summary>
/// <param name="Ok">是否合法。</param>
/// <param name="Value">规范化后的值（写回用）。</param>
/// <param name="Error">不合法时的中文提示。</param>
public sealed record ConfigValidationResult(bool Ok, string Value, string? Error)
{
    public static ConfigValidationResult Success(string value) => new(true, value, null);

    public static ConfigValidationResult Failure(string value, string error) => new(false, value, error);
}

/// <summary>按类型校验/规范化 .cfg 的值（规格书 §9：非法值拒绝写入并就地提示）。</summary>
public static class ConfigValueValidator
{
    public static ConfigValidationResult Validate(CfgEntry entry, string? rawValue)
    {
        var value = rawValue?.Trim() ?? string.Empty;

        switch (entry.Kind)
        {
            case ConfigValueKind.Boolean:
                if (bool.TryParse(value, out var boolean))
                {
                    return ConfigValidationResult.Success(boolean ? "true" : "false");
                }

                return ConfigValidationResult.Failure(value, "只能是 true 或 false。");

            case ConfigValueKind.Integer:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return ConfigValidationResult.Failure(value, "要填整数。");
                }

                return CheckRange(entry, integer, value);

            case ConfigValueKind.Single:
            case ConfigValueKind.Double:
            case ConfigValueKind.Decimal:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return ConfigValidationResult.Failure(value, "要填数字（小数点用 .）。");
                }

                return CheckRange(entry, number, value);

            case ConfigValueKind.Enum:
                if (entry.AcceptableValues.Count == 0 || entry.AcceptableValues.Contains(value, StringComparer.Ordinal))
                {
                    return ConfigValidationResult.Success(value);
                }

                return ConfigValidationResult.Failure(value, $"只能是：{string.Join("、", entry.AcceptableValues)}");

            case ConfigValueKind.Flags:
                var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var unknown = parts.Where(part => !entry.AcceptableValues.Contains(part, StringComparer.Ordinal)).ToList();
                if (unknown.Count > 0)
                {
                    return ConfigValidationResult.Failure(value, $"不认识：{string.Join("、", unknown)}（可选：{string.Join("、", entry.AcceptableValues)}）");
                }

                return ConfigValidationResult.Success(string.Join(", ", parts));

            case ConfigValueKind.KeyboardShortcut:
                return UnityKeyCode.TryParse(value, out var modifiers, out var key)
                    ? ConfigValidationResult.Success(UnityKeyCode.Format(modifiers, key))
                    : ConfigValidationResult.Failure(value, "按键名要按 Unity 的写法，例如 LeftControl + K 或 F8。");

            case ConfigValueKind.Color:
                return ConfigColor.TryParse(value, out var color)
                    ? ConfigValidationResult.Success(color.ToHex())
                    : ConfigValidationResult.Failure(value, "颜色要填 8 位 hex RGBA，例如 FFD700FF。");

            default:
                return ConfigValidationResult.Success(value);
        }
    }

    private static ConfigValidationResult CheckRange(CfgEntry entry, double value, string original)
    {
        if (entry.Minimum is { } min && value < min)
        {
            return ConfigValidationResult.Failure(original, $"不能小于 {min}。");
        }

        if (entry.Maximum is { } max && value > max)
        {
            return ConfigValidationResult.Failure(original, $"不能大于 {max}。");
        }

        return ConfigValidationResult.Success(original);
    }
}
