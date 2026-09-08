using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Lunaqua.Tests.TestAppBuilder))]

namespace Lunaqua.Tests;

/// <summary>无头测试用的 Avalonia 应用构建器（真实绘制，可截图）。</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia();
}
