---
id: 13
title: 决策：技术栈与依赖清单（站长亲自审理）
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: []
---

## Question

站长要亲自过审 v2 使用的技术栈与第三方依赖。逐项定「用什么 / 不用什么 / 为什么」，产出**依赖清单（BOM）**：每个依赖 = 用途 + 包名 + 版本 + 备选 + 许可 + 风险。

待审理面（每项都要有裁决）：
1. **MVVM**：ReactiveUI.Avalonia（工单 01 已定，仍需你确认）vs CommunityToolkit.Mvvm vs 混用
2. **图标库**：FluentIcons.Avalonia / Lucide / Material.Icons / Projektanker.Icons + SVG / 内嵌 PathIcon 几何（零依赖）
3. **DI 容器**：Microsoft.Extensions.DependencyInjection / Splat（随 ReactiveUI）/ 手写组合根
4. **日志**：无 / Serilog + File sink / Microsoft.Extensions.Logging
5. **压缩包**：System.IO.Compression（内置）/ SharpCompress
6. **程序集元数据**（读 BepInEx 插件 GUID/Version）：System.Reflection.Metadata（内置）/ Mono.Cecil
7. **HTTP/JSON**：自写 HttpClient + System.Text.Json（工单 02 已定）/ Flurl / Refit
8. **测试**：xUnit + Avalonia.Headless / NUnit / 不写测试
9. **打包与自更新**：Velopack / Squirrel / 自写 / 手动分发（与工单 08 交叉）
10. **SVG/字体**：Svg.Skia / 仅内嵌 TTF（HarmonyOS Sans）
11. **其它候选**：HotAvalonia（开发热重载）、Avalonia.Xaml.Behaviors、MsgBox.Avalonia

裁决形式：每项给出「采用 / 不采用 / 待定」，并记进依赖清单；未获你确认的依赖不得进规格书。

## Progress（2026-09-09，grilling 两轮）

**已裁决**：
1. **MVVM = CommunityToolkit.Mvvm**（用户最熟、Avalonia 12 官方范式）——**推翻工单 01 的 ReactiveUI.Avalonia 结论**（旧项目是改别人的，不作为选型依据）。
2. **图标库 = FluentIcons.Avalonia 2.1.339.1**（net8+net10，依赖 Avalonia 12.0.0，不依赖 FluentAvalonia）。Lucide.Avalonia / Projektanker 仍是 v11 线，不采用。
3. **DI = Microsoft.Extensions.DependencyInjection**。
4. **日志 = Serilog + 文件 sink**（玩家可一键导出日志）。
5. **压缩 = System.IO.Compression**（内置，仅 zip，够用）。
6. **程序集元数据 = Mono.Cecil 0.11.6**（读 BepInPlugin/BepInDependency）。
7. **测试 = xUnit + Avalonia.Headless.XUnit 12.1.2**（解析器/安装器必须有单测）。
8. **打包/自更新 = Velopack 1.2.0**（差分更新，更新源可挂自己的 OpenList 站）。
9. **不引 SVG 库**：图标走 FluentIcons，字体内嵌 HarmonyOS Sans TTF。
10. **其它候选一律不要**（HotAvalonia / Avalonia.Xaml.Behaviors / MsgBox.Avalonia），用户后续自行按需添加。

**.NET SDK 调研（应用户要求）**：
- AList 时代的 `AListSdkSharp`（j4587698）最后提交 2024-10-24（v1.2.1），**已停更近两年**；
- OpenList 官方**没有 .NET SDK**（OpenListTeam 生态只有 Go/TypeScript/Python；社区 SDK 有 TS/Python/Kotlin 版）；
- 社区 C# 项目两个，均 0 star 且**无 LICENSE**（不可直接抄代码）：
  - `capacitor1/AlistConsoleClient`（2026-07-20 更新）——含完整 `AlistClient` 与 DTO（ApiResponse/FsList/FsGet/PublicSettings/Me），可作 **DTO 形状参考**；
  - `KingStar-China/OpenList-Sync`（2026-09-08 更新）——OpenList+TaoSync 的 Windows 管理器，非 API 客户端库。

**待裁决**：HTTP 客户端（自研 / Flurl / Refit）——已向用户解释三者区别，等其看完再问。
## Resolution（2026-09-09，grilling 两轮 + 用户逐项裁决）

### 依赖清单（BOM，v2 采用）

| 用途 | 选择 | 版本 | 许可 | 备注 |
|---|---|---|---|---|
| UI 框架 | Avalonia + Avalonia.Desktop + Avalonia.Themes.Fluent + Avalonia.Fonts.Inter | 12.1.2 | MIT | TFM **net10.0** |
| MVVM | **CommunityToolkit.Mvvm** | 8.4.2 | MIT | 用户最熟、Avalonia 12 官方范式 |
| 图标 | FluentIcons.Avalonia | 2.1.339.1 | MIT | net8+net10，依赖 Avalonia 12.0.0，不依赖 FluentAvalonia |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT | |
| 日志 | Serilog + Serilog.Sinks.File | 4.4.0 / 7.0.0 | Apache-2.0 | 滚动文件，玩家可一键导出 |
| 程序集元数据 | Mono.Cecil | 0.11.6 | MIT | 读 BepInPlugin / BepInDependency |
| 压缩 | System.IO.Compression | 内置 | — | 仅 zip（BepInEx pack 够用） |
| HTTP/JSON | **自研 HttpClient + System.Text.Json** | 内置 | — | ~300 行；**判 JSON code，不判 HTTP 状态码** |
| BepInEx .cfg 解析 | 自研行式解析器 | — | — | 不引 Tomlyn（.cfg 非严格 TOML） |
| Steam 库定位 | 自研 KeyValues 解析（libraryfolders.vdf） | — | — | 纯文本，无互操作 |
| 测试 | xUnit v3 + Avalonia.Headless.XUnit | 4.0.0 / 12.1.2 | Apache-2.0 / MIT | 解析器、安装器必须有单测 |
| 打包/自更新 | **Velopack** | 1.2.0 | MIT | 差分更新；更新源可挂自己的 OpenList 站 |

### 明确不用

- **ReactiveUI / ReactiveUI.Avalonia** —— 推翻工单 01 的结论（用户：旧项目是改别人的，不作选型依据；CTK 才是他习惯的）
- **FluentAvalonia** —— 官方 DrawerPage 已覆盖（工单 01）
- **AListSdkSharp** —— 停更近两年 + 分页 bug（工单 02）
- **Flurl / Refit** —— 端点仅 3 个 + OpenList 非标准错误模型（HTTP 200 + code）
- **Tomlyn** —— .cfg 是 BepInEx 方言
- **Svg.Skia / Avalonia.Svg.Skia** —— 图标走 FluentIcons
- **HotAvalonia / Avalonia.Xaml.Behaviors / MsgBox.Avalonia** —— 用户后续自行按需添加
- **I18N.Avalonia** —— 中文 only（charting 已定）

### 依赖管理原则

1. 依赖清单以本表为准，新增依赖必须回到本工单追加裁决；
2. 优先内置（System.Text.Json / System.IO.Compression / System.Reflection 等）；
3. 每个依赖记录版本 + 许可，规格书里附本表。

**未决：无。**

## Amendment（2026-09-09，M0 实测）

**测试依赖版本修正**：BOM 原写 `xUnit v3 4.0.0`，实测不兼容 —— `Avalonia.Headless.XUnit 12.1.2` 编译时依赖 `xunit.v3.extensibility.core 3.2.2`，升到 4.0.0 后 `TestIntrospectionHelper.GetTestCaseDetails` 签名变更，`[AvaloniaFact]` 在**发现阶段**就抛 `MissingMethodException`（9 个用例里 4 个直接 error）。

- 裁决：`xunit.v3` 锁 **3.2.2**，与 Avalonia 无头集成对齐；其余测试栈不变（`Avalonia.Headless.XUnit 12.1.2`）。
- 同时确认：.NET 10 的 `dotnet test` 必须走 Microsoft.Testing.Platform（仓库根 `dotnet.config` 已加），VSTest 目标在 .NET 10 SDK 被移除。
- 规格书 §2 BOM 已同步改。