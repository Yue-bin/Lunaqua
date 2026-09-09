using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class CacheStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunaqua-cache", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task 存入缓存后能按哈希命中()
    {
        var content = TestMod.Bytes("缓存内容");
        var sha = TestMod.Sha256(content);
        var store = new CacheStore(_root);

        var path = await store.StoreAsync(new MemoryStream(content), sha, content.Length, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(path));
        Assert.True(store.TryGet(sha, content.Length, out var hit));
        Assert.Equal(path, hit);
    }

    [Fact]
    public async Task 哈希不符时抛错且不留下缓存()
    {
        var store = new CacheStore(_root);
        var content = TestMod.Bytes("内容");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.StoreAsync(
            new MemoryStream(content),
            new string('a', 64),
            content.Length,
            TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task 大小对不上的缓存会被丢弃()
    {
        var content = TestMod.Bytes("内容");
        var sha = TestMod.Sha256(content);
        var store = new CacheStore(_root);
        await store.StoreAsync(new MemoryStream(content), sha, content.Length, TestContext.Current.CancellationToken);

        Assert.False(store.TryGet(sha, content.Length + 1, out _));
    }

    [Fact]
    public void 清理缓存返回释放的字节数()
    {
        var store = new CacheStore(_root);
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a"), new string('x', 100));

        Assert.Equal(100, store.Clear());
        Assert.Empty(Directory.GetFiles(_root));
    }
}
