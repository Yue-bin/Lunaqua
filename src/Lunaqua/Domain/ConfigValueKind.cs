namespace Lunaqua.Domain;

/// <summary>.cfg 条目的值类型（规格书 §9 控件映射）。</summary>
public enum ConfigValueKind
{
    /// <summary>不认识的类型 → 文本框 + 校验。</summary>
    Unknown,

    Boolean,

    /// <summary>整型（Int32 / Int64 / …）。</summary>
    Integer,

    /// <summary>单精度浮点（Single）。</summary>
    Single,

    Double,

    Decimal,

    String,

    /// <summary>枚举（有 Acceptable values）。</summary>
    Enum,

    /// <summary>多值（逗号分隔）。</summary>
    Flags,

    /// <summary>快捷键（如 <c>LeftControl + K</c>）。</summary>
    KeyboardShortcut,

    /// <summary>颜色（hex RGBA）。</summary>
    Color,
}
