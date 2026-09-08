---
id: 06
title: 决策：mod 本地生命周期机制
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: []
---

## Question

要定：本地已装清单存哪、记什么（含版本）；安装/卸载/更新在磁盘上的确切动作；启停机制（后缀改名？移动目录？清单标记？）及与 BepInEx/UVFS 各自的兼容性；如何识别并收纳用户手动安装的 mod；下载缓存与断点。
## Resolution（2026-09-09，grilling 两轮）

### 状态模型
1. **两层状态**：
   - **凭证**（管理器写的）：`<安装根>/.lunaqua/<id>/info.json`（bepinex / uvfs-framework）或 `overlay/mods/<id>/info.json`（uvfs），记录实际落位清单、来源（official / manual-adopted / unknown-version）、启停状态、忽略更新的版本；
   - **磁盘实况**：判定「已装版本」以磁盘为准——BepInEx 读本地 dll 的 GUID+Version，UVFS 读凭证的 version；两者不一致时按磁盘算。

### 安装 / 更新 / 版本
2. **安装流程**（事务式）：解析 info.json → 依赖检查（缺失→提示+一键装；缺 BepInEx→直接进入工单 03 的代装流程）→ 目标已有同名文件→弹窗选覆盖/跳过 → 全部文件下到临时区并 sha256 校验 → 备份现有文件到 `<游戏>/.lunaqua/backup/<时间戳>/` → 逐个替换 → 写凭证。**任一步失败自动回滚到操作前状态**。
3. **下载校验失败**：自动重试一次；仍失败报错 + 「重新下载」「复制信息去群里求助」。
4. **更新检查**：启动时后台检查全部已装 mod（拉各目录 info.json 比 latest）→ 列表标「可更新」→ 玩家点更新；详情页也可手动检查。**不自动更新**。
5. **就地更新**：按新版本 files[] 下载校验 → 备份 → 替换 → 删除新版本不再包含的旧文件（差集）→ 刷新凭证。
6. **版本选择/回退**：详情页版本下拉列出 `versions[]` 全部版本并标记当前已装；回退后仍会提示更新，但提供「**忽略此版本**」（记在本地凭证）。

### 启停 / 卸载
7. **启停**：按工单「双 mod 类型架构抽象」的策略（uvfs 移出/移回 `overlay/mods.disabled/`；bepinex/uvfs-framework 整体移到 `<游戏>/.lunaqua-disabled/<id>/`）；状态记在凭证；不触碰配置与玩家数据。
8. **卸载**：先反查已装 mod 的 `dependencies[]`，提示「X 依赖它」并要玩家确认 → 弹窗问是否保留配置 → 删凭证记录的文件 → 删凭证。备份保留供回溯。

### 手动安装与缓存
9. **BepInEx 手动安装**：扫描 `BepInEx/plugins` 中无凭证的 dll → 按 GUID 匹配站上 mod → 提示「发现手动安装的 X，是否纳入管理」；若版本与 `versions[]` 全不匹配（玩家私改），**仍纳管但标「未知版本 / 外部修改」，默认不提供更新**。
10. **UVFS 手动目录**：无 info.json 的目录**忽略**（04 已定：无元数据不算可管理 mod）。
11. **缓存**：`%LocalAppData%\Lunaqua\cache` 按 sha256 键复用，提供「清理缓存」；下载用 Range 断点续传 + ETag 304。

### 执行与安全
12. **并发**：串行任务队列 + 全局进度（一次一个 mod，文件级并行下载），可取消。
13. **游戏运行中**：检测 `StickFight.exe` / `StickFightTheGame.exe` → 拒绝安装/卸载/启停并提示「请先关闭游戏」。
14. **站上文件缺失**（元数据有、dll 404）：报错提示「站上文件缺失，请联系站长」。

### 明确不做
- UVFS 无元数据目录的认领/纳管；
- 自动更新（必须玩家点）；
- 多 mod 并发执行。
