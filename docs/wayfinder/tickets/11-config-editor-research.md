---
id: 11
title: 调研：BepInEx 配置解析与配置编辑器
labels: ["wayfinder:research"]
status: closed
assignee: moncak (in-session research, 2026-09-09)
blocked-by: []
---

## Question

配置编辑器（Avalonia 原生 GUI 编辑 BepInEx/config/<guid>.cfg）能否复用 BepInEx 自身 API？查证：BepInEx 5 的 ConfigFile/ConfigDescription/AcceptableValue* 公开面与可在游戏外引用的方式（NuGet BepInEx.Core）；.cfg 的元数据注释格式与可重建字段；Tomlyn 自写解析/写回的成本与注释保持；r2modman/Gale/BepInEx.ConfigurationManager 先例；写回与并发风险；UVFS config.ini 是否纳入。

## Resolution（2026-09-09，会话内调研；子代理两次失败后由主会话完成）

完整报告：[research/11-bepinex-config-editor.md](../research/11-bepinex-config-editor.md)

**结论：不需要引用 BepInEx API，自写行式解析器。**

- BepInEx 5 **没有 NuGet 库包**（BepInEx.Core 等实测不存在），且 `ConfigFile.Load()` 不还原元数据注释 → 引用无收益、风险高。
- .cfg **自描述**：`## 描述`、`# Setting type:`、`# Default value:`、`# Acceptable values:` / `# Acceptable value range:`、`[Flags]` 额外一行；头部有 `## Plugin GUID:`。
- .cfg **不是严格 TOML**（键可含内部空格、写入不加引号）→ 不能用 Tomlyn；用行式解析 + 只替换值行写回（保留注释/顺序），参考 Gale `config/bepinex/{de,ser}.rs`。
- BepInEx 5 无 List/Dictionary 配置值 → 不需要数组/字典控件；Flags enum 用逗号分隔字符串。
- 并发：游戏运行中插件可能写回覆盖，UI 需警告；写回 UTF-8 无 BOM。
- UVFS `overlay/config.ini` 格式简单，但 root_ 暗门已定不支持 → v2 不提供该开关。