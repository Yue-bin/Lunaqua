---
id: 01
title: 调研：Avalonia 12 迁移与生态摸底
labels: ["wayfinder:research"]
status: closed
assignee: research-subagent (2026-09-09)
blocked-by: []
---

## Question

查证：Avalonia 12 stable 当前版本号与发布状态；11→12 的破坏性变更清单（尤其主题资源、样式选择器、ReactiveUI 模板）；官方 NavigationView 是否支持旧 FluentAvalonia 那种「侧开可折叠导航栏」及用法；FluentIcons.Avalonia 对 v12 的兼容版本；v12 推荐的主题/明暗切换方案（替代自写 ThemeViewModel + 两套资源）。结论要给出「v2 直接上 v12」是否成立及注意点。

## Resolution（2026-09-09，research 子代理）

完整报告：[research/01-avalonia12.md](../research/01-avalonia12.md)

**成立**：v2 直接上 Avalonia 12。12.0.0 stable 2026-04-07、当前 12.1.2；主题资源与选择器语法均无破坏性变更；唯一结构变化是 ReactiveUI 换包（Avalonia.ReactiveUI 已弃用 → ReactiveUI.Avalonia 12.1.2）。

要点：
- TFM 定 **net10.0**（FA 3.x 与 FluentIcons.Fluent 仅发 net10）。
- 官方 **DrawerPage**（CompactInline/CompactOverlay + CompactDrawerLength 48）就是「侧开可折叠导航栏」的官方等价物 → **v2 不引 FluentAvalonia**（用户既定理由成立）。
- 删 Avalonia.ReactiveUI，换 ReactiveUI.Avalonia（UseReactiveUI/ReactiveWindow/ReactiveUserControl/RoutedViewHost 平移）。
- 编译绑定默认开启（补 x:DataType / 用 ReflectionBinding）；窗口装饰 API 重做；剪贴板 DataTransfer；手势去前缀；DevTools 换 AvaloniaUI.DiagnosticsSupport；UseSkia 需补 UseHarfBuzz。
- 主题切换仍是 RequestedThemeVariant + ThemeDictionaries + DynamicResource，无需框架外机制。

## Amendment（2026-09-09，工单 13 裁决）

**本工单的 ReactiveUI 结论作废**：MVVM 改用 **CommunityToolkit.Mvvm 8.4.2**，不再引入 ReactiveUI / ReactiveUI.Avalonia。
其余结论仍有效：Avalonia **12.1.2** + **net10.0**、官方 **DrawerPage** 取代 FluentAvalonia、编译绑定默认开启、窗口装饰/剪贴板/DevTools 等破坏性变更清单。