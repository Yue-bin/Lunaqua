using System.Runtime.CompilerServices;

namespace Lunaqua.Tests;

/// <summary>把应用数据目录指向临时目录，避免测试污染真实 %LocalAppData%\Lunaqua。</summary>
internal static class TestPaths
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "lunaqua-tests", Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Initialize()
    {
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("LUNAQUA_DATA_DIR", Root);
    }
}
