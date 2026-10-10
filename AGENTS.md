# AGENTS.md — Lunaqua

「柴」（Stick Fight: The Game，Steam **AppID 674940**）的 mod 管理器，正在按规格书重写 v2。中文 only，Windows 优先。

## 先读什么

- **规格书（唯一权威）**：`docs/spec/lunaqua-v2-spec.md` —— 决策、契约、架构、里程碑全在里面
- **决策与调研**：`docs/wayfinder/map.md` + `tickets/` + `research/`（要 rationale 时再 zoom）
- **分支**：`main` = v2 开发主线｜`archive/v1` = 旧项目完整历史（只读）｜`prototype/ui-ia` = UI 原型（throwaway）

## 开发前

- 加载 **avalonia 系列 skill**（已装 `~/.dsh/skills/avalonia*`，33 个；重装：`bash docs/tools/install-avalonia-skills.sh`）
- 环境：.NET SDK **10.0.100**；NuGet 可达；Avalonia 12.1.2 已实测可还原可编译
- 常用命令：
  - `dotnet build Lunaqua.sln` —— 编译（`TreatWarningsAsErrors`，必须 0 警告）
  - `dotnet run --project src/Lunaqua` —— 跑桌面版
  - `dotnet test Lunaqua.sln` —— 测试（xUnit v3 + Avalonia.Headless，**174 个用例**；只有设了 `LUNAQUA_TEST_*` 环境变量时才跑那 3 个 live 用例）
- 目录：`src/Lunaqua/`（Domain / Infrastructure / Services / ViewModels / Views）｜`tests/Lunaqua.Tests/`
- 状态（2026-10-11）：**M0–M5 已完成**；M6 打磨在做；M7（站点发布侧/上传服务）与 M8（跨平台）见规格书 §12/§15/§14

## 环境与坑（文档之外的事实）

- **文件沙箱**：会话工作区 `E:\codes\Lunaqua` 可写；写工作区外需提权（Y 盘走 bash 可直接写）
- **Y 盘是网络盘**：远控时用它看文件（`Y:\lunaqua-spec\` 规格书、`Y:\lunaqua-prototype\` 原型截图、`Y:\lunaqua-m0\` M0 出壳截图）
- `dotnet build` 偶发「Device or resource busy」：先 `dotnet build-server shutdown` 再删目录
- **`dotnet test` 在 .NET 10 必须走 MTP**：仓库根有 `dotnet.config`（`[dotnet.test:runner] name = "Microsoft.Testing.Platform"`），删了就会报「VSTest target is no longer supported」
- **xunit.v3 锁 3.2.2**：`Avalonia.Headless.XUnit 12.1.2` 依赖 `xunit.v3.extensibility.core 3.2.2`；升 4.0.0 会在测试发现阶段 `MissingMethodException`（M0 实测，见工单 13 的 Amendment）
- **Avalonia 12 API 变动**（M0 踩过）：`Bitmap.Save(string)` 已过时 → 传 `PngBitmapEncoderOptions.Default`；`TextBox.Watermark` → `PlaceholderText`；编译绑定默认开启（视图必须写 `x:DataType`）
- **视觉**：当前模型可能读不了图（`read_image` 报不支持、`describe_image` 返回空）→ 截图需人眼确认；无头渲染测试里用「颜色数 > 100」代替人眼判空白帧
- **子代理可能整体不可用**：调研改为会话内自行完成（curl + web 工具都可用）
- git 身份 `Yue-bin <3103174284@qq.com>`；origin 走 SSH，推送已验证可用

## 站与环境

- 上游站：`https://blog.monblog.top/openlist`（OpenList v4.2.2，匿名只读可用）——**API 怪癖见规格书 §5**（HTTP 恒 200 + code、无 `/api/fs/read`、无搜索、sign 每次重取）
- 站点开了 SignAll；**guest 无写权限**（匿名 `GET /api/me` → `permission: 20482` = 免密访问+读压缩包+分享，写位全 0；
  `fs/put`/`fs/mkdir`/`fs/rename` 匿名实测 `code 403 permission denied`，WebDAV `PROPFIND /dav/` 401）
  - ⚠️ `fs/list` 响应里的 `write:true` **不是**当前用户的写权限（站点/存储维度字段）—— 本文档与规格书旧版都据此误判过
  - ⚠️ API 路径相对该账号的 `base_path`：guest 是 `/stick_mods`，所以**列站根要传 `path:"/"`**（传 `/stick_mods` 会 `code 500 object not found`）
  - 站上写操作（哪怕只是建个测试目录）**先问站长**，别自己动手
- 站上 9 个 mod 的 `info.json` 已由补录工具生成、站长上传并逐字节校验通过（`docs/backfill/` 是同份草案）
- 站上 BepInEx pack **已实机核对**：`/BepInEx/BepInEx_pack_x64_这个文件夹不应该出现.zip` = 官方 5.4.23.5 win_x64
  （22 文件哈希全对）+ 预置 `BepInEx/config/BepInEx.cfg`，共 23 文件；清单 `data/bepinex-pack.json`
- **验证 BepInEx 不用从 Steam 启动**：把游戏复制到临时目录（`cp -r` 442MB 约 2 秒）→ 部署 pack →
  直接跑 `StickFight.exe` → 25 秒后杀进程 → 看 `BepInEx/LogOutput.log`（实测出现
  `BepInEx 5.4.23.5 - StickFight` / `Bits64` / `Chainloader startup complete`，Unity 5.6.7）
- 开发机游戏目录 `C:\SteamLibrary\steamapps\common\StickFightTheGame`；Managed 里有一堆手改副本
  （`Assembly-CSharp.dll。blinkgod` 等），BepInEx 会当重复程序集加载并警告 —— 别当原版环境用

## 待办

1. **M6 打磨（在做）** —— 手动纳管（§7.5）、UVFS 冲突提示、错误话术、日志导出、测试补齐
2. **M7 发布侧（下一步，规格书 §15）** —— 上传页 + 发布服务：`https://mod.monblog.top/upload`；
   服务端从 **dll 内嵌 BepInPlugin** + readme 派生元数据、**同版本冲突一律打回**、
   组合包拆成 `bepinex` + `uvfs` 两条目（后者 `dependencies`）；契约与坑见 `docs/wayfinder/research/15-mod-publishing.md`
3. **M8 跨平台（规格书 §14）** —— Linux/Proton 成本低（同一份 Windows 构建，零重编译）；macOS 是原生构建、
   UVFS 不可能支持；事实见 `research/14-platform-support.md`
4. **M5 的收尾（需要人工）** —— 静态更新目录的主机/路径/上传凭据（规格书 §13 风险 2）；
   配好后取消 `.github/workflows/release.yml` 上传段注释，并推一个 `vX.Y.Z` tag 验证流水线
5. **发布前自检（需要人工）** —— `dotnet vpk pack` 本地能出包（已验证）；真机验证「装旧版 → 自动更新 → 提示重启 → 回退」
6. **只能人做的实测** —— ① macOS 装 BepInEx 跑通一个现有插件；② Linux/Proton 的 `winhttp` override；
   ③ **UVFS 在 Windows x64**（线上是 x64/Unity 5.6.7，而 UVFS v1 只在 x86/5.6.4 验证过）；
   ④ UVFS 在 Wine 下能否 MinHook；⑤ 发布账号与 token；⑥ 站上实测 `fs/rename` 能否对目录操作
   - 官方构建指纹表：`bash docs/tools/fingerprint/wizard.sh`（6 站向导，见 `docs/tools/fingerprint/README.md`）；
     当前 build `24952802` / depot `674945` 的元数据已登记在 `data/game-fingerprints.json`，哈希待采
   - 实机事实：安装目录 `StickFightTheGame`，但 exe 是 `StickFight.exe`、数据目录 `StickFight_Data`
7. 可选（站长自办）：DesyncFixer/xhighws 的 `name` 美化与 `author` 补全

## 规矩

- 新增第三方依赖**必须回** `docs/wayfinder/tickets/13-tech-stack.md` 追加裁决
- 规格书与决议是唯一权威；旧项目（`archive/v1`）的技术选型**不作为依据**
- 提交信息用中文简述 + 作用域（如 `feat(m1): OpenList 客户端与 info.json 解析`），一个提交一件事
