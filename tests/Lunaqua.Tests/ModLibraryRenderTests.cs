using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Lunaqua.ViewModels;
using Lunaqua.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>用真实站点数据渲染 Mod 库页面（站点不可达时跳过）。</summary>
public sealed class ModLibraryRenderTests
{
    [AvaloniaFact]
    public async Task Mod库页面能渲染真实站点数据()
    {
        var app = (App)Application.Current!;
        var library = app.Services.GetRequiredService<ModLibraryViewModel>();
        var main = app.Services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = main };
        window.Show();

        try
        {
            await library.ActivateAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Skip($"站点不可达，跳过渲染联调：{ex.Message}");
            return;
        }

        Assert.True(library.Items.Count >= 9, $"Mod 库只列出 {library.Items.Count} 个 mod");
        Assert.False(library.HasError, library.ErrorText);

        main.NavItems.Single(item => item.Key == "mods").SelectCommand.Execute(null);
        Assert.IsType<ModLibraryViewModel>(main.CurrentPage);

        library.SelectedItem = library.Items.First(item => item.Id == "stick.plugins.playermanager");
        await WaitUntilAsync(() => library.Detail.HasSelection && !library.Detail.IsLoadingReadme);

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var colors = TestImages.CountDistinctColors(frame!);
        Assert.True(colors > 100, $"疑似空白帧：只有 {colors} 种颜色");

        frame!.Save(Path.Combine(TestPaths.Root, "library.png"), PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(Path.Combine(TestPaths.Root, "library.png")).Length > 0);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "等待超时");
    }
}
