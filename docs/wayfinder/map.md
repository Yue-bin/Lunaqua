# Lunaqua v2 重启寻路地图

- labels: wayfinder:map
- tickets: 见下方「工单索引」
- tracker: 本地 markdown（docs/wayfinder/）
- 状态: **地图完成**（2026-09-09）——所有决策票关闭，规格书已交付；仅剩 task「补录存量 info.json」

## Destination

一份《Lunaqua v2 重启规格书》：覆盖技术栈（Avalonia 12）、OpenList 客户端方案、mod 元数据与目录约定、双 mod 类型（BepInEx / UVFS）架构、本地生命周期机制、BepInEx 全自动代安装、配置编辑器、UI 信息架构、自身分发方式、**技术栈与依赖清单（站长逐项过审）**。规格书完成后，在当前仓库的新分支上照此动工（动工本身不在本图内）。

## Notes

- 领域：柴 的 BepInEx 5 (x64) mod 生态；上游 mod 站是作者自运营的 OpenList 实例（blog.monblog.top/openlist，AList 兼容 API，匿名可列目录），mod 由作者 CI 经 SFTP 推送上站。
- mod 有两类：BepInEx 代码插件（dll）与 UVFS 覆盖层资源 mod（overlay/mods/<modName>/，见 E:\\codes\\UVFS）。
- 已定方向（charting 问答敲定）：同仓库开新分支推倒重写；只保留旧仓库 Logo/美术资产；中文 only（放弃 i18n）；Windows 优先（mac 后置）；Avalonia 12（官方可折叠导航栏取代 FluentAvalonia 的存在理由）；BepInEx 安装走全自动代操作而非教程引导。
- HITL 工单先加载 grilling 与 domain-modeling 两个 skill；research/prototype 按类型加载对应 skill。
- UI 原型（丢弃型）在 throwaway 分支 `prototype/ui-ia`（commit 9308d2f），main 不留原型代码；截图副本 `Y:\lunaqua-prototype\`。
- 实现阶段先加载 avalonia 系列 skill（已装 ~/.dsh/skills/：avalonia 路由 + 18 个一级 + 14 个二级，共 33 个），它讲通用 Avalonia 12；本项目约定（薄策略层、中文 only、.lunaqua 路径、事务式安装等）以本图决议与规格书为准。
- 工单文件 = tickets/ 下一个 md，frontmatter 含 labels/status/blocked-by/assignee；frontier = status: open、blocked-by 全 closed、无 assignee。一次会话至多解决一张工单（research 除外）。

## 工单索引

| 工单 | 类型 | 状态 | 阻塞于 |
|---|---|---|---|
| [调研：Avalonia 12 迁移与生态摸底](tickets/01-avalonia12-research.md) | research | ✅ closed | — |
| [调研：OpenList 客户端与 API 能力](tickets/02-openlist-client-research.md) | research | ✅ closed | — |
| [调研：BepInEx 5 全自动代安装](tickets/03-bepinex-autoinstall-research.md) | research | ✅ closed | — |
| [决策：mod 元数据格式与目录约定](tickets/04-mod-metadata-convention.md) | grilling | ✅ closed | — |
| [决策：双 mod 类型（BepInEx / UVFS）架构抽象](tickets/05-multi-mod-type-architecture.md) | grilling | ✅ closed | — |
| [决策：mod 本地生命周期机制](tickets/06-mod-local-lifecycle.md) | grilling | ✅ closed | — |
| [原型：信息架构与 UI](tickets/07-ui-prototype.md) | prototype | ✅ closed | — |
| [决策：Lunaqua 自身分发与更新渠道](tickets/08-self-distribution.md) | grilling | ✅ closed | — |
| [决策：技术栈与依赖清单（站长亲自审理）](tickets/13-tech-stack.md) | grilling | ✅ closed | — |
| [汇总：重启规格书](tickets/09-spec-assembly.md) | task | ✅ closed | — |
| [补录：存量 9 个 mod 的 info.json](tickets/10-legacy-backfill.md) | task | open | — |
| [调研：BepInEx 配置解析与配置编辑器](tickets/11-config-editor-research.md) | research | ✅ closed | — |
| [决策：配置编辑器](tickets/12-config-editor.md) | grilling | ✅ closed | 11 |

## Decisions so far

- [调研：OpenList 客户端与 API 能力](tickets/02-openlist-client-research.md): 自研 ~300 行 HttpClient 轻客户端，弃 AListSdkSharp；匿名只读三件套可用，无 /api/fs/read、无搜索索引、sign 每次会话重取，Range/ETag 支持断点与条件请求。
- [调研：Avalonia 12 迁移与生态摸底](tickets/01-avalonia12-research.md): v2 上 Avalonia 12.1.2 + net10.0；官方 DrawerPage 取代 FluentAvalonia；编译绑定默认开启等注意点。（ReactiveUI 部分已被工单 13 作废 → 改用 CommunityToolkit.Mvvm）
- [汇总：重启规格书](tickets/09-spec-assembly.md): 规格书定稿于 `docs/spec/lunaqua-v2-spec.md`（单文件，含 BOM/元数据契约/架构/生命周期/代装/配置编辑器/IA/分发/里程碑 M0–M6/风险）。
- [决策：Lunaqua 自身分发与更新渠道](tickets/08-self-distribution.md): 首发挂 OpenList 站 Lunaqua/ 目录（Setup.exe + readme，留 3 版）；自动更新走自建静态目录（Caddy，无 sign）+ Velopack SimpleWebSource；启动检查提示下载重启；Setup.exe 安装、单稳定通道；设置页「回退到上一版」；GitHub Actions tag 触发发布。
- [决策：技术栈与依赖清单（站长亲自审理）](tickets/13-tech-stack.md): BOM 定稿——Avalonia 12.1.2/net10 + CommunityToolkit.Mvvm 8.4.2 + FluentIcons.Avalonia 2.1.339.1 + MS.DI 10 + Serilog + Mono.Cecil + 自研 HttpClient/.cfg 解析/KeyValues + xUnit v3 & Avalonia.Headless + Velopack 1.2.0；不用 ReactiveUI/FluentAvalonia/AListSdkSharp/Flurl/Refit/Tomlyn/Svg.Skia。
- [原型：信息架构与 UI](tickets/07-ui-prototype.md): 导航 = DrawerPage 折叠抽屉 + Mod 库页内左右分栏（A+C）；首页状态卡+快捷操作+最近更新；详情三 tab（概览/配置/版本）；配置控件按 Setting type 生成 + 只看已改动项；代装向导三步可展开。原型资产：prototype-ui（throwaway 分支 prototype/ui-ia，截图见 Y:\lunaqua-prototype）。
- [决策：mod 本地生命周期机制](tickets/06-mod-local-lifecycle.md): 凭证（.lunaqua/<id>/info.json）+ 磁盘实况两层；事务式安装/更新（临时区→校验→备份→替换→凭证，失败回滚）；启动时后台查更新、手动更新、版本下拉+忽略此版本；启停按 05 策略；卸载反查依赖并问配置；BepInEx 手动 dll 纳管（私改版标未知版本不更新）、UVFS 无元数据目录忽略；sha256 缓存 + Range/ETag；串行队列；游戏运行中拒绝操作。
- [决策：配置编辑器](tickets/12-config-editor.md): 只编辑 BepInEx/config/*.cfg（按 ## Plugin GUID 关联 mod）+ 全局页兜底；自写行式解析、只替换值行、UTF-8 无 BOM；控件按 Setting type 映射（bool/数字/字符串/枚举/Flags/KeyboardShortcut/Color，其余文本框）；KeyCode 表按 Unity 5.6.x 内置、Color 用 hex RGBA + Avalonia 取色盘；即改即存、非法值拒绝、游戏运行中转只读、每条目+每文件重置、编辑前自动备份。
- [调研：BepInEx 配置解析与配置编辑器](tickets/11-config-editor-research.md): 不引 BepInEx API（5 无 NuGet 库包、Load 不还原元数据），自写行式解析/写回；.cfg 自描述（## 描述 / # Setting type / # Default value / # Acceptable values|range / Flags 行）；非严格 TOML 不能用 Tomlyn；无 List/Dict 值；参考 Gale config/bepinex。
- [决策：双 mod 类型（BepInEx / UVFS）架构抽象](tickets/05-multi-mod-type-architecture.md): 薄策略层 + 通用安装核心；bepinex→BepInEx/plugins、uvfs→overlay/mods/<id>、uvfs-framework→管理器内置映射表（特例）；info.json 作已装凭证；uvfs 启停=移出移回、bepinex=整体移入 .lunaqua-disabled；卸载弹窗问配置、覆盖弹窗+备份；缺 BepInEx 直接代装。
- [决策：mod 元数据格式与目录约定](tickets/04-mod-metadata-convention.md): info.json（schema:1）+ 每 mod 一目录；顶层 id/type/guid/name/author/description/latest/dependencies + versions[]（SemVer 强制含预发布、released=tag 日期、files[].sha256+size、changelog）；readme 是唯一人工源、CI 解析并生成机器字段；BepInEx 按 guid 识别已装、缺失依赖提示+一键装；无元数据目录不收录。
- [调研：BepInEx 5 全自动代安装](tickets/03-bepinex-autoinstall-research.md): 用 BepInEx 5.4.23.5 x64 纯文件复制代装；AppID 674940；干净判定三层（buildid + 官方指纹表 + 本机首扫基线）+ steam://validate 降级；PE 头判位数；LogOutput.log 验证 + 一键回滚。

## Not yet specified

- 站上 BepInEx pack 实际内容与 64 位更新后的真实 exe/Data 命名需实机核对（子代理侧 404）

## Out of scope

- macOS 支持：后续版本再议
- 多语言 i18n：明确砍掉，中文 only
- 延续 FluentAvalonia：v12 官方导航组件已覆盖其存在理由
- 新建仓库：已定同仓库新分支
- mod 制作/打包工具链：本图只管「管理器」侧
- mod 仓库 CI 生成器的实现：属上游 mod 仓库侧，本图只定义契约（见工单 04 决议第 8 条）
- UVFS config.ini 的编辑（含 root_ 暗门开关）：明确不做，见工单「决策：配置编辑器」决议
