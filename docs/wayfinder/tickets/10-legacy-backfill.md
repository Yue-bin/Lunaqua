---
id: 10
title: 补录：存量 9 个 mod 的 info.json
labels: ["wayfinder:task"]
status: closed
assignee: agent (2026-09-09)
blocked-by: [04]
---

## Question（task）

站上现有 9 个 stick.plugins.* / z7572.* 目录没有 info.json，而工单「决策：mod 元数据格式与目录约定」已定：无元数据目录管理器不收录。所以需要一次性补录。

做法（agent 可代做草案）：
1. 用 OpenList API 列每个 mod 目录（匿名 fs/list + fs/get 拿 raw_url 下载 dll）；
2. 解析 dll 的 BepInPlugin（GUID/Version/Author）与 BepInDependency；
3. 从各 mod 仓库的 readme.md（git.monblog.top/Stick_Mods/<mod>）解析 name/author/description/changelog；
4. 计算各 dll 的 sha256/size，按工单 04 的 schema 生成 9 份 info.json 草案；
5. 人工审核后由站长 SFTP 推到对应目录。

注意：z7572.DesyncFixer 目录里的旧 dll（NaNFixer，已改名换 GUID）按约定**直接删除**，不写进 versions[]。

完成判据：9 个目录都有合规 info.json，且用管理器能列出并显示名称/作者/版本。
## Resolution（2026-09-09，agent 代做）

**产物**：9 份可直接落站的 `info.json` + `SUMMARY.md`
- Y 盘：`Y:\lunaqua-backfill\`（+ `lunaqua-backfill-20260909.tar.gz`）
- 仓库副本：`docs/backfill/`
- 生成工具：`docs/tools/backfill/`（C# + Mono.Cecil，可重跑：`dotnet run -- <站URL> <输出目录> <Gitea org>`）

**生成方式**：OpenList 匿名 API 列目录 → `fs/get` 下载各目录全部历史 dll → Mono.Cecil 解析 BepInPlugin（GUID/Name/Version）与 BepInDependency → Gitea API 取各 mod 仓库 readme（自动探测默认分支 master/main，尝试 readme.md/README.md）→ 按工单 04 的 schema 组装（含每版本 files[].sha256/size、changelog 取 readme 对应小节）。

**结果**：9/9 目录各一份，共 30 个版本 / 42 个文件记录；7 份文案（name/author/description/changelog）自动从 readme 提取。

**待人工确认（4 条）**：
1. `stick.plugins.playermanager`：文件 `…-v4.0.7.dll` 的内嵌版本是 **4.0.6**（上游打包笔误）；
2. `stick.plugins.xhighws`：文件 `…-v1.0.0.dll` 的内嵌版本是 **0.0.1**；
3. `z7572.DesyncFixer`：Gitea 上**没有仓库** → 无 readme，name 取 dll 名、author/description 为空，需人工补文案；
4. `z7572.DesyncFixer` 目录里的 `NaNFixer-v0.0.1.dll` 按约定跳过，**需从站上删除**。

**另注**：`released` 用站上文件 modified 时间近似（补录拿不到 git tag 日期）。

**推送**：把每个子目录的 info.json 用 SFTP 推到站上对应 mod 目录根部（与 dll 同级），刷新管理器验证。