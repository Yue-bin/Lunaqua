# 调研报告：BepInEx 配置解析与配置编辑器（工单 11 资产）

调研时间 2026-09-09。方法：BepInEx v5.4.23.5 源码逐行核对 + NuGet API 实测 + Gale 源码参考。标注【源码】【实测】【文档】【推断】。

## 0. 结论先行
- **不需要也不该引用 BepInEx API**：BepInEx 5 没有可复用的 NuGet 库包，而 .cfg 文件本身就是自描述的（描述/类型/默认值/取值范围全在注释里）。
- **不要用 Tomlyn**：.cfg 是 BepInEx 自有方言而非严格 TOML（键可含空格且写入时不加引号），严格 TOML 解析器会失败。
- **自写行式解析 + 行式写回**，保留注释与顺序；Gale 的 `config/bepinex` 模块是现成参考。

## 1. BepInEx 5 配置 API 公开面【源码 v5.4.23.5】
命名空间 `BepInEx.Configuration`（程序集 BepInEx.dll）：
- `ConfigFile`：Bind/AddSetting/TryGetEntry/Save/Load/OrphanedEntries
- `ConfigEntry<T>` / `ConfigEntryBase`：SettingType、DefaultValue、Description、Get/SetSerializedValue
- `ConfigDescription`：Description、AcceptableValues、Tags
- `AcceptableValueList<T>` / `AcceptableValueRange<T>`：Clamp / IsValid / ToDescriptionString
- `TomlTypeConverter`：string / bool / 各数值类型 / enum + TypeDescriptor 回退（KeyboardShortcut、Color 等）
- `ConfigDefinition`：section/key 非法字符 = `= \n \t \ " ' [ ]`，且首尾不能有空白（**内部空格合法**）

## 2. 为什么不该引用它【实测 + 源码推断】
- NuGet 实测：`BepInEx.Core`、`BepInEx.BaseUnity`、`BepInEx.Unity.Mono` 在 nuget.org **均不存在**（flatcontainer 返回 BlobNotFound；搜索只找到 BepInEx.AssemblyPublicizer 等工具包）。BepInEx 5 以 zip 分发，插件项目直接引用 pack 里的 DLL；BepInEx 6 另有自己的 NuGet feed，与 5 不通用。
- `ConfigFile.Load()` 只解析数据，**元数据注释不会还原成 ConfigDescription**（描述/类型/默认值只在插件运行时 Bind 时才存在）→ 对编辑器毫无增益。
- 引用 BepInEx.dll 会把游戏侧程序集（含 Unity 依赖）带进 Lunaqua，版本耦合、加载风险高。

## 3. .cfg 格式（源码确证：ConfigEntryBase.WriteDescription + ConfigFile.Save）
文件头（有 ownerMetadata 时）：
```
## Settings file was created by plugin {Name} v{Version}
## Plugin GUID: {GUID}
```
每个条目：
```
## {描述（多行时每行前缀 ## )}
# Setting type: {Type.Name}
# Default value: {序列化默认值}
# Acceptable values: a, b, c            ← AcceptableValueList，或 enum 无显式约束时（Enum.GetNames）
# Acceptable value range: From X to Y   ← AcceptableValueRange
# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)   ← [Flags] enum 专属行
{Key} = {值}
```
- 分节：`[Section]`；条目之间空行分隔。
- 值序列化（TomlTypeConverter）：bool → `true/false` 小写；数值 → InvariantCulture；string → Escape；enum → ToString；其它 → TypeConverter。
- **BepInEx 5 不支持 List/Dictionary 配置值**（转换器无集合支持）→ 不需要数组/字典控件；Flags enum 用逗号分隔字符串。
- 键可含内部空格且写入不加引号 → 非严格 TOML（这是不能上 Tomlyn 的硬理由）。

## 4. 解析/写回方案【推断 + 参考实现】
- **推荐**：逐行状态机解析 + 只替换值行的写回（保留注释/顺序/空行）。Gale `src-tauri/src/config/bepinex/de.rs` 的 `EntryBuilder { description, type_name, default_value, acceptable_values, is_flags, range, name, value }` 与 `EntryKind::Orphaned`（文件里有但无元数据的条目）可直接对照。
- **不推荐** Tomlyn：严格 TOML 在含空格键与 BepInEx 方言上失败；注释保留也要自己写。
- 工作量估算：解析 ~200 行、写回 ~100 行、类型→控件映射 ~100 行 C#。
- 值合法性：按 `# Acceptable values` / `range` 生成控件并做 Clamp（与 BepInEx 行为一致）。
- 无元数据条目（Orphaned）：显示键值、允许编辑、标注「无元数据」。

## 5. 先例
- **Gale**（Rust/Tauri，745★）：`src-tauri/src/config/bepinex/{de.rs,ser.rs,mod.rs,tests.rs}` + 前端控件 `BoolConfig / ColorConfig / EnumConfig / FlagsConfig / NumberInputConfig / SliderConfig / StringConfig / ResetConfigButton`，按 GUID 分组配置文件。
- **r2modman**：内置配置编辑器，同思路（解析 .cfg 注释生成表单）。
- **BepInEx.ConfigurationManager**：游戏内插件，用运行时 API 列出全部 ConfigEntry 与元数据；对「文件里没有元数据」无能为力，仅作 UX 参考。

## 6. 写回与并发【源码 + 推断】
- BepInEx 自身 `Save()` **整文件重写**（注释也会重新生成）→ 我们的编辑在插件下次保存时会被规范化，可接受。
- 游戏运行时插件持有配置并可能写回，会覆盖我们的改动 → UI 必须提示「建议关闭游戏后编辑」，文件被占用时拒绝写入。
- 写回保持 **UTF-8 无 BOM**（BepInEx 用 Utility.UTF8NoBom）。

## 7. UVFS config.ini
`overlay/config.ini` 的 `[uvfs] enable_root_overlay` 由 native 用 GetPrivateProfileIntW 读取，格式简单；但工单「双 mod 类型架构抽象」已明确不支持 root_ 暗门 → 建议 v2 不提供该开关（或只读显示）。

## 8. 推荐实现路径
1. 自写 BepInEx 行式解析/写回（不引 BepInEx、不引 Tomlyn）；
2. 配置与 mod 的映射以 .cfg 头部 `## Plugin GUID:` 为准（文件名通常是 `<guid>.cfg`，但不假设）；
3. 未运行过的 mod 没有 .cfg → 详情页提示「尚未生成配置，先启动一次游戏」；
4. 控件按 `# Setting type` 生成，取值按 `# Acceptable values/range` 约束，[Flags] 用多选；
5. 提供「恢复默认值」（用 `# Default value`）与「按 mod 备份配置」；
6. 游戏运行中编辑给出明确警告。

来源：https://github.com/BepInEx/BepInEx/blob/v5.4.23.5/BepInEx/Configuration/ConfigEntryBase.cs ；…/ConfigFile.cs ；…/ConfigDescription.cs ；…/AcceptableValueList.cs ；…/AcceptableValueRange.cs ；…/TomlTypeConverter.cs ；…/ConfigDefinition.cs ；https://github.com/Kesomannen/gale/tree/master/src-tauri/src/config/bepinex ；https://api.nuget.org/v3-flatcontainer/bepinex.core/index.json （BlobNotFound）；https://docs.bepinex.dev/v5.4.16/api/BepInEx.Configuration.html 。
