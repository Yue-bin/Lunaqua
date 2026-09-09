# Lunaqua v2 重启规格书

- 版本：draft v1 · 2026-09-09
- 状态：决策已全部落定（wayfinder 地图 11 张工单），本文档是**可施工版本**
- 决议原文与调研资产：`docs/wayfinder/`（地图 `map.md`、工单 `tickets/`、调研 `research/`）
- UI 原型（丢弃型）：分支 `prototype/ui-ia`，截图 `Y:\lunaqua-prototype\`

---

## 1. 目标与非目标

**是什么**：「柴」= *Stick Fight: The Game*（Steam **AppID 674940**）的 mod 管理器，服务中文玩家（含连解压都不会的小白）。

**必须做到**：
- 从自建 OpenList 站浏览 / 搜索 / 下载 mod；
- 一键安装到 BepInEx/plugins，支持启停、卸载、更新、版本回退；
- **全自动代装 BepInEx 5**（不是教程引导，是代操作）；
- 内置**配置编辑器**（不手改 .cfg）；
- 管理两类 mod：BepInEx 插件（dll）与 UVFS 覆盖层资源 mod；
- 自身自动更新（Velopack 差分）。

**明确不做**：多游戏 / 多实例、多语言（中文 only）、macOS（后续版本）、UVFS `root_` 暗门、自动更新 mod（必须玩家点）、mod 制作与打包工具链。

> 依据：工单 01–08、11–13；原型决议见工单 07。

---

## 2. 技术栈与依赖清单（BOM）

| 用途 | 选择 | 版本 | 许可 |
|---|---|---|---|
| UI 框架 | Avalonia + Avalonia.Desktop + Avalonia.Themes.Fluent + Avalonia.Fonts.Inter | 12.1.2 | MIT |
| 目标框架 | **net10.0** | — | — |
| MVVM | **CommunityToolkit.Mvvm** | 8.4.2 | MIT |
| 图标 | FluentIcons.Avalonia | 2.1.339.1 | MIT |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT |
| 日志 | Serilog + Serilog.Sinks.File | 4.4.0 / 7.0.0 | Apache-2.0 |
| 程序集元数据 | Mono.Cecil | 0.11.6 | MIT |
| 压缩 | System.IO.Compression（内置，仅 zip） | — | — |
| HTTP/JSON | **自研** HttpClient + System.Text.Json | — | — |
| BepInEx .cfg 解析 | **自研**行式解析/写回 | — | — |
| Steam 库定位 | **自研** KeyValues 解析（libraryfolders.vdf） | — | — |
| 测试 | xUnit v3 + Avalonia.Headless.XUnit | **3.2.2** / 12.1.2 | Apache-2.0 / MIT |
| 打包/自更新 | **Velopack** | 1.2.0 | MIT |

**不用**：ReactiveUI、FluentAvalonia、AListSdkSharp、Flurl、Refit、Tomlyn、Svg.Skia、HotAvalonia、Avalonia.Xaml.Behaviors、MsgBox.Avalonia、I18N.Avalonia。

**原则**：优先内置；新增依赖必须回到工单「决策：技术栈与依赖清单」追加裁决。

> xUnit v3 锁 **3.2.2**（不是 4.0.0）：`Avalonia.Headless.XUnit 12.1.2` 依赖 `xunit.v3.extensibility.core 3.2.2`，升到 4.0.0 后 `[AvaloniaFact]` 在测试发现阶段抛 `MissingMethodException`（M0 实测）。

> 依据：工单 13（BOM）、01（Avalonia 12 迁移注意点）、02（客户端选型）。

---

## 3. 术语与领域模型

| 术语 | 定义 |
|---|---|
| **mod** | 站上一个目录 + 目录内的 `info.json`。**没有 info.json 的目录不是可管理 mod** |
| **mod 类型** | `bepinex`（插件 dll）/ `uvfs`（覆盖层资源）/ `uvfs-framework`（UVFS 框架本体，特例） |
| **安装根** | 该类型文件落位的根：bepinex → 游戏根 `BepInEx/plugins/`；uvfs → `overlay/mods/<id>/`；uvfs-framework → 内置映射表 |
| **凭证** | 安装时落盘的 info.json 快照（记录实际落位清单、来源、启停状态、忽略版本）；识别/启停/卸载都读它 |
| **站** | 站长自运营的 OpenList 实例 `blog.monblog.top/openlist` |
| **latest / versions[]** | 元数据里的最新版本指针与全部历史版本 |
| **忽略版本** | 玩家回退后选择不再提示的那个版本（记在本地凭证） |
| **外部 mod** | 玩家手动装、管理器不认识的 mod（BepInEx 按 GUID 纳管；UVFS 无元数据目录直接忽略） |

---

## 4. 元数据契约（站侧）

### 4.1 目录约定
站上每个 mod 一个目录，目录根部放 `info.json`。可选素材：`icon.png`（客户端自行缩放）、`readme.md`（详情页渲染）。

### 4.2 `info.json` schema

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `schema` | int | ✓ | 固定 1 |
| `id` | string | ✓ | **必须等于目录名**；不一致 → 以元数据 id 记账 + 界面标黄警告，仍可管理 |
| `type` | string | ✓ | `bepinex` \| `uvfs` \| `uvfs-framework` |
| `guid` | string | bepinex/uvfs-framework 必填 | CI 从 dll 的 BepInPlugin 解析 |
| `name` / `author` / `description` | string | ✓ | 全部来自 readme（见 4.4）；description 为纯文本短文案。**`author` 为空时容忍**（界面显示「未知作者」，仅告警）：站上 8/9 个存量 mod 的 readme 没有作者小节 |
| `latest` | string | ✓ | 必须是 `versions[]` 中存在的版本号 |
| `dependencies` | array | ✓（可为空） | `[{ id, minVersion? }]`，CI 从 BepInDependency 解析 |
| `versions` | array | ✓ | 每项 `{ version, released, files[], changelog? }` |
| `versions[].version` | string | ✓ | **SemVer 2.0.0（含 `-rc.1` 预发布后缀）**；必须同时等于 dll 内嵌 BepInPlugin.Version 与 git tag，任一不一致 **CI 直接失败** |
| `versions[].released` | string | ✓ | git tag 日期（ISO 8601） |
| `versions[].files` | array | ✓ | `{ path, sha256, size }`；path 相对 mod 目录；sha256 小写 hex；size 字节 |
| `versions[].changelog` | string | — | readme「## 更新日志」中对应 `### vX.Y.Z` 小节 |

### 4.3 示例

```json
{
  "schema": 1,
  "id": "stick.plugins.playermanager",
  "type": "bepinex",
  "guid": "stick.plugins.playermanager",
  "name": "玩家管理器",
  "author": "老狼老狼几点钟、Moncak、z7572",
  "description": "房间查找、转让房主、玩家记录、kick、拉黑、静音、一键加好友、最近一起玩的玩家列表。",
  "latest": "5.1.0",
  "dependencies": [],
  "versions": [
    { "version": "5.1.0", "released": "2026-08-31",
      "files": [{ "path": "stick.plugins.playermanager.dll", "sha256": "9f2c…", "size": 123456 }],
      "changelog": "1. 添加了被kick提示\n2. 一些小重构和规范化" }
  ]
}
```

### 4.4 readme 是唯一人工源
CI 解析 readme 生成文案字段：H1 → `name`；`## 作者` → `author`；`## 功能` → `description`；`## 更新日志` 的 `### vX.Y.Z` → `versions[].changelog`。**人工只维护 readme**。

### 4.5 CI 生成器契约（在 mod 仓库侧执行）
1. 解析 dll：BepInPlugin（GUID/Version/Author）、BepInDependency；
2. 校验 SemVer 与「dll 内嵌版本 == git tag == info.json version」，任一不符 → 失败；
3. 解析 readme 文案；
4. 计算各文件 sha256/size；
5. 追加/更新 `versions[]` 与 `latest`；
6. 随 dll 一起 SFTP 推送 `info.json`。

### 4.6 存量迁移
现有 9 个目录一次性补录（工单「补录：存量 9 个 mod 的 info.json」）；**管理器不兼容无元数据目录**。改名换 GUID 不兼容（旧 dll 直接删）。

> 依据：工单 04。

---

## 5. 站点与 API 契约（OpenList）

### 5.1 需要的端点
| 能力 | 端点 |
|---|---|
| 站点信息 | `POST /api/public/settings`（匿名） |
| 列目录 | `POST /api/fs/list` `{path,page,per_page}`（匿名，**必须显式传 per_page**） |
| 取文件信息/直链 | `POST /api/fs/get` → `raw_url`（含 sign，直接 GET 即可下载） |
| 下载 | `GET raw_url`；支持 `Range`（206 续传）与 `ETag/If-None-Match`（304 条件请求） |

### 5.2 必须防的坑
1. **`/api/*` 一律 HTTP 200，业务错误在 JSON `code` 字段** → 先解析 JSON 再判 `code`；未知端点会返回 **HTML**（解析失败即视为错误）。
2. **没有 `/api/fs/read`** → 读 readme 必须 `fs/get` 拿 raw_url 再 GET；`fs/list` 的 `readme` 字段恒空。
3. **没有搜索**（本站 `search_index=none`，`code 404`）→ 搜索走本地缓存的目录树。
4. **sign 每次会话重取，不要持久化**（站长轮换 Token 会让旧 sign 全部失效）；本站 sign 为 `:0`（不过期）。
5. **真实路径含 guest base_path `/stick_mods`** → 拼 `/d/` 必须用真实路径；用 `raw_url` 最省事。
6. **中文文件名**：`Content-Disposition` 的 `filename=` 是百分比编码，`filename*=utf-8''` 才是明文。
7. 并发控制在 2–4，失败指数退避（服务端有限流中间件）。
8. 安全建议：本站 guest 有写权限（`write:true`），**建议站长收紧**；客户端只读，永不调用写端点。

> 依据：工单 02（含完整实测表）。

---

## 6. 架构

```
Views (Avalonia .axaml)
   ↑ 绑定
ViewModels (CommunityToolkit.Mvvm: ObservableProperty / RelayCommand)
   ↑
Services（编排）：ModRepository / InstallEngine / BepInExInstaller / ConfigEditorService / UpdateService
   ↑
Domain：MetadataInfo / ModVersion / InstalledMod / IModTypeStrategy / InstallPlan
   ↑
Infrastructure：OpenListClient / MetadataClient / DllInspector(Mono.Cecil) / CfgParser / SteamLocator / VdfParser / FileTransaction / CacheStore
```

### 6.1 薄策略层（类型抽象）
```csharp
interface IModTypeStrategy
{
    string Type { get; }                                  // bepinex | uvfs | uvfs-framework
    string ResolveInstallRoot(GameContext game, string modId);
    InstallPlan PlanInstall(MetadataInfo meta, ModVersion version);  // 文件 → 目标路径
    InstalledState Detect(GameContext game, MetadataInfo meta);      // 已装识别（磁盘实况优先）
    void Enable/Disable(GameContext game, InstalledMod mod);         // 移出/移回
    IReadOnlyList<string> Validate(MetadataInfo meta);               // 路径穿越、豁免、结构校验
}
```
- `bepinex`：文件落 `BepInEx/plugins/<path>`；按 dll GUID 识别。
- `uvfs`：文件落 `overlay/mods/<id>/<path>`（只允许 `data/`、`bundles/` 作用域）；按凭证识别；启停 = `overlay/mods/<id>/` ↔ `overlay/mods.disabled/<id>/`（UVFS 会枚举 mods 下全部目录，改名无效）。
- `uvfs-framework`：**管理器内置映射表**（BepInEx/patchers/Overlay.NativeLoader/{overlay32,overlay64,Overlay.NativeLoader}.dll、BepInEx/plugins/Overlay.Managed.dll、BepInEx/plugins/XUnity.ResourceRedirector/*）；站上结构与映射不符 → 拒绝安装并提示升级管理器。

### 6.2 服务边界
- `ModRepository`：站上索引（目录树缓存）+ 本地已装状态合并，供 UI 查询；
- `InstallEngine`：事务式安装/更新/回退/卸载/启停的唯一入口；
- `BepInExInstaller`：代装状态机；
- `ConfigEditorService`：.cfg 解析/写回/备份；
- `UpdateService`：Velopack 自更新 + 回退。

> 依据：工单 05、06、07。

---

## 7. 本地生命周期

### 7.1 状态模型
- **凭证**：bepinex/uvfs-framework → `<游戏>/.lunaqua/<id>/info.json`；uvfs → `overlay/mods/<id>/info.json`。含：落位清单、来源（official / manual-adopted / unknown-version）、启停状态、忽略版本。
- **磁盘实况优先**：已装版本以磁盘为准（BepInEx 读 dll GUID+Version；UVFS 读凭证 version）。

### 7.2 安装（事务式）
解析 info.json → 依赖检查（缺失提示 + 一键装；缺 BepInEx → 直接进入代装）→ 目标同名文件弹窗（覆盖/跳过）→ 全部文件下到临时区并 sha256 校验（失败自动重试一次）→ 备份到 `<游戏>/.lunaqua/backup/<时间戳>/` → 逐个替换 → 写凭证。**任一步失败自动回滚**。

### 7.3 更新 / 回退
- 启动时后台检查全部已装 mod → 列表标「可更新」→ 玩家点更新 → **就地更新**（差集删除旧版多余文件）；
- 详情页版本下拉列出 `versions[]` 全部版本并标记当前已装；回退后仍提示更新，但可「忽略此版本」。

### 7.4 启停 / 卸载
- 启停：bepinex/uvfs-framework 整体移到 `<游戏>/.lunaqua-disabled/<id>/`；uvfs 移出/移回 `mods.disabled`；
- 卸载：反查 `dependencies[]` 提示「X 依赖它」→ 弹窗问是否保留配置 → 删凭证记录的文件 → 删凭证。

### 7.5 手动纳管与缓存
- BepInEx：扫 `BepInEx/plugins` 无凭证 dll → 按 GUID 匹配站上 mod → 提示纳管；版本对不上标「未知版本 / 外部修改」，默认不提供更新；
- UVFS：无 info.json 目录**忽略**；
- 缓存：`%LocalAppData%\Lunaqua\cache` 按 sha256 复用，提供「清理缓存」；
- 并发：串行任务队列 + 全局进度，可取消；
- 游戏运行中（检测 `StickFight.exe` / `StickFightTheGame.exe`）：拒绝安装/卸载/启停并提示关游戏。

> 依据：工单 05、06。

---

## 8. BepInEx 全自动代安装

**对外三步**（UI）：
1. **找到游戏** —— 注册表 `HKCU\SOFTWARE\Valve\Steam\SteamPath`（兜底 HKLM）→ `config/libraryfolders.vdf` → 各库 `appmanifest_674940.acf`（读 installdir/buildid/StateFlags=4）→ 结构校验（`StickFight.exe` 或 `StickFightTheGame.exe` + `<exe>_Data\Managed\Assembly-CSharp.dll`）+ **PE 头判位数（必须 x64）**；失败 → 手动选目录。
2. **自动安装** —— 干净判定三层 → 备份 → 部署 **BepInEx 5.4.23.5 win_x64**（22 文件，纯复制）→ 逐文件 sha256 自校验 → ini 键格式断言。
3. **验证** —— 启动游戏（`steam://rungameid/674940`）→ ≤60s 轮询 `BepInEx\LogOutput.log` 出现 `BepInEx 5.4.23`；失败读日志尾部分类（位数/杀软/Harmony）→ **一键回滚**。

**干净判定三层**：① `appmanifest` 的 buildid 命中官方指纹表 → 通过；② 否则比 `Assembly-CSharp.dll` 等 SHA-256 与官方指纹表；③ 都不命中 → 引导 `steam://validate/674940` → 复扫 → 仍不命中给「上报哈希」/「信任并继续（user-trusted）」双按钮。指纹表由维护者用 DepotDownloader `-manifest-only -filelist` 生成、随 Lunaqua 发布。

**备份**：`winhttp.dll` / `doorstop_config.ini` / `.doorstop_version` / 已有 `BepInEx` 目录 → `%LocalAppData%\Lunaqua\backups\<日期>` + manifest.json。

**失败话术**（小白向）：找不到游戏→选目录；32 位→先更新游戏；陌生 winhttp.dll→已备份后覆盖；文件不干净→自动调 Steam 修复；杀软拦截→加白名单重试（图文）；首启无日志→收集日志 + 一键还原。

> 依据：工单 03（含 pack 清单与源码级参考）。

---

## 9. 配置编辑器

- **范围**：只编辑 `<游戏>/BepInEx/config/*.cfg`；UVFS `config.ini` 不碰。
- **关联 mod**：以 .cfg 头部 `## Plugin GUID:` 为准（文件名通常是 `<guid>.cfg`，不假设）；全局页兜底列出全部 .cfg（含未关联）。
- **解析/写回**：自写行式解析器；识别 `## 描述`、`# Setting type:`、`# Default value:`、`# Acceptable values:` / `# Acceptable value range:`、`[Flags]` 提示行；写回**只替换值行**，保留注释/顺序/空行；UTF-8 无 BOM。
- **控件映射**：Boolean→开关；整数→数字框（按 range 限幅）；Single/Double/Decimal→数字框（InvariantCulture）；String→文本框；enum→下拉；[Flags]→多选（逗号分隔）；**KeyboardShortcut→快捷键捕获**（格式 `LeftControl + K`，未设置 `Not set`，键名用 **Unity 5.6.x KeyCode 表**）；**Color→Avalonia 取色盘**（hex RGBA 如 `FF00FFFF`）；其它类型→文本框 + 校验。
- **行为**：即改即存 + 「已保存」；非法值拒绝写入并就地提示；游戏运行中→只读 + 提示；每条目「重置」+ 每文件「全部重置」；**「只看已改动项」过滤**；首次编辑前自动备份到 `<游戏>/.lunaqua/backup/config/<guid>/<时间戳>.cfg`；顶部常驻「配置在下次启动游戏时生效」。
- **未运行过的 mod**：没有 .cfg → 提示「尚未生成配置，先启动一次游戏」+「启动游戏」按钮。

> 依据：工单 11（源码级格式取证）、12。

---

## 10. UI 信息架构

- **外壳**：官方 `DrawerPage`，`DrawerLayoutBehavior=CompactInline`、`CompactDrawerLength=48`、`DrawerLength=230`。
- **页面**：
  1. **首页**：三张状态卡（游戏目录 / BepInEx / 可更新数）+ 四个快捷操作（安装·修复 BepInEx、检查更新、打开 mod 目录、启动游戏）+ 最近更新；
  2. **Mod 库**：页内左右分栏（左 400px 列表 + 搜索 + 过滤，右详情）；
  3. **Mod 详情**：概览（描述 + readme 渲染）/ 配置 / 版本（版本下拉 + 历史 + 忽略此版本）三 tab；
  4. **BepInEx 代装**：三步 Expander（默认折叠，点开看细节）；
  5. **设置**：游戏目录、外观（跟随系统/浅/深）、缓存清理、备份目录、关于/更新（含「回退到上一版」）。
- **实现注意**（Avalonia 12）：编译绑定默认开启（补 `x:DataType`）；`TextBox.Watermark` → `PlaceholderText`；主题切换用 `RequestedThemeVariant` + `ThemeDictionaries` + `DynamicResource`。

> 依据：工单 07（原型 + 截图）、01。

---

## 11. 分发与自更新

- **首发**：OpenList 站根 `Lunaqua/` 目录 —— 最新 `Setup.exe` + `readme.md`（安装/更新/卸载/回退说明），**只保留最近 3 个版本**（该目录无 info.json，管理器按约定忽略）。
- **自动更新源**：自建静态目录（如 `https://blog.monblog.top/lunaqua/updates`，Caddy 托管、**无 sign**），Velopack `SimpleWebSource` 读 `releases.win.json` + 差分包。**不挂 OpenList 直链**（SignAll + Token 轮换风险）。
- **行为**：启动后台检查 → 有新版提示「下载并重启」；单稳定通道；设置页「回退到上一版」（Velopack 指定版本更新）。
- **流水线**：GitHub Actions，tag 触发 → `dotnet publish` → `vpk pack` → 上传静态目录 + 站上 `Lunaqua/`；**版本号权威 = git tag**；Setup 内置更新源 URL。

> 依据：工单 08、13。

---

## 12. 落地计划（里程碑切片）

| 里程碑 | 内容 | 验收 |
|---|---|---|
| **M0 骨架** | 新分支 `rewrite/v2`；net10 + Avalonia 12.1.2 + CTK + Serilog + MS.DI；DrawerPage 壳 + 空页面；设置持久化 | `dotnet run` 出壳，日志落文件，设置能存能读 |
| **M1 站点与元数据** | OpenListClient（列目录/取直链/下载）；info.json 解析 + 校验；Mod 库列表/搜索/详情 | 站上 9 个 mod 正确列出（含 id 不一致标黄），搜索可用 |
| **M2 生命周期** | 事务式安装/更新/回退/启停/卸载；凭证；类型策略；缓存；串行队列；游戏运行检测 | 装/更/卸/启停全通，中途杀进程能回滚；私改版被正确标「未知版本」 |
| **M3 BepInEx 代装** | 定位/PE 判位数/哈希三层/备份/部署/验证/回滚 + 三步 UI | 干净机器上一键装好并能启动验证；脏文件走 Steam 修复路径 |
| **M4 配置编辑器** | .cfg 解析/写回；控件映射（含 KeyboardShortcut/Color）；过滤/重置/备份/只读 | 改 playermanager 的 GuiScale 生效且注释不丢；游戏运行中拒绝写 |
| **M5 自更新** | Velopack 打包；GH Actions tag 流水线；静态源；站上目录；回退按钮 | 装旧版能自动更新到新版并提示重启；回退按钮可用 |
| **M6 打磨** | UVFS 冲突提示、手动纳管、错误话术、测试补齐、日志导出 | 覆盖率达标；小白可用性走查通过 |

**测试重点**（xUnit v3 + Avalonia.Headless.XUnit）：info.json 解析与校验、.cfg 解析/写回保真、事务引擎回滚、sha256 校验、路径穿越拒绝、类型策略落位。

---

## 13. 风险与开放项

1. 站上 BepInEx pack 的实际内容与 64 位更新后的真实 exe/Data 命名**需实机核对**（调研子代理侧 404）；
2. 静态更新目录的**主机/路径/上传凭据**待定（建议与 mod CI 的 SFTP 同源）；
3. 官方构建指纹表的**生成与维护流程**（需持有游戏的账号跑 DepotDownloader）；
4. 站上 **guest 写权限建议收紧**（安全）；
5. 便携版与 Velopack 自动更新不兼容（本规格书只做 Setup.exe）；
6. FluentIcons 与 Serilog 的版本升级需回归（依赖精确锁定）。

---

## 附录 A：决议索引

| 工单 | 文件 |
|---|---|
| 调研：Avalonia 12 迁移与生态摸底 | `docs/wayfinder/tickets/01-avalonia12-research.md`（+ `research/01-avalonia12.md`） |
| 调研：OpenList 客户端与 API 能力 | `tickets/02-openlist-client-research.md`（+ `research/02-openlist-api.md`） |
| 调研：BepInEx 5 全自动代安装 | `tickets/03-bepinex-autoinstall-research.md`（+ `research/03-bepinex-autoinstall.md`） |
| 决策：mod 元数据格式与目录约定 | `tickets/04-mod-metadata-convention.md` |
| 决策：双 mod 类型架构抽象 | `tickets/05-multi-mod-type-architecture.md` |
| 决策：mod 本地生命周期机制 | `tickets/06-mod-local-lifecycle.md` |
| 原型：信息架构与 UI | `tickets/07-ui-prototype.md` |
| 决策：Lunaqua 自身分发与更新渠道 | `tickets/08-self-distribution.md` |
| 调研：BepInEx 配置解析与配置编辑器 | `tickets/11-config-editor-research.md`（+ `research/11-bepinex-config-editor.md`） |
| 决策：配置编辑器 | `tickets/12-config-editor.md` |
| 决策：技术栈与依赖清单 | `tickets/13-tech-stack.md` |
| 补录：存量 9 个 mod 的 info.json | `tickets/10-legacy-backfill.md`（待办） |

## 附录 B：资产

- 地图：`docs/wayfinder/map.md`
- UI 原型（丢弃型）：分支 `prototype/ui-ia`；截图 `Y:\lunaqua-prototype\`
- Avalonia 12 技能包：`~/.dsh/skills/avalonia*`（33 个，实现阶段先加载）
- 安装脚本：`docs/tools/install-avalonia-skills.sh`
