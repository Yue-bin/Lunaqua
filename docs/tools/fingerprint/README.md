# 官方构建指纹表生成器

给「柴」（Stick Fight: The Game，AppID **674940**）生成 `data/game-fingerprints.json`，
供 Lunaqua 的「干净判定」第 ① ② 层使用（规格书 §8）。

## 跑之前你需要

- Windows + 已装 Steam 与该游戏
- Git Bash（本仓库开发环境已有）
- `node`（仓库里已经在用）
- 可选：`winget install --exact --id SteamRE.DepotDownloader`（补历史 build 才需要）

## 当前状态（2026-09-09）

当前 public build **24952802** 的指纹**已经采好**（5 个文件，见 `data/game-fingerprints.json`），
不需要再跑向导；游戏下次大更新后重跑一次即可。

## 怎么跑

**别用 `bash`**：Windows 上的 `bash` 常常指向 WSL，会因内存不足报
`Bash/Service/CreateInstance/CreateVm/HCS/0x800705aa`。用 Git Bash：

```bat
docs\tools\fingerprint\wizard.cmd
```

或直接指定 Git Bash：

```bash
"C:\Program Files\Git\bin\bash.exe" docs/tools/fingerprint/wizard.sh
```

向导共 6 站，会替你做掉能自动做的部分，只让你做机器做不了的部分：

| 站 | 你做什么 | 向导做什么 |
|---|---|---|
| 1 | 看说明 | 检查 DepotDownloader 是否就绪 |
| 2 | 看一眼数字 | 从 `api.steamcmd.net` 读当前 buildid / depot / manifest（匿名） |
| 3 | 可选：在 Steam 里点「验证游戏文件的完整性」 | 打开 `steam://validate/674940`，等你跑完 |
| 4 | 确认游戏目录 | 算 SHA-256 并写进指纹表 |
| 5 | 可选：粘贴 SteamDB 上的历史 manifest id | 调 DepotDownloader 只拉几个文件，再采集 |
| 6 | 提交文件 | 打印表内 build 数 |

> 历史 build 基本没用：没更新的玩家是 32 位旧版，根本进不去联机；真遇到了也不该用管理器。

## 指纹表格式

```json
{
  "schema": 1,
  "appId": "674940",
  "updatedAt": "2026-09-09T12:00:00.000Z",
  "builds": [
    {
      "buildId": "24952802",
      "depot": "674945",
      "manifest": "8864248428190094911",
      "exe": "StickFight.exe",
      "dataDir": "StickFight_Data",
      "capturedAt": "2026-09-09T20:00:00+08:00",
      "source": "local-capture",
      "files": [
        { "path": "StickFight.exe", "sha256": "…64 位小写十六进制…", "size": 22842880 }
      ]
    }
  ]
}
```

- `files` 至少含 exe 与 `Assembly-CSharp.dll`；有就一起收 `UnityEngine.dll`、`globalgamemanagers`、
  **`level0`**（Unity 主场景/资源包，109MB；有 mod 会覆盖它，所以必须进指纹）。
- `files` 为空 = 只登记了 buildid（第 ① 层「buildid 命中」可用，第 ② 层哈希比对跳过）。
- 同一 buildid 重复采集会覆盖旧行。

## 两个辅助脚本

```bash
# 采集一个目录，输出一行 JSON（向导第 4/5 站调用）
bash docs/tools/fingerprint/capture-fingerprint.sh <游戏目录> <buildId> [depot] [manifest] [输出.json]

# 把一行合并进表（按 buildId 去重）
node docs/tools/fingerprint/merge-fingerprint.mjs data/game-fingerprints.json <行.json>
```

## 已知事实（2026-09-09 实测）

- 当前 public build：**24952802**，Windows 64 位 depot **674945**，manifest **8864248428190094911**
- 安装目录名是 `StickFightTheGame`，但 exe 叫 `StickFight.exe`、Data 目录叫 `StickFight_Data`
  （所以「`<exe名>_Data`」规则成立，别按 installdir 推）
- 这版 Unity 布局是老的：Managed 里是 `UnityEngine.dll`，**没有** `UnityEngine.CoreModule.dll`，
  也没有 `data.unity3d`；主场景是 `StickFight_Data/level0`（109MB）
- 有 mod 会落位覆盖 `level0`（暂时不兼容 64 位，所以本机是原版）；干净判定支持把
  「已被已装 mod 覆盖的文件」排除出比对（`GameCleanCheckService.CheckAsync(..., excludedPaths)`），
  等 M6 接上凭证里的落位清单后自动填这个参数
- 32 位旧 depot 是 674941（只在玩家没更新到 64 位时才会用到）

## 注意

- 第 3 站的 Steam 校验会**覆盖被改过的游戏文件**（含你手改的 `Assembly-CSharp.dll`），
  但不会动 BepInEx 的 `winhttp.dll` / `BepInEx` 目录（它们不在官方清单里）。
- 别拿没校验过的本机文件当官方指纹：本仓库开发机上那份 `Assembly-CSharp.dll` 就比
  `Assembly-CSharp.dll.vanilla.bak` 大 2KB，多半被改过。
