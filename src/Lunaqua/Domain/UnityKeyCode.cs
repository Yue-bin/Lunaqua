namespace Lunaqua.Domain;

/// <summary>
/// Unity 5.6.x 的 KeyCode 名称表（规格书 §9：快捷键用 Unity 键名，不是 WPF/Avalonia 的）。
/// 游戏实测 Unity 5.6.7。
/// </summary>
public static class UnityKeyCode
{
    /// <summary>未设置快捷键时 BepInEx 写出的字面量。</summary>
    public const string NotSet = "Not set";

    private static readonly List<string> ModifierOrder =
    [
        "LeftControl", "LeftShift", "LeftAlt",
        "RightControl", "RightShift", "RightAlt",
    ];

    /// <summary>修饰键（顺序固定，格式化时按此顺序排）。</summary>
    public static IReadOnlyList<string> Modifiers => ModifierOrder;

    /// <summary>可选的按键名（Unity 5.6.x KeyCode）。</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "Alpha0", "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9",
        "Keypad0", "Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5", "Keypad6", "Keypad7", "Keypad8", "Keypad9",
        "KeypadPeriod", "KeypadDivide", "KeypadMultiply", "KeypadMinus", "KeypadPlus", "KeypadEnter", "KeypadEquals",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "F13", "F14", "F15",
        "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        "Backspace", "Delete", "Tab", "Clear", "Return", "Pause", "Escape", "Space",
        "Insert", "Home", "End", "PageUp", "PageDown",
        "Numlock", "CapsLock", "ScrollLock", "Print", "SysReq", "Break", "Menu", "Help",
        "Mouse0", "Mouse1", "Mouse2", "Mouse3", "Mouse4", "Mouse5", "Mouse6",
    ];

    public static bool IsModifier(string name) => Modifiers.Contains(name, StringComparer.Ordinal);

    public static bool IsKnown(string name) =>
        IsModifier(name) || Keys.Contains(name, StringComparer.Ordinal);

    /// <summary>把 <c>LeftControl + K</c> 拆成修饰键与主键；空/Not set 返回空。</summary>
    public static bool TryParse(string? text, out IReadOnlyList<string> modifiers, out string? key)
    {
        modifiers = [];
        key = null;

        if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), NotSet, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        // 修饰键可以出现在任意位置（手写时顺序常乱），非修饰键只能有一个且是主键
        var mods = new List<string>();
        foreach (var part in parts)
        {
            if (IsModifier(part))
            {
                mods.Add(part);
                continue;
            }

            if (key is not null)
            {
                return false;
            }

            key = part;
        }

        if (key is null)
        {
            return false;
        }

        modifiers = [.. mods.OrderBy(ModifierOrder.IndexOf)];
        return IsKnown(key);
    }

    /// <summary>格式化成 BepInEx 的写法（修饰键按固定顺序，未设置写 Not set）。</summary>
    public static string Format(IEnumerable<string> modifiers, string? key)
    {
        var mods = modifiers.Where(IsModifier).OrderBy(ModifierOrder.IndexOf).ToList();
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotSet;
        }

        return mods.Count == 0 ? key! : string.Join(" + ", mods) + " + " + key;
    }
}
