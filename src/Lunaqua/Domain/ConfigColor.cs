using System.Globalization;

namespace Lunaqua.Domain;

/// <summary>BepInEx 的 Color 值：hex RGBA（如 <c>FF00FFFF</c>）。</summary>
public readonly record struct ConfigColor(byte R, byte G, byte B, byte A)
{
    /// <summary>解析 6 位 RGB 或 8 位 RGBA（带不带 # 都认）。</summary>
    public static bool TryParse(string? text, out ConfigColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim().TrimStart('#');
        if (value.Length is not (6 or 8))
        {
            return false;
        }

        if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        if (value.Length == 6)
        {
            color = new ConfigColor(
                (byte)((number >> 16) & 0xFF),
                (byte)((number >> 8) & 0xFF),
                (byte)(number & 0xFF),
                0xFF);
        }
        else
        {
            color = new ConfigColor(
                (byte)((number >> 24) & 0xFF),
                (byte)((number >> 16) & 0xFF),
                (byte)((number >> 8) & 0xFF),
                (byte)(number & 0xFF));
        }

        return true;
    }

    /// <summary>写成 BepInEx 的 8 位 hex RGBA（大写，无 #）。</summary>
    public string ToHex() => ((uint)R << 24 | (uint)G << 16 | (uint)B << 8 | A).ToString("X8", CultureInfo.InvariantCulture);
}
