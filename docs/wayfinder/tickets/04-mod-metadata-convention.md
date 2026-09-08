---
id: 04
title: 决策：mod 元数据格式与目录约定
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: []
---

## Question

方向已定：每个 mod 一个文件夹、文件夹内有元数据文件才视为「可管理的 mod」。要定：元数据文件名与 schema（id/名称/作者/版本/描述/图标/类型标记等）；版本与更新检测语义；是否/如何辅读 BepInEx dll 内嵌信息（GUID/版本）；CI→SFTP 推送侧 info.json 由谁生成、版本号怎么涨；兼容站上现有 9 个 stick.plugins.* 目录的迁移成本。
## Resolution（2026-09-09，grilling 三轮 + research 输入）

### 约定总览
1. **位置**：每个 mod 一个目录，目录根部放 `info.json`；**目录内有 info.json 才算可管理 mod**。无元数据目录管理器不展示、不管理（存量 9 个一次性补录，见工单「补录：存量 9 个 mod 的 info.json」）。
2. **文件名/格式**：`info.json`，JSON（System.Text.Json 原生，CI 任意语言可写）。
3. **顶层字段**：
   - `schema`: 1（元数据格式版本，留给未来演进）
   - `id`: 字符串，**必须等于目录名**；不一致时以元数据 id 记账 + 界面标黄警告，仍可正常管理
   - `type`: `"bepinex"` | `"uvfs"`（显式类型；uvfs 依赖 UVFS 框架本体，归工单「双 mod 类型架构抽象」）
   - `guid`: 字符串，**BepInEx 类型必填**，CI 从 dll 的 BepInPlugin 解析；uvfs 不写
   - `name` / `author` / `description`: 文案字段，全部来自 readme（见第 6 条）；description 为纯文本短文案
   - `latest`: 必须是 `versions[]` 中存在的版本号（显式指针，客户端不猜排序）
   - `dependencies`: `[{ id, minVersion? }]`，CI 从 dll 的 BepInDependency 解析
   - `versions`: 全部历史版本数组，每项 `{ version, released, files[{ path, sha256, size }], changelog? }`
     - `version`: **SemVer 2.0.0（含 `-rc.1` 等预发布后缀）**，且必须同时等于 dll 内嵌 BepInPlugin.Version 与 git tag，**任一不一致 CI 直接失败**
     - `released`: git tag 的日期（ISO 8601）
     - `files[].path`: 相对 mod 目录的路径；`sha256` 小写 hex；`size` 字节数（下载校验 + 断点续传依据）
     - `changelog`: readme「## 更新日志」中对应 `### vX.Y.Z` 小节的文本（可选）
4. **可选素材（不进 schema，约定文件名）**：`icon.png`（客户端自行缩放）、`readme.md`（详情页渲染）；有则显示、无则优雅降级。
5. **客户端行为**：
   - 列表：遍历站上目录，只有含 info.json 的才收录；
   - 已装识别：BepInEx 按 `guid` 匹配本地 BepInEx/plugins 中 dll 的插件 GUID；
   - 更新检测：本地版本 vs `latest`，SemVer 比较（含预发布）；
   - 安装：按 `latest` 对应 `files[]` 逐文件下载 → sha256 校验 → 落位（断点续传用 Range/ETag，见工单「调研：OpenList 客户端与 API 能力」）；
   - 依赖：安装前检查 `dependencies[]`，缺失则提示并支持一键安装；**不做版本冲突求解**；
   - id 不一致：以元数据 id 记账 + 标黄警告。
6. **readme 是唯一人工源**：CI 解析 readme 生成文案——H1 → `name`；`## 作者` → `author`；`## 功能` → `description`；`## 更新日志` 的 `### vX.Y.Z` → 对应版本的 `changelog`。人工只维护 readme，零重复。
7. **严格性**：SemVer 强制；版本三处一致否则 CI 失败；**改名换 GUID 不兼容**（旧 dll 直接删除，需要旧版的自行去仓库找）——规范是用来遵守的。
8. **CI 侧契约**（在 mod 仓库执行，本图只定契约）：解析 dll（BepInPlugin 的 GUID/Version/Author + BepInDependency）→ 校验 SemVer 与版本三处一致 → 解析 readme 文案 → 计算 sha256/size → 追加/更新 `versions[]` 与 `latest` → 随 dll 一起 SFTP 推送 info.json。

### info.json 示例

```json
{
  "schema": 1,
  "id": "stick.plugins.playermanager",
  "type": "bepinex",
  "guid": "stick.plugins.playermanager",
  "name": "玩家管理器",
  "author": "老狼老狼几点钟、Moncak、z7572",
  "description": "房间查找、转让房主、玩家记录、kick、拉黑、静音、一键加好友、最近一起玩的玩家列表。",
  "latest": "5.1.0",
  "dependencies": [],
  "versions": [
    {
      "version": "5.1.0",
      "released": "2026-08-31",
      "files": [
        { "path": "stick.plugins.playermanager.dll", "sha256": "9f2c…", "size": 123456 }
      ],
      "changelog": "1. 添加了被kick提示\n2. 一些小重构和规范化"
    }
  ]
}
```

### 明确不做
- 无元数据目录的兼容降级（存量一次性补录即可）；
- 改名/换 GUID 的历史身份迁移；
- 依赖版本冲突求解与循环依赖处理；
- 中心化索引文件（目录树逐层枚举足够，站上规模小）；
- 展示素材字段化进 schema（icon/readme 走文件名约定）。
