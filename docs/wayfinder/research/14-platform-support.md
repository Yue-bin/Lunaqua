# 调研 14：macOS / Linux 平台支持

- 日期：2026-10-11
- 状态：事实已取证；实现未排期（决议见规格书 §14）
- 一句话：**Linux 玩家跑的是 Proton 下的 Windows 构建 → mod 一行都不用改；macOS 是原生 Mach-O → BepInEx 有官方 mac 包但 UVFS 原生层不可能支持。**

---

## 1. Steam 侧（一手：Steam PICS `app_info`，2026-10-11 实测）

`common.oslist = "windows,macos"` —— **Steam 认可的平台只有 Windows 与 macOS，没有 Linux**。

`config.launch` 只有两条：

| # | oslist | executable |
|---|---|---|
| 0 | windows | `StickFight.exe` |
| 1 | macos | `StickFight.app/Contents/MacOS/StickFight` |

**没有 linux 启动项。**

depots：

| depot | oslist | osarch | size（字节） | 说明 |
|---|---|---|---|---|
| 674941 | windows | — | 392,891,569 | 32 位 |
| 674945 | windows | 64 | 398,198,449 | **64 位 ≈ 线上指纹 build 24952802 那一份** |
| 674942 | macos | — | 405,623,097 | 32 位 |
| 674944 | macos | 64 | 409,009,614 | 64 位（x86_64，**无 arm64 depot**） |
| 674943 | linux | — | 374,454,681 | **有内容，但无启动项、也不在 oslist 里** |

推论：

- **Linux**：Steam 判定「不支持 Linux」→ 走 Steam Play/Proton → 装的是 **Windows depot（→ 674945 x64）**，`steamapps/common/StickFightTheGame` 里就是 Windows PE 环境。**对 mod 而言与 Windows 完全同构**。
- **macOS**：原生构建，Apple Silicon 靠 Rosetta 2 跑 x86_64。
- depot 674943 的存在是疑点（疑为弃用的实验构建）；**不要基于它做任何设计**（Steam 也不会装它）。

## 2. BepInEx 侧（一手：GitHub Releases + 官方文档）

5.4.23.5 官方资产：

```
BepInEx_win_x86 / win_x64 / linux_x86 / linux_x64 / macos_universal / Patcher
```

（5.4.23.4 还是 `macos_x64`，到 5.4.23.5 变 `macos_universal`。）

文档要求：Windows 7+ / Linux（GCC 10+，x86_64 与 x86）/ **macOS 10.13+**；Unity 3+；**IL2CPP 不支持**。我们是 Unity **5.6.7 Mono** → 平台要求全部满足。

两条官方路径：

1. **原生 Unix 游戏**（macOS 就是这种）：必须经由 `run_bepinex.sh` 启动 —— `chmod u+x run_bepinex.sh` + Steam 启动选项 `./run_bepinex.sh %command%`。原因：Steam 会直接跑游戏可执行文件并可能重启它，绕过 doorstop。
2. **Windows 游戏跑在 Proton/Wine**（Linux 就是这种）：UnityDoorstop 依赖的 `winhttp.dll` 代理**默认不生效**，必须加 DLL override。官方文档给的是 winecfg / `protontricks` 加 `winhttp` override。

## 3. UVFS 侧（一手：读 `E:\codes\UVFS` 源码与计划文档）

- native 层 **Windows 独占**：`#include <windows.h>`、`__declspec(dllexport)`、`WINAPI`、`WIN32_FIND_DATAW`、`GetPrivateProfileIntW`
- 钩子用 **MinHook**：`MH_CreateHookApi(L"ntdll.dll","NtCreateFile"/"NtOpenFile"/"NtQueryAttributesFile"/"NtQueryFullAttributesFile")` + `kernel32!GetFileAttributesW/GetFileAttributesExW/FindFirstFileW/FindNextFileW/FindClose`（`native/src/hooks.cpp`）
- 构建：C++17 / CMake 3.27 / VS2022，双架构 `overlay32.dll` + `overlay64.dll`；`scripts\deploy.cmd` 部署
- ⚠️ **但它 v1 的验证环境是 32 位**：计划文档写「VS2022 (v143) **Win32**」「加载 `overlay32.dll`（x86 native）」「Win32 (x86) 下 stdcall 导出名带 `_name@N`」，README 写「v1：Unity **5.6.4** Mono/**x86** 验证」「游戏需已装 BepInEx 5.4.21+ **x86**」
- 而**线上是 64 位**：站上装的是 `BepInEx_pack_x64`，实机 `LogOutput.log` 出现 `Bits64`，对应 depot 674945（osarch 64）；Unity 实测 **5.6.7**（≠ 5.6.4）
- → 结论：**UVFS 的 x64 路径（overlay64）在当前线上形态下尚未验证**。x64 目标在 CMake 工程里存在（`build64/`、README 写「双架构都装即可，patcher 按游戏位数自动选择」），但没看到 x64 的实机验证记录。站上目前 **9 个 mod 全是 BepInEx 插件**（`stick.plugins.*` + `z7572.DesyncFixer`），没有 UVFS mod，所以现在没有踩到

平台判断：

| | 可行性 | 依据 |
|---|---|---|
| Windows | ✅ 现状 | — |
| Linux / Proton | ⚠️ **理论可行，必须实测** | PE dll 在 Wine 里能 `LoadLibrary`，钩的是 Wine 的 ntdll/kernel32；但 MinHook 的 trampoline 在 Wine 上不是保证可用 |
| macOS 原生 | ❌ **不可能** | PE dll 进不了 Mach-O 进程，MinHook 的 x86/x64 trampoline 也不适用；要支持必须把 native 层重写为 fishhook / DYLD interposing —— 那是另一个项目 |

## 4. 平台矩阵（结论）

| | 游戏本体 | BepInEx 托管插件 | UVFS |
|---|---|---|---|
| Windows x64 | PE（现状） | ✅ | ✅（x64 待验证） |
| Linux / SteamOS | Proton 跑 Windows PE | ✅ **同一份 dll，零重编译**（需 `winhttp` override） | ⚠️ 待实测 |
| macOS（x86_64 / Rosetta） | 原生 Mach-O | ⚠️ 有官方 mac 包，需实机验证 | ❌ 不支持 |

**对 mod 作者的含义**：**不需要为 Linux 编译任何东西**（Proton 就是 Windows 环境）；mac 是否需要单独编译取决于插件是否平台相关 —— 托管插件理论上跨平台（同一份 dll），但它引用的是 mac 版 `Assembly-CSharp.dll` / `UnityEngine.dll`，且插件里若 P/Invoke Windows API 或按平台条件编译就会挂，**必须实机验证**。

## 5. Lunaqua 客户端缺口（代码级）

1. `SteamLibraryLocator.ReadSteamPathFromRegistry()` 在非 Windows 直接返回 null（`OperatingSystem.IsWindows()` 守卫）→ 需要补非 Windows 的 Steam 根探测：`~/.steam/steam`、`~/.local/share/Steam`、`~/Library/Application Support/Steam`
2. **大小写敏感文件系统**：`StringComparer.OrdinalIgnoreCase` 用在**本地路径**比较的地方在 Linux 上语义错误，需按平台选 comparer（`InstallEngine` 的 `sources`/`newPaths`、`BepInExModStrategy.destinations`、`ConfigEditorService` 的备份集合、`GameFingerprintTableLoader` 之类的表比对）
3. `libraryfolders.vdf` 自研解析本身跨平台；但 `Path.Combine` / 分隔符替换（`Replace('/', Path.DirectorySeparatorChar)`）要逐处确认
4. 启动游戏：Windows 现走 `steam://`；Linux 用 `xdg-open steam://rungameid/674940`、macOS 用 `open steam://rungameid/674940`
5. BepInEx 包：**Linux/Proton 复用现有 windows x64 pack**（23 文件 `data/bepinex-pack.json`）；macOS 需要另建 `macos_universal` 清单（22 文件 + 预置 cfg）
6. 指纹表：Linux/Proton 与 Windows **同一份**（同一 depot）；macOS 要另采（depot 674944），`data/game-fingerprints.json` 需支持按平台分组
7. `GameProcessDetector`：Windows 与 Proton 下进程名都是 `StickFight.exe`；macOS 原生是 `StickFight`
8. 分发：Velopack 支持 Linux/macOS，但 **macOS 必须签名 + 公证**（Apple Developer 账号，否则 Gatekeeper 拦截）；Linux 建议 AppImage
9. schema 需要 `platforms` 字段（规格书 §4.2）

## 6. 待人工实测（开发机做不了）

1. macOS：装 `BepInEx_macos_universal` + `./run_bepinex.sh %command%` 启动选项 → 跑一个现有插件（如 playermanager），看 `LogOutput.log`
2. macOS：确认 mac 构建的 Unity 版本（Windows 侧实测 5.6.7）
3. Linux/Proton：`winhttp` override（官方给的 winecfg/protontricks 路径，或社区常用的 `WINEDLLOVERRIDES="winhttp=n,b" %command%` —— 后者需实测确认）后 BepInEx 是否加载
4. Linux/Proton：UVFS `overlay64.dll` 在 Wine 下 MinHook 能否成功
5. macOS：Lunaqua 自身（Avalonia net10.0）能否启动并定位到游戏目录
6. Windows：UVFS x64（overlay64）在 Unity 5.6.7 / BepInEx x64 下的实际表现

## 7. 来源

- Steam PICS app_info：`https://api.steamcmd.net/v1/info/674940`（实测 2026-10-11）
- BepInEx Releases：`https://api.github.com/repos/BepInEx/BepInEx/releases`
- BepInEx 安装文档：<https://docs.bepinex.dev/articles/user_guide/installation/index.html>
- BepInEx 原生 Unix 经 Steam 启动：<https://docs.bepinex.dev/articles/advanced/steam_interop.html>
- BepInEx Proton/Wine：<https://docs.bepinex.dev/articles/advanced/proton_wine.html>
- UVFS 源码：`E:\codes\UVFS\`（`README.md`、`native/src/hooks.cpp`、`docs/superpowers/plans/2026-08-20-uvfs-v1.md`）
