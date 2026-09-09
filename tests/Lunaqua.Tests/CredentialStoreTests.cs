using Lunaqua.Domain;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunaqua-cred", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 写读回环保持字段()
    {
        var store = new CredentialStore();
        var path = Path.Combine(_root, "demo", "info.json");
        var credential = new InstalledMod(
            1, "stick.plugins.demo", ModTypes.BepInEx, "stick.plugins.demo", "1.2.0",
            ModSource.Official, true,
            [new InstalledFile("demo.dll", new string('a', 64), 1234)],
            DateTimeOffset.Parse("2026-09-09T07:00:00+08:00"),
            "1.3.0");

        store.Write(path, credential);
        var loaded = store.Read(path);

        Assert.NotNull(loaded);
        Assert.Equal("1.2.0", loaded!.Version);
        Assert.Equal(ModSource.Official, loaded.Source);
        Assert.True(loaded.Enabled);
        Assert.Equal("1.3.0", loaded.IgnoredVersion);
        Assert.Equal("demo.dll", loaded.Files[0].Path);
    }

    [Fact]
    public void 能枚举三类位置的凭证()
    {
        var store = new CredentialStore();
        var game = new GameContext(_root);

        store.Write(Path.Combine(game.LunaquaDirectory, "a", "info.json"), Credential("a", ModTypes.BepInEx));
        store.Write(Path.Combine(game.OverlayModsDirectory, "b", "info.json"), Credential("b", ModTypes.Uvfs));
        store.Write(Path.Combine(game.OverlayDisabledDirectory, "c", "info.json"), Credential("c", ModTypes.Uvfs));

        var all = store.Enumerate(game);

        Assert.Equal(3, all.Count);
        Assert.Contains(all, item => item.Id == "a");
        Assert.Contains(all, item => item.Id == "b");
        Assert.Contains(all, item => item.Id == "c");
    }

    [Fact]
    public void 损坏的凭证读成null()
    {
        var store = new CredentialStore();
        var path = Path.Combine(_root, "bad", "info.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ 不是 JSON");

        Assert.Null(store.Read(path));
    }

    private static InstalledMod Credential(string id, string type) => new(
        1, id, type, null, "1.0.0", ModSource.Official, true,
        [new InstalledFile("x.dll", new string('b', 64), 10)], DateTimeOffset.Now);
}
