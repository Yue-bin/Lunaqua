using Lunaqua.Services;
using Lunaqua.ViewModels;

namespace Lunaqua.Tests;

/// <summary>测试里拼 ViewModel 的小助手。</summary>
internal static class TestVms
{
    public static ConfigTabViewModel ConfigTab(ISettingsService settings, bool gameRunning = false)
    {
        var detector = new FakeProcessDetector { Running = gameRunning };
        return new ConfigTabViewModel(new ConfigEditorService(detector), detector, settings);
    }
}
