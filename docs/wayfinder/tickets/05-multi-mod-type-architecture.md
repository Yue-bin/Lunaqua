---
id: 05
title: 决策：双 mod 类型（BepInEx / UVFS）架构抽象
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: []
---

## Question

两类 mod：BepInEx 代码插件（dll 进 BepInEx/plugins）与 UVFS 覆盖层资源 mod（目录进 overlay/mods/<modName>/，依赖 UVFS 框架本体已部署）。要定：Provider/Installer 抽象面（发现、详情、安装、卸载、启停、更新在两类上如何统一）；类型在元数据里如何标记；UVFS 框架本体是否也作为一种「必需组件」由管理器代装；未来第三种类型（如别的 Unity 游戏）的扩展点。
## Resolution（2026-09-09，grilling 三轮 + UVFS 源码取证）

### 类型与落位（最终形态）
| type | files[].path 相对 | 安装目标 | 已装识别 | 启停 |
|---|---|---|---|---|
| `bepinex` | **BepInEx/plugins/** | `<游戏>/BepInEx/plugins/<path>` | dll 的 BepInPlugin GUID | 移到 `<游戏>/.lunaqua-disabled/<id>/` |
| `uvfs` | **overlay/mods/<id>/** | `<游戏>/overlay/mods/<id>/<path>` | 本地 info.json 凭证 | `overlay/mods/<id>/` ↔ `overlay/mods.disabled/<id>/` |
| `uvfs-framework` | 无（内置映射） | 见下 | GUID `Overlay.Managed` | 同 bepinex |

- `bepinex`：站上 mod 目录平铺 dll（最简），落 BepInEx/plugins/（支持子目录）。
- `uvfs`：站上目录镜像 overlay/mods/<id>/（data/...、bundles/...），逐文件落位；**只支持 data/ 与 bundles/ 作用域，root_ 暗门不支持**。
- `uvfs-framework`（特例）：UVFS 框架本体。落位由管理器**内置映射表**决定——BepInEx/patchers/Overlay.NativeLoader/{overlay32.dll, overlay64.dll, Overlay.NativeLoader.dll}、BepInEx/plugins/Overlay.Managed.dll、BepInEx/plugins/XUnity.ResourceRedirector/{XUnity.ResourceRedirector.dll, XUnity.Common.dll, XUnity.ResourceRedirector.BepInEx.dll}（来源：UVFS scripts/deploy.cmd 实测）；`files[]` 只负责下载与 sha256 校验，按文件名匹配内置映射；**站上结构与映射不符 → 拒绝安装并提示升级管理器**。
- 所有类型统一拒绝 `../` 路径穿越。

### 其余决议
1. **抽象**：薄策略层——一个通用安装核心（下载/校验/落位/凭证/备份/冲突）+ 每类型一个策略实现（目标根解析、已装识别、启停、卸载）。新增类型 = 加一个策略 + 一个 type 值。
2. **已装凭证**：安装时把 info.json 落到安装根固定位置（bepinex/uvfs-framework → `<游戏>/.lunaqua/<id>/info.json`；uvfs → `overlay/mods/<id>/info.json`），记录实际落位清单 + 版本；识别/启停/卸载都读它；另用 dll GUID 扫描兜底识别手动安装的 mod。删目录=卸载，自愈。
3. **启停**：uvfs 移出移回（源码事实：OverlayPlugin 用 Directory.GetDirectories(overlay/mods) 枚举，无任何禁用约定，改名无效）；bepinex/uvfs-framework 按凭证整体移到 `<游戏>/.lunaqua-disabled/<id>/`（注意 Chainloader 递归扫 plugins 下 *.dll，禁用区必须在 plugins 之外）。
4. **卸载**：删除凭证记录的文件；**弹窗询问是否保留配置**（BepInEx/config 的 .cfg、玩家数据如 CustomPlayerRecord.txt），选择删除才连带清理。
5. **前置检查**：BepInEx 缺失 → 直接进入工单「调研：BepInEx 5 全自动代安装」的全自动代装流程，装完继续装 mod；uvfs mod 缺 UVFS 框架 → 走 dependencies[] 检查 + 一键安装（框架作为站上普通 mod 上架，type=uvfs-framework，id 由站长定）。
6. **同名文件**：弹窗让玩家选覆盖/跳过；覆盖前备份到 `<游戏>/.lunaqua/backup/<时间戳>/` 并记入凭证。
7. **未知 type**：跳过安装，列表显示「不支持的类型」+ 版本信息（提示升级管理器）。
8. **单游戏单实例**：游戏目录一个配置项，不做多游戏抽象（未来再加 GameProfile）。
9. **UVFS 冲突**：用各已装 mod 本地 info.json 的 files[] 比对同名覆盖，提示但不阻止（UVFS 规则是字典序靠后者赢）。

### 明确不做
- 多游戏/多实例抽象；
- root_ 暗门类 mod；
- 完整冲突管理页（不改 UVFS 的字典序规则）；
- 改名/换 GUID 的历史身份兼容（工单 04 已定）。
