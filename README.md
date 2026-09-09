# Lunaqua v2

「柴」（Stick Fight: The Game，Steam AppID 674940）的 mod 管理器 · 正在按规格书重写。

- **规格书（唯一权威）**：[docs/spec/lunaqua-v2-spec.md](docs/spec/lunaqua-v2-spec.md)
- **决策与调研**：[docs/wayfinder/](docs/wayfinder/)（地图 + 13 张工单 + 4 份调研）
- **旧项目 v1**：分支 `archive/v1`（ReactiveUI + FluentAvalonia 那版，已归档只读）
- **UI 原型（丢弃型）**：分支 `prototype/ui-ia`

## 状态

- 技术栈：Avalonia 12.1.2 / .NET 10 / CommunityToolkit.Mvvm / FluentIcons / Serilog / MS.DI / Velopack
- 进度：
  - **M0 骨架** ✅ DrawerPage 壳 + 四个页面 + 设置持久化 + 文件日志
  - **M1 站点与元数据** ✅ 自研 OpenList 客户端、info.json 解析校验、Mod 库列表/搜索/详情（站上 9 个 mod 联调通过）
  - **M2 生命周期** ✅ 事务式安装/更新/回退/卸载/启停、凭证、类型策略、sha256 缓存、串行队列、游戏运行检测
  - 测试：`dotnet test Lunaqua.sln` → 104 个用例
- 下一步：**M3 BepInEx 全自动代装**（定位游戏 / PE 判位数 / 哈希三层 / 备份 / 部署 / 验证 / 回滚）
- 开发辅助：`bash docs/tools/install-avalonia-skills.sh` 安装 Avalonia 12 skill 包（33 个）

## 开发

```bash
dotnet build Lunaqua.sln           # 编译（警告即错误）
dotnet run --project src/Lunaqua   # 运行
dotnet test Lunaqua.sln            # 测试（xUnit v3 + Avalonia.Headless）
```

- 数据目录：`%LocalAppData%\Lunaqua`（环境变量 `LUNAQUA_DATA_DIR` 可覆盖，测试与便携用）
- 设置：`%LocalAppData%\Lunaqua\settings.json`（原子写入：先写 .tmp 再替换）
- 日志：`%LocalAppData%\Lunaqua\logs\lunaqua-<日期>.log`（按天滚动，保留 14 天）

## 目录

```
src/Lunaqua/            应用（Views / ViewModels / Services / Models / Resources）
tests/Lunaqua.Tests/    xUnit v3 + Avalonia.Headless 测试
docs/spec/              规格书（唯一权威）
docs/wayfinder/         地图 / 工单 / 调研
docs/backfill/          存量 9 个 mod 的 info.json 草案
docs/tools/             一次性工具（补录工具、skill 安装脚本）
```

## 分支

| 分支 | 用途 |
|---|---|
| `main` | v2 开发主线（干净起点） |
| `archive/v1` | 旧项目完整历史（归档） |
| `prototype/ui-ia` | UI 原型（throwaway） |
