---
id: 07
title: 原型：信息架构与 UI
labels: ["wayfinder:prototype"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: [04, 06]
---

## Question

基于元数据约定与生命周期机制，做低成本 UI 原型（Avalonia 12 导航栏、页面清单：首页/mod 列表+搜索/mod 详情/设置/BepInEx 代安装向导），让用户对布局与流程做出反应后定稿信息架构。

## Progress（2026-09-09，agent）

原型已产出（**丢弃型，不是产品代码**）：`docs/wayfinder/prototype-ui/`

- 可交互运行：`cd docs/wayfinder/prototype-ui && dotnet run`（.NET 10 + Avalonia 12.1.2，restore 已验证可用）
- 无头渲染三张截图：`docs/wayfinder/prototype-ui/screens/variant-A|B|C.png`（1180×760，`dotnet <dll> --render <dir>`）
- 三个 IA 变体：**A** DrawerPage CompactInline（左折叠抽屉）/ **B** 顶部导航 / **C** 左右分栏（左列表右详情）
- 五个页面：首页（状态卡+快捷操作+最近更新）/ Mod 库（搜索+卡片列表）/ Mod 详情（概览·配置·版本三 tab）/ 设置 / BepInEx 代装向导（五步）
- 假数据按工单 04 的 info.json schema 造（含 unknown-version 私改版、uvfs-framework 特例、依赖缺失提示）
- 附带发现（进规格书）：Avalonia 12 里 `TextBox.Watermark` 已废弃 → `PlaceholderText`

**待用户反应（HITL，5 问）**：
1. 导航形态选 A / B / C / A+C 组合？
2. 首页内容够不够（状态卡+快捷操作+最近更新）？
3. Mod 详情三 tab（概览/配置/版本）分层对不对？要不要加「依赖」tab？
4. 配置控件形态认可吗（bool 开关/数字文本框/枚举下拉/快捷键/色块）？数字要不要改滑块？
5. 代装向导五步的表述小白能懂吗？要不要合并成三步？

未答复，工单保持 open。决议落定后：把原型提交到 throwaway 分支（如 `prototype/ui-ia`），main 只留决议。
## Resolution（2026-09-09，HITL：原型 + 用户反应）

**资产**：`docs/wayfinder/prototype-ui/`（Avalonia 12.1.2 / net10，`dotnet run` 可跑，`--render <dir> all` 无头出图）；截图副本在 `Y:\lunaqua-prototype\`。原型将提交到 throwaway 分支 `prototype/ui-ia`，main 只留决议。

**决议**：
1. **导航 = A + C 组合**：全局壳用官方 `DrawerPage`（`DrawerLayoutBehavior=CompactInline`、`CompactDrawerLength=48`、展开 `DrawerLength=230`）；**Mod 库页内再左右分栏**（左 400px 列表 / 右详情）。
2. **首页**：三张状态卡（游戏目录 / BepInEx 状态 / 可更新数）+ 四个快捷操作（安装·修复 BepInEx、检查更新、打开 mod 目录、启动游戏）+ 最近更新列表。
3. **Mod 详情 = 三个 tab**：概览（描述 + readme 渲染）/ 配置 / 版本（版本下拉 + 历史列表 + 忽略此版本）。不单独加依赖 tab，依赖以概览里的警示条呈现。
4. **配置编辑器控件**：按 `# Setting type` 生成 —— bool 开关 / 数字文本框 / 枚举下拉 / KeyboardShortcut 文本框 / Color 色块+文本框；行尾「重置」；顶部「只看已改动项」+「全部重置」。
5. **BepInEx 代装向导 = 三步**（找到游戏 → 自动安装 → 验证），每步一个 **Expander**（默认折叠，点开看细节：哈希校验/备份/日志轮询）。

**附带实现事实（进规格书）**：
- Avalonia 12 中 `TextBox.Watermark` 已废弃 → 用 `PlaceholderText`；
- `DrawerPage` 关键属性与取值（`DrawerLayoutBehavior` = Overlay/Split/CompactOverlay/**CompactInline**，`DrawerBehavior` = Auto/Flyout/Locked/Disabled）；
- 无头渲染可用：`Avalonia.Headless` + `UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })` + `UseSkia()` + `window.CaptureRenderedFrame()`。

**未决**：无。