using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lunaqua.ViewModels;

namespace Lunaqua.Views;

public partial class ModDetailView : UserControl
{
    public ModDetailView() => InitializeComponent();

    private void OnEntryTextLostFocus(object? sender, RoutedEventArgs e) => Commit(sender);

    private void OnEntryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit(sender);
            e.Handled = true;
        }
    }

    private static void Commit(object? sender)
    {
        if (sender is Control { DataContext: ConfigEntryViewModel entry })
        {
            entry.CommitTextCommand.Execute(null);
        }
    }
}
