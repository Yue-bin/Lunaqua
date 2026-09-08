#!/usr/bin/env bash
# 安装 linuxdevel/Avalonia-skills 到 DSH 用户级 skills 目录（摊平嵌套子 skill，使每个都可被 Skill 工具直接加载）
# 用法: bash docs/tools/install-avalonia-skills.sh
set -euo pipefail

REPO="https://github.com/linuxdevel/Avalonia-skills"
DEST="${DSH_HOME:-$HOME/.dsh}/skills"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "==> 克隆 $REPO"
git clone --depth=1 -q "$REPO" "$TMP"
SRC="$TMP/skills/avalonia"
[ -f "$SRC/SKILL.md" ] || { echo "错误: 仓库结构与预期不符（缺 $SRC/SKILL.md）"; exit 1; }

echo "==> 安装到 $DEST"
mkdir -p "$DEST"

# 只清理本仓库的命名空间 avalonia / avalonia-*
rm -rf "$DEST/avalonia" "$DEST"/avalonia-*

# 1) master router（只放 SKILL.md 与 LICENSE，子 skill 单独摊平）
mkdir -p "$DEST/avalonia"
cp "$SRC/SKILL.md" "$DEST/avalonia/"
[ -f "$SRC/LICENSE" ] && cp "$SRC/LICENSE" "$DEST/avalonia/" || true

# 2) 一级子 skill
for dir in "$SRC"/*/; do
    name="$(basename "$dir")"
    mkdir -p "$DEST/$name"
    cp -r "$dir". "$DEST/$name/"
    echo "    + $name"
done

# 3) 二级子 skill（avalonia-controls/*, avalonia-pro-max/*）→ 摊平为 <parent>-<child>，并改写 name 与路由表引用
for parent in avalonia-controls avalonia-pro-max; do
    [ -d "$SRC/$parent" ] || continue
    for childdir in "$SRC/$parent"/*/; do
        child="$(basename "$childdir")"
        flat="$parent-$child"
        mkdir -p "$DEST/$flat"
        cp -r "$childdir". "$DEST/$flat/"
        # frontmatter name 统一为摊平后的名字（DSH 以 name 作为 skill id）
        sed -i "0,/^name: /s|^name: .*|name: $flat|" "$DEST/$flat/SKILL.md"
        echo "    + $flat"
    done
    # 父 router 里的 `parent/child` 引用改成摊平名
    for childdir in "$SRC/$parent"/*/; do
        child="$(basename "$childdir")"
        sed -i "s|$parent/$child|$parent-$child|g" "$DEST/$parent/SKILL.md"
    done
done

echo "==> 完成。已安装："
ls -1d "$DEST"/avalonia* | sed 's|.*/|    |'
echo "==> 共 $(ls -1d "$DEST"/avalonia* | wc -l) 个 skill（含 master router）"
