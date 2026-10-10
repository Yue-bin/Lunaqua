# 调研 15：mod 发布链路（上传页 + CI）

- 日期：2026-10-11
- 状态：站长决策已定（规格书 §15）；实现未排期
- 背景：项目搁置约一个半月，**现有 6–7 个 modder**，其中有人不会用 git、有人硬编码版本号反复重发。目标是把「发一个 mod 新版本」从「手写 info.json + 手动传文件」变成「网页表单 / CI 一条命令」。

---

## 1. 已决（站长，2026-10-11）

| 项 | 决定 |
|---|---|
| 入口 | `https://mod.monblog.top/upload` |
| 同版本冲突 | **一律打回**（不做自动升版、不做覆盖） |
| 通道 | 双通道：网页上传（主线）+ CI / API token（会用 git 的人） |
| 账号 | 站长手动开号；自助注册暂不做（留设计） |
| 托管 | 与 OpenList / Caddy 同机，Caddy 反代 |

## 2. OpenList 写面能力（一手：匿名探测 + 官方 API 文档）

| 用途 | 端点 | 约束 |
|---|---|---|
| 建目录 | `POST /api/fs/mkdir` `{path}` | 匿名 403（端点存在） |
| 上传（流式） | `PUT /api/fs/put` | `File-Path` 头（URL 编码）+ 原始字节 body |
| 上传（表单） | `PUT /api/fs/form` | `multipart/form-data`，**必须带 `Content-Length`（文件大小）** 与 `File-Path` |
| 改名 | `POST /api/fs/rename` `{name, path}` | `name` 不能含 `/` |
| 移动 / 复制 / 删除 | `fs/move`、`fs/copy`、`fs/remove` | 端点存在（匿名 403） |
| WebDAV | `/dav/` | `PROPFIND` 匿名 **401** → 通道存在，需认证 |
| 读回校验 | `fs/list`、`fs/get` | `per_page` ≤ 500；`filtered_total`/`has_more`/`pages_total` 都有 |

**未验证**：`fs/rename` 对**目录**是否可用（文档只写「源文件名」）。发布流程想用它做原子切换，**必须在站上拿测试目录实测一次**；不行就退回 `fs/move`。

**路径相对根**：API 路径是相对**该账号 `base_path`** 的。匿名 guest 的 `base_path=/stick_mods`，所以列站根要用 `path:"/"`（用 `path:"/stick_mods"` → `code 500 object not found`）。发布账号若也限在 `/stick_mods`，写文件时要发 `/stick.plugins.foo/x.dll`，不能带 `/stick_mods` 前缀。

## 3. 权限与鉴权（一手：OpenList 源码 + 匿名实测）

匿名身份：`GET /api/me` → `{id:2, username:"guest", base_path:"/stick_mods", role:1, permission:20482}`。

权限位定义（`internal/model/user.go`）：

| 位 | 值 | 含义 |
|---|---|---|
| 0 | 1 | 可见隐藏文件 |
| 1 | 2 | 免密访问 |
| 2 | 4 | 离线下载任务 |
| 3 | 8 | 上传 / mkdir |
| 4 | 16 | 改名 |
| 5 | 32 | 移动 |
| 6 | 64 | 复制 |
| 7 | 128 | 删除 |
| 8 | 256 | WebDAV 读 |
| 9 | 512 | WebDAV 写 |
| 10 | 1024 | FTP/SFTP 读 |
| 11 | 2048 | FTP/SFTP 写 |
| 12 | 4096 | 读压缩包 |
| 13 | 8192 | 解压 |
| 14 | 16384 | 分享 |
| 15 | 32768 | 自定义分享 id |

`20482 = 2 + 4096 + 16384` → 免密访问 + 读压缩包 + 分享。**所有写位为 0。**

实测：`PUT /api/fs/put`、`POST /api/fs/mkdir`、`fs/rename`、`fs/batch_rename` 匿名 → `code 403 permission denied`；`fs/move`、`fs/remove` 空 body → `code 400 Empty file names`（先校验参数后校验权限，不影响结论）。

⚠️ **`fs/list` 响应里的 `write:true` 不是当前用户的写权限**（站点/存储维度字段）。规格书旧版 §5 曾据此误判「guest 有写权限」，已纠正。

**鉴权基建能不能复用**：

- ✅ 可复用**用户表 + 权限位模型**：`POST /api/admin/user/create` 存在（匿名 → `code 403 You are not an admin`）；用户带 `base_path` 与权限位，天然支持「modder 只能看见 `/stick_mods`、不能写」的账号
- ❌ **不可复用登录 token 当发布/CI 凭据**：`server/common/auth.go:28` 里 JWT 的 `ExpiresAt = now + conf.TokenExpiresIn 小时`，`ParseToken` 对过期直接返回 `token is expired`；claim 里只有用户信息，**没有 scope**，权限就是账号的权限位（能写存储 = 能写存储里的一切）。CI 需要长期有效、可撤销、可限定用途的凭据 → **服务自己签发 token**（存哈希、可限定到某个 mod id），服务端另持**唯一一个**高权限 OpenList 凭据写站

## 4. 发布流程（设计）

1. 上传到 `/stick_mods/.staging/<提交ID>/`（`.lunaqua-` 前缀占位：站上约定凡 `.` 开头目录不作为 mod）
2. 服务端校验（见 §5）
3. 文件落到 `<mod id>/`（文件名带版本号，天然不覆盖历史）
4. **最后**写 `info.json`（先写 `.tmp` 再改名）—— **info.json 就是提交点**，在它落盘之前玩家看到的仍是旧版本
5. 审计留痕（谁 / 哪个 mod / 版本 / 每个文件 sha256 / 时间 / IP / 走哪个通道）+ 返回可分享链接

## 5. 服务端校验（为什么必须服务端做）

- **不让 modder 手写元数据**：`guid` / `name` / `version` 从 **dll 内嵌 `BepInPlugin`** 读（Mono.Cecil，已在 BOM 里）；`author` / `description` / 更新日志从 readme 解析（规格书 §4.4）
- **版本号三方比对**：dll 内嵌 `Version` == 提交声明版本；站上该 mod 的版本必须**严格递增**（SemVer 2.0.0 比较）
- **同版本冲突一律打回**：版本号已存在 → 拒绝。sha256 与已有版本相同 → 提示「内容未变，无需重发」；不同 → 提示「版本号已被占用，请升版本后重传」
  - 动机：有 modder 硬编码版本号反复重发。客户端凭证只认 SemVer（`有更新 = 已装 < latest`），同号不同内容会让已装玩家**永远收不到更新**，且与 M3 的干净判定/哈希校验互相打架
- **文件级**：逐个算 sha256 写进 info.json；zip 解包做 **zip-slip**（绝对路径 / `..`）与 **zip 炸弹**（条目数、单文件、总解压量上限）防护
- **UVFS 覆盖层**：只允许 `data/`、`bundles/` 作用域（规格书 §6）
- 元数据生成后**回显预览**（即将写入的 info.json + 文件哈希清单）再提交

## 6. 上传形态与「同时需要 BepInEx + UVFS」的包

收三种：

1. **单个/多个 dll**（BepInEx 插件）—— 最安全，直接读 BepInPlugin
2. **zip**（含 `data/`、`bundles/` 的 UVFS 覆盖层）
3. **组合包**：一个 zip 里同时有 dll 与 overlay 资源

组合包由服务端**拆成两条站上条目**：

- `stick.plugins.xxx`（`type=bepinex`）
- `stick.overlay.xxx`（`type=uvfs`，`dependencies: [{ id: "stick.plugins.xxx", minVersion: "x.y.z" }]`）
- 两份 info.json **同一个提交内一起写**，任一失败全回滚；版本号一起递增

为什么拆而不是塞进一个条目：`type` 决定落位根（`bepinex` → `BepInEx/plugins/`；`uvfs` → `overlay/mods/<id>/`），**一条目只能有一个 type**；拆开还能让资源包独立更新；客户端已有「依赖检查 + 一键装」（规格书 §7），玩家侧完全无感。

组合包内建议约定：zip 顶层 `plugins/`（dll）与 `overlay/`（`data/`、`bundles/`）两个目录 —— 待定稿。

## 7. 开放项

1. 站长：建发布账号 + 给上传服务一个 token（凭据不进仓库）
2. 站上实测 `fs/rename` 能否对目录操作
3. 服务端框架：**ASP.NET Core**（复用 Lunaqua 的 SemVer / info.json / 校验代码，同工具链）→ 需回工单 13 追加一笔
4. 组合包的目录约定定稿
5. OpenList 账号若开了 2FA（`OtpSecret`），API 登录需要额外 otp 字段 → 发布账号不要开 2FA，或流程里留位

## 8. 来源

- OpenList API 文档（`fs/mkdir`、`fs/rename`、`fs/form`、`fs/put`、`fs/list` 契约）
- OpenList 源码：`internal/model/user.go`（权限位）、`server/common/auth.go`（token 签发/校验）、`server/handles/auth.go`（登录）
- 站上匿名实测（2026-10-11）：`/api/me`、`fs/list`、`fs/put`、`fs/mkdir`、`fs/rename`、`fs/move`、`fs/remove`、`/api/admin/user/create`、`PROPFIND /dav/`
