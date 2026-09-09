using Lunaqua.Domain;
using Lunaqua.Domain.Strategies;
using Xunit;

namespace Lunaqua.Tests;

public sealed class ModTypeStrategyTests
{
    private static readonly GameContext Game = new(@"C:\Games\StickFight");

    [Fact]
    public void bepinex落位到plugins并写凭证到lunaqua目录()
    {
        var strategy = new BepInExModStrategy();
        var meta = TestMod.Info(versions: TestMod.Release("1.0.0", ("demo.dll", TestMod.Bytes("x"))));
        var plan = strategy.PlanInstall(Game, meta, meta.LatestVersion);

        var target = Assert.Single(plan.Targets);
        Assert.Equal(Path.Combine(Game.PluginsDirectory, "demo.dll"), target.DestinationPath);
        Assert.Equal(Path.Combine(Game.LunaquaDirectory, meta.Id, "info.json"), plan.CredentialPath);
        Assert.False(strategy.CredentialInsideInstallRoot);
    }

    [Fact]
    public void bepinex保留子目录结构()
    {
        var strategy = new BepInExModStrategy();
        var meta = TestMod.Info(versions: TestMod.Release("1.0.0", ("sub/demo.dll", TestMod.Bytes("x"))));
        var plan = strategy.PlanInstall(Game, meta, meta.LatestVersion);

        Assert.Equal(
            Path.Combine(Game.PluginsDirectory, "sub", "demo.dll"),
            plan.Targets[0].DestinationPath);
    }

    [Fact]
    public void uvfs只允许data与bundles作用域()
    {
        var strategy = new UvfsModStrategy();

        var bad = TestMod.Info(type: ModTypes.Uvfs, guid: null, versions: TestMod.Release("1.0.0", ("evil.dll", TestMod.Bytes("x"))));
        Assert.Contains(strategy.Validate(bad, bad.LatestVersion), error => error.Contains("data/"));

        var good = TestMod.Info(type: ModTypes.Uvfs, guid: null, versions: TestMod.Release("1.0.0", ("data/tex.bundle", TestMod.Bytes("x"))));
        Assert.Empty(strategy.Validate(good, good.LatestVersion));

        var plan = strategy.PlanInstall(Game, good, good.LatestVersion);
        Assert.Equal(Path.Combine(Game.OverlayModsDirectory, good.Id, "data", "tex.bundle"), plan.Targets[0].DestinationPath);
        Assert.True(strategy.CredentialInsideInstallRoot);
    }

    [Fact]
    public void uvfs框架按内置映射表落位()
    {
        var strategy = new UvfsFrameworkModStrategy();
        var meta = TestMod.Info(
            type: ModTypes.UvfsFramework,
            versions: TestMod.Release(
                "1.0.0",
                ("BepInEx/patchers/Overlay.NativeLoader/overlay64.dll", TestMod.Bytes("a")),
                ("BepInEx/plugins/Overlay.Managed.dll", TestMod.Bytes("b")),
                ("BepInEx/plugins/XUnity.ResourceRedirector/XUnity.ResourceRedirector.dll", TestMod.Bytes("c"))));

        Assert.Empty(strategy.Validate(meta, meta.LatestVersion));

        var plan = strategy.PlanInstall(Game, meta, meta.LatestVersion);
        Assert.Equal(3, plan.Targets.Count);
        Assert.Contains(plan.Targets, target => target.DestinationPath == Game.Combine("BepInEx/patchers/Overlay.NativeLoader/overlay64.dll"));
    }

    [Fact]
    public void uvfs框架结构不符时拒绝安装()
    {
        var strategy = new UvfsFrameworkModStrategy();
        var meta = TestMod.Info(
            type: ModTypes.UvfsFramework,
            versions: TestMod.Release("1.0.0", ("BepInEx/plugins/SomeOther.dll", TestMod.Bytes("x"))));

        Assert.Contains(strategy.Validate(meta, meta.LatestVersion), error => error.Contains("映射表"));
    }

    [Fact]
    public void 策略表能按类型查找()
    {
        var strategies = new ModTypeStrategies();

        Assert.NotNull(strategies.Find(ModTypes.BepInEx));
        Assert.NotNull(strategies.Find(ModTypes.Uvfs));
        Assert.NotNull(strategies.Find(ModTypes.UvfsFramework));
        Assert.Null(strategies.Find("exe"));
        Assert.Throws<NotSupportedException>(() => strategies.Get("exe"));
    }
}
