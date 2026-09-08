# 调研报告：BepInEx 5 全自动代安装（工单 03 资产）

调研时间 2026-09-09；标注：【事实·实测】= 本次直接验证；【事实·引用】= 权威文档；【推断】= 判断。

## 0. 勘误
- **Stick Fight 的 Steam AppID = 674940**（Steam 官方 appdetails 实测；730840 是别的游戏）。v2 硬编码一律用 674940。
- r2modman 生态注册表：目录名 StickFightTheGame、exe StickFight.exe、Data 目录 StickFight_Data；**64 位更新后可能改名** → v2 必须兼容探测两种命名。注册表还收录中文别名「火柴人大乱斗」。

## 1. BepInEx 5 pack 清单与可程序化程度
**v5.4.23.5 win_x64**（最新 5.x，22 个文件）根目录：.doorstop_version（内容 4.5.0）、doorstop_config.ini（1460B 新格式 [General]+target_assembly=+[UnityMono]）、winhttp.dll（26,112B，Doorstop 4.5.0，SHA-256 8c6cdbc3…75412c）、changelog.txt；BepInEx/core/：0Harmony.dll(+xml)、0Harmony20.dll、BepInEx.dll(+xml)、BepInEx.Harmony.dll、BepInEx.Preloader.dll、HarmonyXInterop.dll、Mono.Cecil{,.Mdb,.Pdb,.Rocks}.dll、MonoMod.RuntimeDetour.dll、MonoMod.Utils.dll。
**v5.4.21/22**（各 21 文件）：**无 .doorstop_version**；ini 旧键名（[UnityDoorstop]+targetAssembly=，891B）；winhttp.dll 25,088B（Doorstop 3.x）。
三个包都不含 BepInEx/{plugins,patchers,config,cache} 空目录（首启由 Preloader 创建）。
→ 安装动作 = 纯文件复制（根 3–4 文件 + core 目录），**可程序化程度极高**；版本判别读 .doorstop_version 与 ini 键名格式。v2 建议直接用 5.4.23.5（含 Unity 6 修复，Doorstop 4 CLI 更友好）。

## 2. Doorstop 机制对安装器的影响
- winhttp.dll 代理劫持：Unity 启动加载游戏目录 winhttp.dll，Doorstop 转发真件并按 ini/CLI 注入 BepInEx.Preloader.dll。CLI：--doorstop-enabled <bool>、--doorstop-target-assembly <path>；env：DOORSTOP_* 系列；enabled=false / --doorstop-enable false 可不删文件禁用。
- 覆盖前唯一必须备份的是已存在的 winhttp.dll（连同 .doorstop_version / doorstop_config.ini）；哈希不认识的先改名 winhttp.dll.lunaqua-bak.<ts>。
- **位数不匹配 = 游戏启动失败**：必须读 exe PE 头 Machine（0x8664=x64 / 0x14c=x86）选包，不要信注册表。
- 杀软误报是代理 DLL 固有属性 → 失败分支要专门话术。
- BepInEx 5 (Mono) 补丁在内存、缓存写 BepInEx/cache，**不改 Managed 目录** → 「干净校验」只需对 *_Data/Managed/*.dll 做哈希；装坏删 BepInEx 目录即还原。

## 3. 「原版干净/版本识别」哈希方案
| 途径 | 可得 | 结论 |
|---|---|---|
| steamcmd app_info_print | depot/分支 manifest gid；逐文件哈希需持有游戏账号 | 维护者离线养表 |
| SteamDB | build 历史 | 403 反爬 + ToS → 不可编程依赖 |
| **DepotDownloader** | -manifest-only -filelist 输出含逐文件校验的 manifest | **推荐**：每次更新跑一次生成指纹表随 Lunaqua 发布 |
| appmanifest buildid | 准确构建号，零成本 | **主键**（Steam 版） |
| 本机首扫缓存 | 本机基线 | 兜底；脏基线风险 → 需 official 命中或 user-trusted |

**推荐三层判定**：buildid（主键）+ 官方构建指纹表（随版本发布，字段：buildid、Assembly-CSharp.dll SHA-256+size、UnityEngine.CoreModule.dll SHA-256、exe SHA-256、构建标记）+ 本机首扫基线（兜底，标 verified:official / user-trusted）。
**失配降级**：buildid 有表 → 静默通过；无表 → 引导 steam://validate/674940 → 等 appmanifest 变化 → 复扫 → 仍无 → 「上报哈希」/「信任并继续(user-trusted)」双按钮；非 Steam 版只能复扫 + user-trusted。

## 4. 参考实现
- **r2modman**（源码级）：ModLinker 只把 profile 根部的 winhttp.dll / doorstop_config.ini / .doorstop_version 复制进游戏目录（幂等去重）；启动用 Steam.exe -applaunch <appid> + Doorstop 参数（v4 --doorstop-enabled true --doorstop-target-assembly …；原版模式 --doorstop-enable false）；游戏元数据来自 thunderstore 生态 API（本地 ecosystem.json 兜底）；修复 = 清空游戏目录 + steam://validate/<appid>（副作用大，v2 建议分级）。
- **Gale**（Tauri/Rust）：steamlocate + new-vdf-parser；五平台抽象；印证「定位层独立可单测」。LimeLoader 是 Android/IL2CPP，**不适用**。

## 5. 推荐全自动流程（状态机）
1. 环境探测：注册表 HKCU\SOFTWARE\Valve\Steam\SteamPath（兜底 HKLM WOW6432Node InstallPath）→ config/libraryfolders.vdf（兼容 steamapps/libraryfolders.vdf）→ 各库 steamapps/appmanifest_674940.acf（读 installdir/buildid/StateFlags，4=Fully Installed）→ 游戏根 = <库>\steamapps\common\<installdir>。
2. 定位与体检：结构校验（StickFight.exe 或 StickFightTheGame.exe + <exe>_Data\Managed\Assembly-CSharp.dll）+ PE 位数（必须 x64）+ 识别旧 BepInEx/Lunaqua 残留。
3. 干净判定（三层，见 §3）。
4. 备份：进程检测 → winhttp.dll/doorstop 文件/BepInEx 目录 → %LocalAppData%\Lunaqua\backups\<日期> + manifest.json。
5. 部署：复制 5.4.23.5 x64 pack → 逐文件 SHA-256 自校验 → ini 键格式断言。
6. 运行时验证：启动游戏（steam://rungameid/674940 或 exe）→ ≤60s 轮询 BepInEx\LogOutput.log 新 mtime 且含 BepInEx 5.4.23 → 成功；失败读日志尾部分类（位数/杀软/Harmony）→ 一键回滚。
7. 成功页：大 ✓ + 「从 Steam 点开始也生效」+ 卸载/禁用说明。

失败话术要点：找不到游戏→手动选目录（认 StickFight.exe）；32 位→提示先更新游戏；陌生 winhttp.dll→自动备份后覆盖并告知；文件不干净→自动调 steam://validate 并说明；杀软拦截→加白名单后重试（附图文）；首启无日志→收集日志 + 一键还原。

## 6. 待人工复核（发布前）
1. 站上 BepInEx pack 实际内容与 x64 适配（子代理侧 https://blog.monblog.top/BepInEx 404，需在 OpenList 站内实际路径核对）。
2. 64 位更新后真实 exe 名与 Data 目录名（StickFight.exe+StickFight_Data vs StickFightTheGame.exe+StickFightTheGame_Data）。
3. 「Steam 验证不删 pack 文件」与 steam://validate 在国区/家庭共享下的行为。
4. 用持有游戏的账号实跑 DepotDownloader 生成 674940 当前构建指纹表。

来源：https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5 ；https://github.com/NeighTools/UnityDoorstop ；https://github.com/ebkr/r2modmanPlus ；https://thunderstore.io/api/experimental/schema/dev/latest/ ；https://store.steampowered.com/api/appdetails?appids=674940 ；https://github.com/SteamRE/DepotDownloader ；https://github.com/Kesomannen/gale ；https://crates.io/crates/steamlocate 。
