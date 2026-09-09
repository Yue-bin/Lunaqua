using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

/// <summary>配置编辑器里的一条（按类型生成对应控件）。</summary>
public sealed partial class ConfigEntryViewModel : ObservableObject
{
    private readonly CfgFile _file;
    private readonly ConfigEditorService _service;
    private readonly GameContext _game;
    private readonly bool _readOnly;
    private bool _loading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private decimal _numberValue;

    [ObservableProperty]
    private string? _selectedOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    private string _currentValue = string.Empty;

    public ConfigEntryViewModel(
        CfgFile file,
        CfgEntry entry,
        ConfigEditorService service,
        GameContext game,
        bool readOnly)
    {
        _file = file;
        _service = service;
        _game = game;
        _readOnly = readOnly;
        Entry = entry;

        _loading = true;
        CurrentValue = entry.Value;
        Text = entry.Value;

        switch (entry.Kind)
        {
            case ConfigValueKind.Boolean:
                _boolValue = string.Equals(entry.Value, "true", StringComparison.OrdinalIgnoreCase);
                break;
            case ConfigValueKind.Integer:
            case ConfigValueKind.Single:
            case ConfigValueKind.Double:
            case ConfigValueKind.Decimal:
                _numberValue = decimal.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0m;
                break;
            case ConfigValueKind.Enum:
                _selectedOption = entry.AcceptableValues.FirstOrDefault(option =>
                    string.Equals(option, entry.Value, StringComparison.Ordinal));
                break;
        }

        _loading = false;
    }

    public CfgEntry Entry { get; }

    public string Section => Entry.Section;

    public string Key => Entry.Key;

    public string Description => Entry.DisplayName;

    public string TypeText => Entry.RawType;

    public bool IsChanged => !string.Equals(CurrentValue, Entry.DefaultValue ?? string.Empty, StringComparison.Ordinal);

    public bool IsBoolean => Entry.Kind == ConfigValueKind.Boolean;

    public bool IsNumber => Entry.Kind is ConfigValueKind.Integer or ConfigValueKind.Single or ConfigValueKind.Double or ConfigValueKind.Decimal;

    public bool IsEnum => Entry.Kind == ConfigValueKind.Enum;

    public bool IsText => !IsBoolean && !IsNumber && !IsEnum;

    public bool IsReadOnly => _readOnly;

    public bool IsEditable => !_readOnly;

    public IReadOnlyList<string> Options => Entry.AcceptableValues;

    public decimal? Minimum => Entry.Minimum is { } min ? (decimal)min : null;

    public decimal? Maximum => Entry.Maximum is { } max ? (decimal)max : null;

    public bool HasRange => Minimum is not null || Maximum is not null;

    public string RangeHint => HasRange
        ? $"范围 {Minimum?.ToString(CultureInfo.InvariantCulture) ?? "—"} ~ {Maximum?.ToString(CultureInfo.InvariantCulture) ?? "—"}"
        : string.Empty;

    public string Placeholder => Entry.Kind switch
    {
        ConfigValueKind.KeyboardShortcut => "例如 LeftControl + K，留空 = Not set",
        ConfigValueKind.Color => "8 位 hex RGBA，例如 FFD700FF",
        ConfigValueKind.Flags => "多个值用逗号分隔",
        _ => string.Empty,
    };

    /// <summary>文本框：失焦或回车时提交。</summary>
    [RelayCommand]
    public void CommitText() => Apply(Text);

    [RelayCommand]
    public void Reset()
    {
        var value = Entry.DefaultValue ?? string.Empty;
        Text = value;
        Apply(value);
    }

    partial void OnBoolValueChanged(bool value)
    {
        if (!_loading)
        {
            Apply(value ? "true" : "false");
        }
    }

    partial void OnNumberValueChanged(decimal value)
    {
        if (!_loading)
        {
            Apply(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    partial void OnSelectedOptionChanged(string? value)
    {
        if (!_loading && value is not null)
        {
            Apply(value);
        }
    }

    private void Apply(string rawValue)
    {
        if (_readOnly)
        {
            return;
        }

        var result = ConfigValueValidator.Validate(Entry, rawValue);
        if (!result.Ok)
        {
            HasError = true;
            ErrorText = result.Error;
            return;
        }

        HasError = false;
        ErrorText = null;

        if (_file.SetValue(Entry, result.Value))
        {
            try
            {
                _service.Save(_game, _file);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorText = ex.Message;
                return;
            }
        }

        _loading = true;
        CurrentValue = Entry.Value;
        Text = Entry.Value;

        switch (Entry.Kind)
        {
            case ConfigValueKind.Boolean:
                BoolValue = string.Equals(Entry.Value, "true", StringComparison.OrdinalIgnoreCase);
                break;
            case ConfigValueKind.Enum:
                SelectedOption = Entry.Value;
                break;
            case ConfigValueKind.Integer:
            case ConfigValueKind.Single:
            case ConfigValueKind.Double:
            case ConfigValueKind.Decimal:
                if (decimal.TryParse(Entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    NumberValue = number;
                }

                break;
        }

        _loading = false;
    }
}
