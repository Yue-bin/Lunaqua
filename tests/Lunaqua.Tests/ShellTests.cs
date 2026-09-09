using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Lunaqua.Models;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Lunaqua.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lunaqua.Tests;

public sealed class ShellTests
{
    private static App App => (App)Application.Current!;

    [AvaloniaFact]
    public void 主窗口能出壳并渲染成帧()
    {
        var viewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.Equal(1180, frame!.PixelSize.Width);
        Assert.Equal(760, frame.PixelSize.Height);

        // 空白帧也是「非 null」，所以按颜色数判定真的画出了东西
        var colors = TestImages.CountDistinctColors(frame);
        Assert.True(colors > 100, $"疑似空白帧：只有 {colors} 种颜色");

        var screenshot = Path.Combine(TestPaths.Root, "shell.png");
        frame.Save(screenshot, PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(screenshot).Length > 0);
    }

    [AvaloniaFact]
    public void 导航能切换页面并更新选中态()
    {
        var viewModel = App.Services.GetRequiredService<MainWindowViewModel>();

        Assert.IsType<HomeViewModel>(viewModel.CurrentPage);

        viewModel.NavItems.Single(item => item.Key == "settings").SelectCommand.Execute(null);

        Assert.IsType<SettingsViewModel>(viewModel.CurrentPage);
        Assert.Equal("设置", viewModel.CurrentTitle);
        Assert.True(viewModel.NavItems.Single(item => item.Key == "settings").IsActive);
        Assert.False(viewModel.NavItems.Single(item => item.Key == "home").IsActive);
    }

    [AvaloniaFact]
    public void 代装向导页能渲染()
    {
        var main = App.Services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = main };
        window.Show();

        main.NavItems.Single(item => item.Key == "wizard").SelectCommand.Execute(null);
        Assert.IsType<WizardViewModel>(main.CurrentPage);

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(TestImages.CountDistinctColors(frame!) > 100, "向导页疑似空白帧");

        frame!.Save(Path.Combine(TestPaths.Root, "wizard.png"), PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(Path.Combine(TestPaths.Root, "wizard.png")).Length > 0);
    }

    [AvaloniaFact]
    public void 设置页改外观会落盘()
    {
        var file = NewSettingsFile("theme");
        var settings = new SettingsService(file);
        settings.Load();
        var viewModel = new SettingsViewModel(settings, new DialogService(), new CacheStore(Path.Combine(TestPaths.Root, "cache")), new FakeUpdateService());

        viewModel.SelectedTheme = viewModel.ThemeOptions.Single(option => option.Value == AppTheme.Dark);

        var reloaded = new SettingsService(file);
        reloaded.Load();
        Assert.Equal(AppTheme.Dark, reloaded.Current.Theme);
    }

    [AvaloniaFact]
    public void 设置页清除游戏目录会落盘()
    {
        var file = NewSettingsFile("gamedir");
        var settings = new SettingsService(file);
        settings.Load();
        settings.Update(s => s.GameDirectory = @"C:\Games\StickFight");
        var viewModel = new SettingsViewModel(settings, new DialogService(), new CacheStore(Path.Combine(TestPaths.Root, "cache")), new FakeUpdateService());

        Assert.True(viewModel.HasGameDirectory);

        viewModel.ClearGameDirectoryCommand.Execute(null);

        Assert.False(viewModel.HasGameDirectory);
        Assert.Equal("未设置", viewModel.GameDirectoryText);

        var reloaded = new SettingsService(file);
        reloaded.Load();
        Assert.Null(reloaded.Current.GameDirectory);
    }

    [AvaloniaFact]
    public void 图标库能画出字形()
    {
        var window = new Window
        {
            Width = 64,
            Height = 64,
            Content = new FluentIcon { Icon = Icon.Home, IconSize = IconSize.Size32 },
        };

        window.Show();
        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        var colors = TestImages.CountDistinctColors(frame!);
        Assert.True(colors > 1, $"FluentIcon 没有画出字形：只有 {colors} 种颜色");

        frame.Save(Path.Combine(TestPaths.Root, "icon.png"), PngBitmapEncoderOptions.Default);
    }

    private static string NewSettingsFile(string name)
    {
        var directory = Path.Combine(TestPaths.Root, name);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
