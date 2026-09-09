using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class FileTransactionTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _game;

    public FileTransactionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-tx", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _game = Path.Combine(_root, "game");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_game);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void 提交后新文件生效且备份留下()
    {
        var destination = Path.Combine(_game, "BepInEx", "plugins", "demo.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "旧内容");
        var newFile = Path.Combine(_source, "new.dll");
        File.WriteAllText(newFile, "新内容");

        var transaction = new FileTransaction(Path.Combine(_game, ".lunaqua", "backup", "t1"), _game);
        transaction.Replace(newFile, destination);
        transaction.Commit();

        Assert.Equal("新内容", File.ReadAllText(destination));
        Assert.Equal("旧内容", File.ReadAllText(transaction.Backups[0].BackupPath));
        Assert.True(File.Exists(Path.Combine(_game, ".lunaqua", "backup", "t1", "manifest.json")));
    }

    [Fact]
    public void 回滚恢复旧文件并删掉新文件()
    {
        var destination = Path.Combine(_game, "plugins", "demo.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "旧内容");
        var brandNew = Path.Combine(_game, "plugins", "extra.dll");
        var newFile = Path.Combine(_source, "new.dll");
        File.WriteAllText(newFile, "新内容");

        var transaction = new FileTransaction(Path.Combine(_game, "backup", "t1"), _game);
        transaction.Replace(newFile, destination);
        transaction.Replace(newFile, brandNew);
        transaction.Rollback();

        Assert.Equal("旧内容", File.ReadAllText(destination));
        Assert.False(File.Exists(brandNew));
    }

    [Fact]
    public void 删除的文件能回滚回来()
    {
        var path = Path.Combine(_game, "plugins", "old.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "旧内容");

        var transaction = new FileTransaction(Path.Combine(_game, "backup", "t1"), _game);
        transaction.Remove(path);
        Assert.False(File.Exists(path));

        transaction.Rollback();
        Assert.Equal("旧内容", File.ReadAllText(path));
    }
}
