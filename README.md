# Lunaqua v2

「柴」（Stick Fight: The Game，Steam AppID 674940）的 mod 管理器 · 正在按规格书重写。

- **规格书（唯一权威）**：[docs/spec/lunaqua-v2-spec.md](docs/spec/lunaqua-v2-spec.md)
- **决策与调研**：[docs/wayfinder/](docs/wayfinder/)（地图 + 13 张工单 + 4 份调研）
- **旧项目 v1**：分支 `archive/v1`（ReactiveUI + FluentAvalonia 那版，已归档只读）
- **UI 原型（丢弃型）**：分支 `prototype/ui-ia`

## 状态

- 技术栈：Avalonia 12.1.2 / .NET 10 / CommunityToolkit.Mvvm / FluentIcons / Serilog / MS.DI / Velopack
- 进度：规格书定稿，尚未动工（M0 起步）
- 开发辅助：`bash docs/tools/install-avalonia-skills.sh` 安装 Avalonia 12 skill 包（33 个）

## 分支

| 分支 | 用途 |
|---|---|
| `main` | v2 开发主线（干净起点） |
| `archive/v1` | 旧项目完整历史（归档） |
| `prototype/ui-ia` | UI 原型（throwaway） |
