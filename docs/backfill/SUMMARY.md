# Lunaqua v2 — 存量 9 个 mod 的 info.json 补录草案

- 生成时间：2026-09-09 06:53
- 来源站：https://blog.monblog.top/openlist（OpenList）
- 文案来源：https://git.monblog.top/api/v1/orgs/Stick_Mods/repos 的 readme.md
- 目录：本目录下每个 mod 一个子目录，内含可直接落站的 info.json

## 人工审核要点

1. **released 字段**用站上文件的 modified 时间近似（补录拿不到 git tag 日期）——如需精确，改为 tag 日期后重推。
2. **name/author/description/changelog** 从 readme 解析；缺 readme 的 mod 需人工补文案。
3. **非 SemVer 版本号**会在下方列出；按规格书 §4.2 要求应统一为 SemVer。
4. 非 dll 附件（如 StreamingAssets.zip）默认挂在最新版；如属特定版本请手动调整。
5. 历史上改名换 GUID 的 dll（如 NaNFixer）按约定**不写入 versions[]**，需从站上删除。

## 概览（9 个目录）

## stick.plugins.alwaysst
- guid: stick.plugins.alwaysst｜latest: **1.0.1**｜版本数: 1｜文件数: 2
- 文案来源: Gitea/stick.plugins.alwaysst
- ✅ 无警告

## stick.plugins.chatrecorder
- guid: stick.plugins.chatrecorder｜latest: **1.4.0**｜版本数: 4｜文件数: 5
- 文案来源: Gitea/stick.plugins.chatrecorder
- ✅ 无警告

## stick.plugins.cntext
- guid: stick.plugins.cntext｜latest: **1.0.3**｜版本数: 3｜文件数: 5
- 文案来源: Gitea/stick.plugins.cntext
- ✅ 无警告

## stick.plugins.fuckcustommap
- guid: stick.plugins.fuckcustommap｜latest: **1.0.1**｜版本数: 1｜文件数: 2
- 文案来源: Gitea/stick.plugins.fuckcustommap
- ✅ 无警告

## stick.plugins.onekey
- guid: stick.plugins.onekey｜latest: **1.1.3**｜版本数: 2｜文件数: 3
- 文案来源: Gitea/stick.plugins.onekey
- ✅ 无警告

## stick.plugins.playermanager
- guid: stick.plugins.playermanager｜latest: **5.1.0**｜版本数: 13｜文件数: 14
- 文案来源: Gitea/stick.plugins.playermanager
- ✅ 无警告

## stick.plugins.stat
- guid: stick.plugins.stat｜latest: **1.2.1**｜版本数: 4｜文件数: 5
- 文案来源: Gitea/stick.plugins.stat
- ✅ 无警告

## stick.plugins.xhighws
- guid: stick.plugins.xhighws｜latest: **0.0.1**｜版本数: 1｜文件数: 2
- 文案来源: Gitea/stick.plugins.xhighws
- ✅ 无警告

## z7572.DesyncFixer
- guid: z7572.DesyncFixer｜latest: **1.1.0**｜版本数: 1｜文件数: 2
- 文案来源: GitHub
- ✅ 无警告

## 全部警告

- 无

## 推送方式

把每个子目录的 info.json 用 SFTP 推到站上对应 mod 目录根部（与 dll 同级），然后在管理器里刷新验证。
