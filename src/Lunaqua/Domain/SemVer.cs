using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Lunaqua.Domain;

/// <summary>SemVer 2.0.0 版本号（支持 <c>-rc.1</c> 预发布与 <c>+build</c> 元数据）。</summary>
public sealed class SemVer : IComparable<SemVer>, IEquatable<SemVer>
{
    private SemVer(int major, int minor, int patch, string prerelease, string buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        BuildMetadata = buildMetadata;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>预发布标识（不含前导 <c>-</c>）；没有则为空串。</summary>
    public string Prerelease { get; }

    /// <summary>构建元数据（不含前导 <c>+</c>）；没有则为空串。</summary>
    public string BuildMetadata { get; }

    public bool IsPrerelease => Prerelease.Length > 0;

    public static bool TryParse(string? text, [NotNullWhen(true)] out SemVer? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();

        var buildMetadata = string.Empty;
        var hasBuildMetadata = false;
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            buildMetadata = value[(plus + 1)..];
            value = value[..plus];
            hasBuildMetadata = true;
        }

        var prerelease = string.Empty;
        var hasPrerelease = false;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = value[(dash + 1)..];
            value = value[..dash];
            hasPrerelease = true;
        }

        var numbers = value.Split('.');
        if (numbers.Length != 3
            || !TryParseNumber(numbers[0], out var major)
            || !TryParseNumber(numbers[1], out var minor)
            || !TryParseNumber(numbers[2], out var patch)
            || (hasPrerelease && !IsValidIdentifiers(prerelease, forbidLeadingZero: true))
            || (hasBuildMetadata && !IsValidIdentifiers(buildMetadata, forbidLeadingZero: false)))
        {
            return false;
        }

        version = new SemVer(major, minor, patch, prerelease, buildMetadata);
        return true;
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        return result != 0 ? result : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public bool Equals(SemVer? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemVer other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease);

    public override string ToString() => BuildMetadata.Length > 0
        ? $"{Major}.{Minor}.{Patch}{(Prerelease.Length > 0 ? "-" + Prerelease : string.Empty)}+{BuildMetadata}"
        : $"{Major}.{Minor}.{Patch}{(Prerelease.Length > 0 ? "-" + Prerelease : string.Empty)}";

    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;

    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    private static bool TryParseNumber(string text, out int number)
    {
        number = 0;
        if (text.Length == 0 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool IsValidIdentifiers(string text, bool forbidLeadingZero)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            var numeric = true;
            foreach (var c in identifier)
            {
                if (!(char.IsAsciiLetterOrDigit(c) || c == '-'))
                {
                    return false;
                }

                if (!char.IsAsciiDigit(c))
                {
                    numeric = false;
                }
            }

            if (forbidLeadingZero && numeric && identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static int ComparePrerelease(string left, string right)
    {
        if (left == right)
        {
            return 0;
        }

        // 有预发布 < 无预发布
        if (left.Length == 0)
        {
            return 1;
        }

        if (right.Length == 0)
        {
            return -1;
        }

        var a = left.Split('.');
        var b = right.Split('.');

        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            if (i >= a.Length)
            {
                return -1;
            }

            if (i >= b.Length)
            {
                return 1;
            }

            var x = a[i];
            var y = b[i];
            var xNumeric = x.All(char.IsAsciiDigit);
            var yNumeric = y.All(char.IsAsciiDigit);

            int result;
            if (xNumeric && yNumeric)
            {
                result = long.Parse(x, CultureInfo.InvariantCulture).CompareTo(long.Parse(y, CultureInfo.InvariantCulture));
            }
            else if (xNumeric)
            {
                return -1;   // 数字标识 < 字母标识
            }
            else if (yNumeric)
            {
                return 1;
            }
            else
            {
                result = string.CompareOrdinal(x, y);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }
}
