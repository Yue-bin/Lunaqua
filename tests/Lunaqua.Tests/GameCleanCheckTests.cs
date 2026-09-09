using Lunaqua.Domain;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class GameCleanCheckTests : IDisposable
{
    private readonly string _root;
    private readonly GameInstallation _installation;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public GameCleanCheckTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-clean", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "StickFight_Data", "Managed"));

        Add("StickFight.exe", TestPe.Create(TestPe.MachineAmd64));
        Add("StickFight_Data/Managed/Assembly-CSharp.dll", TestMod.Bytes("官方 Assembly-CSharp"));
        Add("StickFight_Data/Managed/UnityEngine.dll", TestMod.Bytes("官方 UnityEngine"));
        Add("StickFight_Data/globalgamemanagers", TestMod.Bytes("官方 globalgamemanagers"));
        Add("StickFight_Data/level0", TestMod.Bytes("官方 level0"));

        _installation = GameInstallationValidator.Validate(_root);
        Assert.True(_installation.IsValid, _installation.Summary);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task buildid命中官方表就判干净()
    {
        var service = new GameCleanCheckService(BuildTable("24952802"));

        var result = await service.CheckAsync(_installation, "24952802", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.Clean, result.Status);
        Assert.True(result.IsClean);
        Assert.Equal("24952802", result.MatchedBuildId);
        Assert.Contains("官方构建", result.Message);
    }

    [Fact]
    public async Task buildid不认识但哈希对得上也判干净()
    {
        var service = new GameCleanCheckService(BuildTable("24952802"));

        var result = await service.CheckAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.Clean, result.Status);
        Assert.Equal("24952802", result.MatchedBuildId);
        Assert.Contains("哈希", result.Message);
    }

    [Fact]
    public async Task 哈希对不上要求Steam校验()
    {
        var service = new GameCleanCheckService(BuildTable("24952802"));
        File.WriteAllText(Path.Combine(_root, "StickFight_Data", "Managed", "Assembly-CSharp.dll"), "被改过了");

        var result = await service.CheckAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.NeedsValidate, result.Status);
        Assert.False(result.IsClean);
        Assert.Contains("Steam 校验", result.Message);
    }

    [Fact]
    public async Task 命中本机信任基线判Trusted()
    {
        var service = new GameCleanCheckService(BuildTable("11111111", matchFiles: false));
        await service.CaptureBaselineAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);

        var result = await service.CheckAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.Trusted, result.Status);
        Assert.True(result.IsClean);
        Assert.Contains("基线", result.Message);
    }

    [Fact]
    public async Task 基线之后文件又被改了就不算Trusted()
    {
        var service = new GameCleanCheckService(BuildTable("11111111", matchFiles: false));
        await service.CaptureBaselineAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(_root, "StickFight_Data", "Managed", "Assembly-CSharp.dll"), "又改了");

        var result = await service.CheckAsync(_installation, "99999999", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.NeedsValidate, result.Status);
    }

    [Fact]
    public async Task 被mod覆盖的文件可以排除在比对之外()
    {
        var service = new GameCleanCheckService(BuildTable("24952802"));
        File.WriteAllText(Path.Combine(_root, "StickFight_Data", "level0"), "被 UVFS 覆盖的 level0");

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "StickFight_Data/level0" };
        var result = await service.CheckAsync(
            _installation,
            "99999999",
            excluded,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.Clean, result.Status);
    }

    [Fact]
    public async Task 没找到exe时判Unknown()
    {
        var service = new GameCleanCheckService(BuildTable());
        var broken = new GameInstallation(_root, null, null, false, ["没有 exe"]);

        var result = await service.CheckAsync(broken, null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GameCleanStatus.Unknown, result.Status);
    }

    [Fact]
    public void 能加载随程序发布的指纹表()
    {
        var table = GameFingerprintTableLoader.Load();

        Assert.Equal("674940", table.AppId);
        Assert.True(table.Count >= 1, "指纹表是空的");

        var build = table.FindByBuildId("24952802");
        Assert.NotNull(build);
        Assert.True(build!.HasHashes, "当前 build 没有哈希");
        Assert.Contains(build.Files, file => file.Path == "StickFight.exe");
        Assert.Contains(build.Files, file => file.Path.EndsWith("Assembly-CSharp.dll", StringComparison.Ordinal));
    }

    private GameFingerprintTable BuildTable(string buildId = "24952802", bool matchFiles = true) => new(
        1,
        "674940",
        [
            new GameFingerprint(
                buildId,
                "674945",
                "8864248428190094911",
                "StickFight.exe",
                "StickFight_Data",
                [.. _files.Select(pair => new FingerprintFile(
                    pair.Key,
                    matchFiles ? TestMod.Sha256(pair.Value) : new string('a', 64),
                    pair.Value.Length))]),
        ]);

    private void Add(string relativePath, byte[] content)
    {
        _files[relativePath] = content;
        File.WriteAllBytes(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);
    }
}
