---
id: 12
title: 决策：配置编辑器
labels: ["wayfinder:grilling"]
status: closed
assignee: moncak (wayfinder session 2026-09-09)
blocked-by: [11]
---

## Question

基于调研结论定配置编辑器的产品形态：编辑哪些文件（BepInEx .cfg / UVFS config.ini）；从哪里进入（mod 详情页/独立页面）；能否编辑未运行过（配置尚未生成）的 mod；控件如何按类型生成（bool/数字/枚举/字符串/数组）与元数据缺失时的降级；写回策略与游戏运行中的并发提示；是否支持「恢复默认值」「按 mod 备份配置」；与工单「决策：mod 本地生命周期机制」的卸载/启停如何协同。
## Resolution（2026-09-09，grilling 两轮 + 会话内调研）

### 范围与入口
1. **只编辑** `<游戏>/BepInEx/config/*.cfg`；UVFS `overlay/config.ini` 不碰（含 root_ 暗门开关）。
2. **入口**：mod 详情页内嵌「配置」区块（只显示该 mod 的）+ 全局配置页兜底（列出全部 .cfg，含无关联的孤儿文件）。
3. **mod 关联**以 .cfg 头部 `## Plugin GUID:` 为准（文件名通常是 `<guid>.cfg`，但不假设）；关联不上则归入「未关联」分组。
4. **未生成配置**：提示「尚未生成配置，先启动一次游戏」+「启动游戏」按钮（`steam://rungameid/674940`）。

### 解析与写回
5. **自写行式解析器**（不引 BepInEx API、不引 Tomlyn——.cfg 非严格 TOML）。解析 `## 描述`、`# Setting type:`、`# Default value:`、`# Acceptable values:` / `# Acceptable value range:`、Flags 提示行；写回**只替换值行**，保留注释/顺序/空行；UTF-8 无 BOM。
6. **无元数据条目**（Orphaned）：显示键值、标注「无元数据」、按字符串编辑。
7. **即改即存** + 「已保存」提示；**非法值拒绝写入并就地提示，保持原值**。
8. **游戏运行中**：检测 `StickFight.exe` / `StickFightTheGame.exe` 进程 → 编辑器转只读 + 提示「请关闭游戏后编辑」。
9. **生效提示**：配置区顶部常驻「配置在下次启动游戏时生效」。

### 控件映射（按 `# Setting type:`）
| Setting type | 控件 | 序列化格式 |
|---|---|---|
| Boolean | 开关 | `true` / `false` |
| 各整数类型 | 数字框（按 AcceptableValueRange 限幅/滑块） | 十进制 |
| Single / Double / Decimal | 数字框 | InvariantCulture |
| String | 文本框 | BepInEx Escape 规则 |
| enum | 下拉 | 枚举名 |
| [Flags] enum | 多选 | 逗号分隔（如 `Debug, Warning`） |
| KeyboardShortcut | 快捷键捕获控件 | `LeftControl + K`；未设置 = `Not set`（源码：Serialize = 键名用 ` + ` 连接，Deserialize = 按 `+` 拆分后 Enum.Parse(KeyCode)） |
| Color | Avalonia 原生取色盘 | hex RGBA，如 `FF00FFFF`（可带 `#`）（源码：`ColorUtility.ToHtmlStringRGBA` / `TryParseHtmlString`） |
| 其它未知类型 | 文本框 + 保存前基本校验 | 原样字符串 |

### KeyCode / Color 的类型来源
10. 管理器**不引用 UnityEngine**；内置一份 **Unity 5.6.x 的 KeyCode 名称表**（从游戏的 `UnityEngine.CoreModule.dll` 反射导出，或按 Unity 5.6.7/5.6.4 文档），与游戏的 Unity 版本对齐。
11. Color 按 `ColorUtility` 的 hex RGBA 语法读写；Avalonia 12 原生取色盘（ColorPicker）直接对接。

### 其它
12. **恢复默认**：每条目「重置」（用 `# Default value`）+ 每文件「全部重置」。
13. **备份**：首次编辑某文件前自动备份到 `<游戏>/.lunaqua/backup/config/<guid>/<时间戳>.cfg`。

### 明确不做
- UVFS `config.ini` 的编辑（含 root_ 暗门开关）；
- 其它 Unity 类型的专门控件（文本框兜底即可）；
- 配置热生效（BepInEx 插件启动时读取，无法从外部触发）。
