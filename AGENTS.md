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
  - `dotnet test Lunaqua.sln` —— 测试（xUnit v3 + Avalonia.Headless，9 个用例）
- 目录：`src/Lunaqua/`（Views / ViewModels / Services / Models / Resources）｜`tests/Lunaqua.Tests/`

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
- 站点开了 SignAll；**guest 有写权限（建议站长收紧）**
- 站上 9 个 mod 的 `info.json` 已由补录工具生成、站长上传并逐字节校验通过（`docs/backfill/` 是同份草案）
- 站上 BepInEx pack **已实机核对**：`/BepInEx/BepInEx_pack_x64_这个文件夹不应该出现.zip` = 官方 5.4.23.5 win_x64
  （22 文件哈希全对）+ 预置 `BepInEx/config/BepInEx.cfg`，共 23 文件；清单 `data/bepinex-pack.json`
- **验证 BepInEx 不用从 Steam 启动**：把游戏复制到临时目录（`cp -r` 442MB 约 2 秒）→ 部署 pack →
  直接跑 `StickFight.exe` → 25 秒后杀进程 → 看 `BepInEx/LogOutput.log`（实测出现
  `BepInEx 5.4.23.5 - StickFight` / `Bits64` / `Chainloader startup complete`，Unity 5.6.7）
- 开发机游戏目录 `C:\SteamLibrary\steamapps\common\StickFightTheGame`；Managed 里有一堆手改副本
  （`Assembly-CSharp.dll。blinkgod` 等），BepInEx 会当重复程序集加载并警告 —— 别当原版环境用

## 待办

1. **M6 打磨** —— 手动纳管（§7.5）、UVFS 冲突提示、错误话术、日志导出、测试补齐
2. **M5 的收尾（需要人工）** —— 静态更新目录的主机/路径/上传凭据（规格书 §13 风险 2）；
   配好后取消 `.github/workflows/release.yml` 上传段注释，并推一个 `vX.Y.Z` tag 验证流水线
3. **发布前自检** —— `dotnet vpk pack` 本地能出包（已验证）；真机验证「装旧版 → 自动更新 → 提示重启 → 回退」
   - 官方构建指纹表：`bash docs/tools/fingerprint/wizard.sh`（6 站向导，见 `docs/tools/fingerprint/README.md`）；
     当前 build `24952802` / depot `674945` 的元数据已登记在 `data/game-fingerprints.json`，哈希待采
   - 实机事实：安装目录 `StickFightTheGame`，但 exe 是 `StickFight.exe`、数据目录 `StickFight_Data`
2. **M5–M6** —— 见规格书 §12 里程碑表
   - 已完成：M0（骨架）、M1（站点与元数据）、M2（生命周期引擎 + 详情页动作）、M3（BepInEx 代装全链路）
   - 还没做：手动纳管（§7.5）、UVFS 冲突提示、错误话术、日志导出（M6）
3. 可选（站长自办）：DesyncFixer/xhighws 的 `name` 美化与 `author` 补全；收紧 OpenList guest 写权限；生成 674940 官方指纹表；实机核对站上 BepInEx pack

## 规矩

- 新增第三方依赖**必须回** `docs/wayfinder/tickets/13-tech-stack.md` 追加裁决
- 规格书与决议是唯一权威；旧项目（`archive/v1`）的技术选型**不作为依据**
- 提交信息用中文简述 + 作用域（如 `feat(m1): OpenList 客户端与 info.json 解析`），一个提交一件事
