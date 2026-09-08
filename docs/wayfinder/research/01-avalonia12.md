# 调研报告：Avalonia 12 迁移与生态摸底（工单 01 资产）

调研时间 2026-09-09；标注：【事实】= 已查证；【推断】= 判断。

## 1. 版本现状
- Avalonia 12.0.0 stable 发布 2026-04-07；当前最新 **12.1.2**（2026-09-02）；12.0 线补丁 12.0.5。11.3.x 仍在维护（11.3.21，2026-09-08）可作回退线。
- NuGet 可直接引用；v12 放弃 .NET Framework / netstandard，仅 .NET 8+，官方推荐 .NET 10。
- TFM：核心包有 net8.0+net10.0，但 **FluentAvaloniaUI 3.x 与 FluentIcons.Avalonia.Fluent 2.1.3xx 仅发 net10.0** → 用它们就必须 net10.0。

## 2. 破坏性变更（与本项目相关）
**无变更**：主题资源字典/主题切换（FluentTheme、ThemeDictionaries、ThemeVariant 全在）、样式选择器语法（无任何条目）。
**有变更**：
1. 编译绑定默认开启（{Binding} 按 CompiledBinding 处理；补 x:DataType 或改 ReflectionBinding）——最易批量报错的一步。
2. 绑定类层级重做：IBinding→BindingBase；InstancedBinding→BindingExpressionBase；C# 建绑定用 ReflectionBinding/CompiledBinding。
3. 绑定插件体系移除；DataAnnotations 校验插件默认禁用；自定义控件可删 UpdateDataValidation 覆写。
4. 窗口装饰重做：TitleBar/CaptionButtons/ChromeOverlayLayer/ExtendClientAreaChromeHints 删除 → WindowDecorations + ExtendClientAreaToDecorationsHint；SystemDecorations→WindowDecorations。
5. 剪贴板：IDataObject/DataObject→DataTransfer/DataTransferItem；GetTextAsync→TryGetTextAsync；BinaryFormatter 移除。
6. 手势事件移到 InputElement（XAML 去 Gestures. 前缀）；焦点事件签名改 FocusChangedEventArgs。
7. WindowState 不能再从 Style 设置；TopLevel 一律 TopLevel.GetTopLevel(visual)。
8. DispatcherTimer/AvaloniaSynchronizationContext 绑定当前 Dispatcher（须 UI 线程构造）。
9. 隐藏控件动画默认停止（Animation.PlaybackBehavior.Always 恢复）。
10. Avalonia.Diagnostics 移除 → AvaloniaUI.DiagnosticsSupport（AttachDeveloperTools()）。
11. Direct2D1 后端删除（Skia 唯一）；显式 UseSkia() 需补 UseHarfBuzz()；Type1 字体不再支持。

## 3. 导航控件：官方 DrawerPage 就是「侧开可折叠」
- 核心库**没有** NavigationView 控件；但 v12 新增官方页面导航族：ContentPage、**DrawerPage**、NavigationPage（PushAsync/PopAsync）、CarouselPage、TabbedPage、CommandBar、PipsPager。
- DrawerPage 关键 API（官方文档 https://docs.avaloniaui.net/controls/navigation/drawerpage ）：
  - DrawerPlacement：Left/Right/Top/Bottom（侧开）；IsOpen（可绑定 MVVM）
  - DrawerLayoutBehavior：Overlay / Split / **CompactOverlay / CompactInline**——后两者关闭时保留窄图标栏（CompactDrawerLength 默认 48）展开为完整抽屉 = WinUI NavigationView 的 LeftCompact 模式
  - DrawerLength（默认 320）、DrawerBreakpointLength（响应式自动切换）、DrawerBehavior：Auto/Flyout/Locked/Disabled
  - DrawerHeader/Footer/Icon(+Template)、BackdropBrush、Opened/Closing(可 Cancel)/Closed、Esc 关闭、触摸滑动
  - 与 NavigationPage 联动：页面栈非根时抽屉按钮自动变返回键
  - 文档页含 10+ 段可直接抄的 XAML/C# 示例
- FluentAvalonia v3 的 NavigationView 仍在（3.1.0 包内 58 个属性与 v2.3.0 基本一致），但伴随类型加 FA 前缀：NavigationViewPaneDisplayMode→**FANavigationViewPaneDisplayMode**（值不变，含 LeftCompact）。FA 作者未发 v3 迁移说明。
- 结论：既然 v1 用 FA 的唯一理由是侧折叠导航栏，v12 的 DrawerPage（CompactInline）即可替代，v2 不引 FA。

## 4. 其余生态件
- **ReactiveUI 换包**：Avalonia.ReactiveUI 止步 11.3.8/9，NuGet 已标记弃用（Legacy），官方替代 = **ReactiveUI.Avalonia**，与 Avalonia 同步发版（12.1.2 依赖 Avalonia 12.1.2 + ReactiveUI 24.2.0 + Splat 21.0.0；TFM net8/9/10/11）。UseReactiveUI/ReactiveWindow/ReactiveUserControl/RoutedViewHost/ViewModelViewHost 均在；ReactiveControl/AutoPersistHelper 未检出，按报错处理。
- **FluentIcons.Avalonia.Fluent 2.1.339.1**（2026-08-31）：依赖 FluentAvaloniaUI>=3.0.0，仅 net10.0。不需要 FA 时可用无 FA 变体 FluentIcons.Avalonia 2.1.339.1（直接依赖 Avalonia>=12.0.0，net8+net10）。
- **主题切换官方做法**：Application.RequestedThemeVariant = Light/Dark/Default（Default=跟随系统），运行时改即时生效；ResourceDictionary.ThemeDictionaries 放两套 + 一律 DynamicResource；ThemeVariantScope 局部强制；ActualThemeVariant(Changed) 侦测。框架不提供 ThemeViewModel，就是应用侧十几行代码。

## 5. 结论与注意点
**v2 直接上 Avalonia 12 成立**（12.0.0 已 stable 五个月、迭代到 12.1.2；担心点主题/选择器/图标导航均不在破坏清单；唯一结构变化是 ReactiveUI 换包且官方给了替代）。
注意点：TFM 定 **net10.0**；Avalonia 全家 12.1.2；删 Avalonia.ReactiveUI 换 ReactiveUI.Avalonia 12.1.2；不引 FluentAvalonia（用官方 DrawerPage）；图标包按是否用 FA 二选一；编译绑定补 x:DataType；自绘标题栏用新 WindowDecorations；DevTools 换包；字体用 TTF/OTF。

来源：https://docs.avaloniaui.net/docs/avalonia12-breaking-changes ；https://avaloniaui.net/blog/avalonia-12 ；https://github.com/AvaloniaUI/Avalonia/releases ；https://docs.avaloniaui.net/controls/navigation/drawerpage ；https://docs.avaloniaui.net/docs/how-to/theme-switching-how-to ；https://www.nuget.org/packages/ReactiveUI.Avalonia ；https://www.nuget.org/packages/FluentIcons.Avalonia.Fluent 。
