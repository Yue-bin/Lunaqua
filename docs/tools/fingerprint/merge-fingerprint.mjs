// 把一行指纹合并进指纹表（按 buildId 去重，新的覆盖旧的）。
//
// 用法: node merge-fingerprint.mjs <表文件> <行文件>
import { readFileSync, writeFileSync, existsSync } from "node:fs";

const [tablePath, rowPath] = process.argv.slice(2);
if (!tablePath || !rowPath) {
  console.error("用法: node merge-fingerprint.mjs <表文件> <行文件>");
  process.exit(1);
}

const row = JSON.parse(readFileSync(rowPath, "utf8"));
if (!row.buildId) {
  console.error("这一行没有 buildId，拒绝写入");
  process.exit(1);
}

const table = existsSync(tablePath)
  ? JSON.parse(readFileSync(tablePath, "utf8"))
  : { schema: 1, appId: "674940", builds: [] };

table.builds = (table.builds ?? []).filter((item) => item.buildId !== row.buildId);
table.builds.push(row);
table.builds.sort((a, b) => Number(b.buildId) - Number(a.buildId));
table.updatedAt = new Date().toISOString();
table.schema = table.schema ?? 1;

writeFileSync(tablePath, JSON.stringify(table, null, 2) + "\n");
console.log(`✓ ${tablePath} 现在有 ${table.builds.length} 个 build（最新 ${table.builds[0].buildId}）`);
