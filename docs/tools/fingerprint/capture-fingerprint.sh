#!/usr/bin/env bash
# 从一个游戏目录采集官方构建指纹，输出一行 JSON（给 wizard.sh 用）。
#
# 用法: capture-fingerprint.sh <游戏目录> <buildId> [depot] [manifest] [输出文件]
set -euo pipefail

GAME_DIR="${1:?用法: capture-fingerprint.sh <游戏目录> <buildId> [depot] [manifest] [输出文件]}"
BUILD_ID="${2:?缺 buildId}"
DEPOT="${3:-}"
MANIFEST="${4:-}"
OUT="${5:-}"

if [ ! -d "$GAME_DIR" ]; then
  echo "目录不存在：$GAME_DIR" >&2
  exit 1
fi

EXE=""
for candidate in StickFight.exe StickFightTheGame.exe; do
  if [ -f "$GAME_DIR/$candidate" ]; then EXE="$candidate"; break; fi
done
if [ -z "$EXE" ]; then
  echo "没找到 StickFight.exe / StickFightTheGame.exe" >&2
  exit 1
fi

DATA_DIR=""
for d in "$GAME_DIR"/*_Data; do
  if [ -d "$d" ]; then DATA_DIR="$(basename "$d")"; break; fi
done
if [ -z "$DATA_DIR" ]; then
  echo "没找到 *_Data 目录" >&2
  exit 1
fi

json_files=""
count=0
for rel in \
  "$EXE" \
  "$DATA_DIR/Managed/Assembly-CSharp.dll" \
  "$DATA_DIR/Managed/UnityEngine.dll" \
  "$DATA_DIR/Managed/Assembly-CSharp-firstpass.dll" \
  "$DATA_DIR/globalgamemanagers"
do
  path="$GAME_DIR/$rel"
  [ -f "$path" ] || continue
  sha="$(sha256sum "$path" | cut -d' ' -f1)"
  size="$(stat -c%s "$path")"
  if [ "$count" -gt 0 ]; then json_files="$json_files,"; fi
  json_files="$json_files
    { \"path\": \"$rel\", \"sha256\": \"$sha\", \"size\": $size }"
  count=$((count + 1))
done

if [ "$count" -eq 0 ]; then
  echo "没找到任何关键文件" >&2
  exit 1
fi

row="{
  \"buildId\": \"$BUILD_ID\",
  \"depot\": \"$DEPOT\",
  \"manifest\": \"$MANIFEST\",
  \"exe\": \"$EXE\",
  \"dataDir\": \"$DATA_DIR\",
  \"capturedAt\": \"$(date -Iseconds)\", 
  \"source\": \"local-capture\",
  \"files\": [$json_files
  ]
}"

if [ -n "$OUT" ]; then
  printf '%s\n' "$row" > "$OUT"
  echo "✓ 已写入 $OUT（$count 个文件）" >&2
else
  printf '%s\n' "$row"
fi
