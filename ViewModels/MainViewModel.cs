using ReactiveUI;

namespace Lunaqua.ViewModels;

public class MainViewModel : ThemeViewModel
{
    private bool _isMenuOpen;

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => this.RaiseAndSetIfChanged(ref _isMenuOpen, value);
    }
}