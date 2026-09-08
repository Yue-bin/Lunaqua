---
id: 08
title: 决策：Lunaqua 自身分发与更新渠道
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: []
---

## Question

管理器自己怎么到用户手里、怎么更新：单文件 exe 丢群里？挂到 OpenList 站上自我更新？安装器？要考虑小白玩家（连解压都不会）的零门槛获取路径与后续静默更新策略。
## Resolution（2026-09-09，grilling 两轮 + Velopack 文档取证）

### 分发与更新
1. **首发渠道 = OpenList 站的 `Lunaqua/` 目录**（站根，与 mod 并列）：放最新 `Setup.exe` + `readme.md`（安装 / 更新 / 卸载 / 回退说明），**只保留最近 3 个版本**。该目录没有 info.json → 管理器按工单 04 的约定忽略，不会混进 mod 列表。
2. **自动更新源 = 自建静态目录**（如 `https://blog.monblog.top/lunaqua/updates`，Caddy 直接托管、**无 sign**），Velopack `SimpleWebSource` 读取 `releases.win.json` + 差分包。**不用 OpenList 直链**（站点 SignAll，token 轮换会让所有直链失效）。
3. **更新行为**：启动时后台检查 → 有新版提示「下载并重启」（Velopack 原生流程）。
4. **安装形态**：Velopack `Setup.exe`（自动快捷方式 + 卸载项）；**单通道稳定版**，不做预览通道。
5. **回退**：设置页「关于 / 更新」区一个「回退到上一版」按钮（Velopack 指定版本更新）；回退后仍会提示可更新回最新。
6. **发布流水线 = GitHub Actions**：tag 触发 → `dotnet publish` → `vpk pack` → 同时上传两处：
   - 静态更新目录（releases feed + 差分包）；
   - OpenList 站 `Lunaqua/` 目录（Setup.exe + readme）。
   需要 secrets：静态目录上传凭据（SFTP/SCP/rsync，可与 mod CI 同源）、OpenList 上传凭据（或同样走 SFTP）。**版本号权威 = git tag**（与工单 04 的「版本三处一致」同源）。
7. Setup 内**内置更新源 URL**：玩家从站上装的版本，之后自动走静态目录更新。

### 事实依据（Velopack 文档）
- 更新源只有 `SimpleWebSource`（任意静态目录）/ `SimpleFileSource` / `GithubSource` / `GiteaSource` / `VelopackFlowSource`；**库内没有 S3/Azure 源**（那只是 deploy 侧概念，客户端一律用 SimpleWebSource 指向桶 URL）。
- GitHub/Gitea 源匿名限 60 请求/小时/IP（更新场景够用），私有仓库需 token。

**未决：无。**