# AGENTS.md — Lunaqua

「柴」（Stick Fight: The Game，Steam **AppID 674940**）的 mod 管理器，正在按规格书重写 v2。中文 only，Windows 优先。

## 先读什么

- **规格书（唯一权威）**：`docs/spec/lunaqua-v2-spec.md` —— 决策、契约、架构、里程碑全在里面
- **决策与调研**：`docs/wayfinder/map.md` + `tickets/` + `research/`（要 rationale 时再 zoom）
- **分支**：`main` = v2 开发主线（干净起点）｜`archive/v1` = 旧项目完整历史（只读）｜`prototype/ui-ia` = UI 原型（throwaway）

## 开发前

- 加载 **avalonia 系列 skill**（已装 `~/.dsh/skills/avalonia*`，33 个；重装：`bash docs/tools/install-avalonia-skills.sh`）
- 环境：.NET SDK **10.0.100**；NuGet 可达；Avalonia 12.1.2 已实测可还原可编译
- 运行：`cd <项目目录> && dotnet run`；无头渲染截图：`dotnet <dll> --render <输出目录> all`

## 环境与坑（文档之外的事实）

- **文件沙箱**：会话工作区 `E:\codes\Lunaqua` 可写；写工作区外需提权（Y 盘走 bash 可直接写）
- **Y 盘是网络盘**：远控时用它看文件（`Y:\lunaqua-spec\` 规格书、`Y:\lunaqua-prototype\` 截图、`Y:\lunaqua-docs-backup-*` 备份）
- `dotnet build` 偶发「Device or resource busy」：先 `dotnet build-server shutdown` 再删目录
- **视觉**：当前模型可能读不了图（`read_image` 报不支持、`describe_image` 返回空）→ 截图需人眼确认
- **子代理可能整体不可用**：调研改为会话内自行完成（curl + web 工具都可用）
- git 身份 `Yue-bin <3103174284@qq.com>`；origin 走 SSH，推送已验证可用

## 站与环境

- 上游站：`https://blog.monblog.top/openlist`（OpenList v4.2.2，匿名只读可用）——**API 怪癖见规格书 §5**（HTTP 恒 200 + code、无 `/api/fs/read`、无搜索、sign 每次重取）
- 站点开了 SignAll；**guest 有写权限（建议站长收紧）**
- 站上 BepInEx pack 实际内容 / 64 位更新后的 exe 与 Data 目录名**尚未实机核对**（规格书 §13 风险 1）

## 待办

1. **补录存量 9 个 mod 的 info.json** —— `docs/wayfinder/tickets/10-legacy-backfill.md`（拉站上 dll 解析 BepInPlugin/BepInDependency + 各 mod 仓库 readme 文案，生成草案待审）
2. **实现 M0–M6** —— 规格书 §12；M0 = net10 + Avalonia 12.1.2 + CTK + Serilog + MS.DI，DrawerPage 壳 + 空页面 + 设置持久化

## 规矩

- 新增第三方依赖**必须回** `docs/wayfinder/tickets/13-tech-stack.md` 追加裁决
- 规格书与决议是唯一权威；旧项目（`archive/v1`）的技术选型**不作为依据**
- 提交信息用中文简述 + 作用域（如 `feat(m1): OpenList 客户端与 info.json 解析`）
