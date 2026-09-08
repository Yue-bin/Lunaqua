---
id: 03
title: 调研：BepInEx 5 全自动代安装
labels: ["wayfinder:research"]
status: closed
assignee: research-subagent (2026-09-09)
blocked-by: []
---

## Question

目标：用户要求「高度自动化代操作」——自动定位游戏目录（Steam 库扫描/注册表）、按 Assembly-CSharp.dll 哈希识别游戏版本并确认其干净、自动完成 BepInEx 5 (x64) 部署直至可用。查证：BepInEx 5 pack 的文件清单与安装步骤中哪些可程序化；如何取「原版干净文件哈希」（Steam depot 下载/社区数据库/首次扫描缓存）；已有工具（r2modman、BepInEx 装机脚本等）的成熟做法；失败场景（更新后哈希不匹配、非 Steam 版）如何呈现给小白用户。

## Resolution（2026-09-09，research 子代理）

完整报告：[research/03-bepinex-autoinstall.md](../research/03-bepinex-autoinstall.md)

**勘误**：Stick Fight 的 Steam AppID = **674940**（非 730840）；64 位更新后 exe/Data 目录名可能与 r2modman 注册表（StickFight.exe / StickFight_Data）不同，需兼容探测。

要点：
- 用 BepInEx **5.4.23.5 win_x64**（22 文件，纯文件复制即可安装；5.4.21/22 无 .doorstop_version、ini 键名不同）。
- 位数必须读 exe PE 头（x64），不信注册表；覆盖前备份 winhttp.dll/doorstop 两文件；杀软误报需专门话术。
- 干净判定三层：buildid（主键）+ 官方构建指纹表（DepotDownloader -manifest-only -filelist 生成，随 Lunaqua 发布）+ 本机首扫基线（user-trusted 兜底）；失配走 steam://validate/674940 分级降级。
- 定位链：注册表 SteamPath → libraryfolders.vdf → appmanifest_674940.acf → 结构校验 + PE 头。
- 验证：启动后 ≤60s 轮询 BepInEx\LogOutput.log 出现 BepInEx 5.4.23；失败分类 + 一键回滚（备份在 %LocalAppData%\Lunaqua\backups）。
- 待人工复核：站上 pack 实际内容（子代理侧 404）、真实 exe/Data 命名、指纹表生成。
