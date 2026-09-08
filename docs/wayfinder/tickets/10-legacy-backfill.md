---
id: 10
title: 补录：存量 9 个 mod 的 info.json
labels: ["wayfinder:task"]
status: open
assignee: null
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
