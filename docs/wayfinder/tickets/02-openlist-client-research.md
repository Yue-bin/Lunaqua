---
id: 02
title: 调研：OpenList 客户端与 API 能力
labels: ["wayfinder:research"]
status: closed
assignee: research-subagent (2026-09-09)
blocked-by: []
---

## Question

针对 blog.monblog.top/openlist（已实测 /api/public/settings 与 /api/fs/list 匿名可用）查证：列目录/读 README/原始下载直链与 sign 机制/搜索（是否需建索引）/鉴权方式；评估 AListSdkSharp 1.2.1 与 OpenList 的兼容度 vs 自写轻量 HTTP 客户端（列目录+取文件其实很简单）；给出客户端选型建议与理由。

## Resolution（2026-09-09，research 子代理）

完整报告：[research/02-openlist-api.md](../research/02-openlist-api.md)

**结论：自研 ~300 行 HttpClient 轻客户端（匿名只读），不引入 AListSdkSharp**（SDK 停更一年+、无测试、分页 bug pre_page、含 v4 已删端点）。

关键事实：
- 匿名可用：/api/public/settings、/api/fs/list、/api/fs/get、/api/me；写操作需 token。
- **无 /api/fs/read**：读 readme 要 fs/get 拿 raw_url 再 GET；fs/list 的 readme 字段恒空。
- **无搜索**（本站 search_index=none，code 404）：搜索走本地缓存目录树。
- sign 永久有效（":0"）但 Token 轮换即失效 → 每次会话重新取 raw_url，勿持久化。
- 真实路径含 guest base_path=/stick_mods，拼 /d/ 必须用真实路径；用 raw_url 最省事。
- Range 206 / ETag 304 均可用 → 断点续传与条件请求可行。
- /api/* 一律 HTTP 200，错误看 JSON code；未知端点返回 HTML。
- 中文文件名在 Content-Disposition 的 filename= 是百分比编码，filename* 才是明文。
- 站上现状：每 mod 一目录 + 平铺历史版本 dll；**z7572.DesyncFixer 目录含两个不同 mod 的 dll**（已升级为工单 04 的约定问题）。
- 安全提示：本站 guest 有写权限（write:true），建议站长收紧；v2 只读。
