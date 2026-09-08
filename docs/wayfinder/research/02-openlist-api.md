# 调研报告：OpenList 客户端与 API 能力（工单 02 资产）

目标站：https://blog.monblog.top/openlist（OpenListTeam/OpenList 驱动，实测 v4.2.2，Frontend v4.2.2，Build 2026-05-25）。
标注：【实测】对目标站 curl/node 实测；【源码】OpenList v4.2.2/main 源码确证；【代理】子代理对 NuGet/GitHub 一手来源调研；【推断】分析判断。

## 1. API 能力清单

### 1.1 响应包装（关键坑）【实测+源码】
所有 /api/* 均返回 HTTP 200，业务错误在 JSON envelope 的 code 字段：fs/get 不存在 → code 500；fs/search 无索引 → code 404 "search not available"；/api/admin/* 匿名 → code 403；未知 /api 路径（如 /api/fs/read）→ 返回前端 SPA 的 index.html。
→ 客户端必须以「能否解析为 {code,message,data} JSON」判成败，不能只看 HTTP 状态码。
源码：server/common/resp.go

### 1.2 站点信息：POST /api/public/settings 【实测】
匿名可用。本站：hide_files="/\/README.md/i"、search_index="none"、default_page_size="30"、auto_update_index="false"、ignore_direct_link_params="sign,openlist_ts,raw"。
注意 hide_files 只是前端展示过滤，服务端 fs/list 不过滤（实测 BepInEx 列表里 readme.md、以及名字写着「这个文件夹不应该出现」的 zip 都原样返回）。

### 1.3 列目录：POST /api/fs/list 【实测+源码】
请求 {path,password,page,per_page,refresh}；响应 data：content[]（name,size,is_dir,modified,created,sign,thumb,type,hashinfo,hash_info）、total、readme、header、write、provider。
- 务必显式传 per_page（默认 30）；分页为服务端内存切片。
- readme 字段只来自 meta 配置，本站恒为空串——要拿说明需自行下载目录内 readme.md（根 2952B，各 mod 目录 149B–10.8KB，实测均可下载）。
- type：1=目录、4=markdown、0=普通文件（判目录请用 is_dir）。
源码：server/handles/fsread.go

### 1.4 文件信息：POST /api/fs/get 【实测+源码】
匿名可用。返回 sign、raw_url（形如 {base}/p/{真实挂载路径}/file?sign=...）、provider、related。raw_url 已含编码与 sign，照抄即可下载。

### 1.5 读文本：没有 /api/fs/read 【实测+源码】
v4.2.2 路由表无此端点；实测返回 SPA index.html。正确方式：fs/get 拿 raw_url → GET（text/markdown; charset=utf-8）。

### 1.6 下载直链与 sign 机制 【实测+源码】
- 端点：GET/HEAD /d/{真实路径}（attachment）与 /p/{真实路径}（预览流）。无 sign/错 sign → 真 HTTP 401 + HTML（与 /api 的 envelope 不同）。本站开启 SignAll。
- 真实路径 ≠ guest 可见路径：本站 guest base_path=/stick_mods（/api/me 匿名可查）。手工拼 /d/ 必须拼真实路径（/d/readme.md?sign= → 401；/d/stick_mods/readme.md?sign= → 200）；用 fs/get 的 raw_url 最省事。
- sign = base64url(HMAC-SHA256(key=站点Token, data=path+\":\"+expire))+\":\"+expire；expire=0 表示永不过期，本站全部 \":0\" → 签名长期有效（「30 天签名」不适用）；站长轮换 Token 会使所有 sign 失效。
- 下载质量【实测】：HEAD 200+Content-Length；Range → 206（断点续传可用）；ETag/Last-Modified → 304（可做条件请求）；zip 627,740B、application/zip、PK 头；dll application/octet-stream、MZ 头。
- 归档端点 /api/fs/archive/meta、/api/fs/archive/list 匿名实测可用：不下载 zip 即可枚举内部目录树（可用于校验 BepInEx 包结构）。

### 1.7 搜索：POST /api/fs/search 【实测+源码】
本站 search_index="none"、auto_update_index=false → 无索引，搜索返回 code 404。管理器应实现搜索但做好降级；目录树用 fs/list 逐层枚举（顶层仅 ~11 项，开销可忽略）。allow_indexed 与文件搜索无关。

### 1.8 批量信息 【实测+源码】
无批量端点；fs/dirs 只返回目录树不含文件。做法：并发多次 fs/list（实测 40 连发全 200、无 429）。

## 2. 匿名访问边界 【实测+源码】
- 匿名只读三件套 settings/fs/list/fs/get 全部可用；/api/me 匿名返回 guest（id=2, base_path=/stick_mods）。
- 需要 token：全部写操作、task/*、share/*、admin/*（匿名 code 403）；token 由 POST /api/auth/login 获取，Authorization 头放裸 token（无 Bearer 前缀）。
- **本站 guest 有写权限（fs/list 返回 write:true）**——建议站长收紧 guest 写权限（防误删/滥用）；v2 客户端只读，永不调用写端点。

## 3. AListSdkSharp 1.2.1 评估 【代理】
- MIT、单作者（j4587698）、仅 netstandard2.0、依赖 Flurl.Http 4.0.2；1.2.1=2024-10-24 后停更；约 2,971 下载、9 stars、无测试无文档。
- 对 OpenList v4.2.2：主链路（fs/list→fs/get→raw_url 下载）大概率可用；必坏点：add_aria2/add_qbit（v4 已删路由）、PUT /api/fs/form 上传（协议不符）；自身 bug：分页参数序列化为 pre_page（服务端要 per_page → 分页从未生效）。
- 结论：成熟度低，不建议作生产依赖。
- 自研估算：HttpClient + System.Text.Json 约 250–400 行（envelope+DTO ~60、client ~120–180、下载器 Range/304/进度 ~80–120）。

## 4. 实测记录（匿名）
| # | 请求 | 结果要点 |
|---|---|---|
| 1 | POST /api/public/settings | version=v4.2.2；hide_files；search_index=none |
| 2 | POST /api/fs/list {"/",per_page:100} | 11 项：BepInEx/、readme.md、9 个 stick.plugins.*、z7572.DesyncFixer/；write:true |
| 3 | POST /api/fs/get /readme.md | raw_url=/p/stick_mods/readme.md?sign=...:0；provider=Local |
| 4 | POST /api/fs/read | 无此路由（返回 SPA HTML） |
| 5 | POST /api/fs/search | HTTP 200 + code 404 |
| 6 | GET /api/me | guest, base_path=/stick_mods |
| 7 | GET /d/readme.md（±sign） | 均真 401；须 /d/stick_mods/... → 200 |
| 8 | GET /p/stick_mods/readme.md?sign | 200；text/markdown；2952B |
| 9 | GET raw_url（BepInEx zip 中文名） | 200；application/zip；627,740B；filename= 为百分比编码，filename*=utf-8'' 才是明文 |
| 10 | Range / HEAD | 206+Content-Range / 200+Accept-Ranges |
| 11 | If-None-Match / If-Modified-Since | 304 |
| 12 | /api/fs/archive/meta|list（zip） | 200：不下载即可枚举 zip 内部树 |
| 13 | 40 连发 fs/list | 全 200，无 429 |
| 14 | fs/get 不存在 / admin 匿名 | code 500 / code 403 |

**mod 库结构实测**：每个 mod 一个目录（BepInEx GUID 风格名），目录内 readme.md + 全部历史版本 dll 并排（playermanager 有 v1.0.0→v5.1.0 共 14 个 dll）；cntext 附 StreamingAssets.zip(9.6MB)；**z7572.DesyncFixer 目录含 DesyncFixer 与 NaNFixer 两个不同 dll**。

## 5. 结论
**自研 ~300 行 HttpClient 轻客户端（匿名只读），不引入 AListSdkSharp。**
必防的坑：错误判定看 JSON code（HTML fallback）；中文文件名取 filename* 或 URL-decode；sign 每次会话重新 fs/get 拿 raw_url，勿持久化；并发 2–4 + 指数退避；版本文件名风格混乱需宽容 SemVer + modified 兜底；目录列表未经策展。

主要来源：目标站实测；OpenList v4.2.2 源码（server/router.go、server/handles/fsread.go、server/middlewares/down.go、server/middlewares/search.go、internal/sign/sign.go、pkg/sign/hmac.go）；https://doc.oplist.org/ ；https://alist.nn.ci/guide/api/fs.html ；https://www.nuget.org/packages/AListSdkSharp 。
