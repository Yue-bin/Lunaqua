using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mono.Cecil;

const string GiteaBase = "https://git.monblog.top/api/v1";

var baseUrl = (args.Length > 0 ? args[0] : "https://blog.monblog.top/openlist").TrimEnd('/');
var outDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "lunaqua-backfill");
var org = args.Length > 2 ? args[2] : "Stick_Mods";
Directory.CreateDirectory(outDir);

// 注意：目标站与自建 Gitea 的证书链可能不完整（PartialChain），此迁移工具显式放宽校验。
var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Lunaqua-Backfill/1.0");
var summary = new StringBuilder();
var warningsAll = new List<string>();

async Task<JsonNode?> Api(string path, object body)
{
    var resp = await http.PostAsync(baseUrl + path, JsonContent.Create(body));
    var text = await resp.Content.ReadAsStringAsync();
    try { return JsonNode.Parse(text); } catch { return null; }
}

async Task<JsonNode?> FsList(string path)
{
    var j = await Api("/api/fs/list", new { path, page = 1, per_page = 500 });
    if (j?["code"]?.GetValue<int>() != 200) return null;
    return j["data"];
}

async Task<byte[]?> Download(string path)
{
    var j = await Api("/api/fs/get", new { path });
    var url = j?["data"]?["raw_url"]?.GetValue<string>();
    if (url is null) return null;
    return await http.GetByteArrayAsync(url);
}

DllInfo ParseDll(byte[] bytes)
{
    using var ms = new MemoryStream(bytes);
    using var asm = AssemblyDefinition.ReadAssembly(ms);
    string? guid = null, name = null, version = null;
    var deps = new List<(string Guid, string? Version)>();
    void Walk(TypeDefinition t)
    {
        foreach (var a in t.CustomAttributes)
        {
            var fn = a.AttributeType.FullName;
            if (fn == "BepInEx.BepInPlugin")
            {
                guid = a.ConstructorArguments.Count > 0 ? a.ConstructorArguments[0].Value?.ToString() : null;
                name = a.ConstructorArguments.Count > 1 ? a.ConstructorArguments[1].Value?.ToString() : null;
                version = a.ConstructorArguments.Count > 2 ? a.ConstructorArguments[2].Value?.ToString() : null;
            }
            else if (fn == "BepInEx.BepInDependency")
            {
                var g = a.ConstructorArguments.Count > 0 ? a.ConstructorArguments[0].Value?.ToString() : null;
                var v = a.ConstructorArguments.Count > 1 ? a.ConstructorArguments[1].Value?.ToString() : null;
                if (g != null) deps.Add((g, v));
            }
        }
        foreach (var n in t.NestedTypes) Walk(n);
    }
    foreach (var t in asm.MainModule.Types) Walk(t);
    return new DllInfo(guid, name, version, deps);
}

async Task<string?> DefaultBranch(string repo)
{
    var resp = await http.GetAsync($"{GiteaBase}/repos/{org}/{repo}");
    if (!resp.IsSuccessStatusCode) return null;
    try
    {
        var j = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        return j?["default_branch"]?.GetValue<string>();
    }
    catch { return null; }
}

async Task<string?> FetchReadme(string repo)
{
    var branches = new List<string>();
    var def = await DefaultBranch(repo);
    if (def is not null) branches.Add(def);
    branches.Add("master"); branches.Add("main");
    foreach (var branch in branches.Distinct())
    foreach (var fileName in new[] { "readme.md", "README.md", "Readme.md" })
    {
        var resp = await http.GetAsync($"{GiteaBase}/repos/{org}/{repo}/contents/{fileName}?ref={branch}");
        if (!resp.IsSuccessStatusCode) continue;
        try
        {
            var j = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
            var b64 = j?["content"]?.GetValue<string>();
            if (b64 is not null) return Encoding.UTF8.GetString(Convert.FromBase64String(b64.Replace("\n", "").Replace("\r", "")));
        }
        catch { }
    }
    return null;
}

async Task<List<string>> OrgRepos()
{
    var list = new List<string>();
    for (var page = 1; page <= 5; page++)
    {
        var resp = await http.GetAsync($"{GiteaBase}/orgs/{org}/repos?page={page}&limit=50");
        if (!resp.IsSuccessStatusCode) break;
        if (JsonNode.Parse(await resp.Content.ReadAsStringAsync()) is not JsonArray arr || arr.Count == 0) break;
        foreach (var r in arr) { var n = r?["name"]?.GetValue<string>(); if (!string.IsNullOrEmpty(n)) list.Add(n!); }
    }
    return list;
}

string? Section(string md, string heading)
{
    var m = Regex.Match(md, $@"^##\s+{Regex.Escape(heading)}\s*$", RegexOptions.Multiline);
    if (!m.Success) return null;
    var start = m.Index + m.Length;
    var rest = md[start..];
    var next = Regex.Match(rest, @"^##\s+", RegexOptions.Multiline);
    return (next.Success ? rest[..next.Index] : rest).Trim();
}

string? H1(string md)
{
    var m = Regex.Match(md, @"^#\s+(.+)$", RegexOptions.Multiline);
    return m.Success ? m.Groups[1].Value.Trim() : null;
}

string Clean(string? md, int max = 220)
{
    if (string.IsNullOrWhiteSpace(md)) return "";
    var lines = md.Split('\n')
        .Select(l => Regex.Replace(l.Trim(), @"^[-*+]\s+|^\d+\.\s+", ""))
        .Where(l => l.Length > 0 && !l.StartsWith('#'))
        .ToList();
    var s = string.Join("、", lines.Take(4)).Replace("**", "").Replace("`", "");
    return s.Length > max ? s[..max] + "…" : s;
}

Dictionary<string, string> Changelogs(string? md)
{
    var result = new Dictionary<string, string>();
    if (string.IsNullOrWhiteSpace(md)) return result;
    var sec = Section(md!, "更新日志");
    if (sec is null) return result;
    var parts = Regex.Split(sec, @"^###\s+", RegexOptions.Multiline);
    foreach (var p in parts.Skip(1))
    {
        var nl = p.IndexOf('\n');
        if (nl < 0) continue;
        var ver = p[..nl].Trim().TrimStart('v', 'V').Trim();
        var body = p[(nl + 1)..].Trim();
        if (ver.Length > 0) result[ver] = body;
    }
    return result;
}

(int[] Parts, string Pre)? Semver(string v)
{
    var m = Regex.Match(v.Trim().TrimStart('v', 'V'), @"^(\d+)\.(\d+)\.(\d+)(?:[-+](.*))?$");
    if (!m.Success) return null;
    return (new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value) }, m.Groups[4].Value);
}

int CompareSemver(string a, string b)
{
    var sa = Semver(a); var sb = Semver(b);
    if (sa is null && sb is null) return string.CompareOrdinal(a, b);
    if (sa is null) return -1;
    if (sb is null) return 1;
    for (var i = 0; i < 3; i++) { var c = sa.Value.Parts[i].CompareTo(sb.Value.Parts[i]); if (c != 0) return c; }
    var pa = sa.Value.Pre; var pb = sb.Value.Pre;
    if (pa.Length == 0 && pb.Length == 0) return 0;
    if (pa.Length == 0) return 1;
    if (pb.Length == 0) return -1;
    return string.CompareOrdinal(pa, pb);
}

var repos = await OrgRepos();
Console.WriteLine($"[gitea] {org} 仓库数：{repos.Count}");

var root = await FsList("/");
if (root is null) { Console.WriteLine("无法列出站点根目录"); return; }
var modDirs = root["content"]!.AsArray()
    .Where(x => x?["is_dir"]?.GetValue<bool>() == true)
    .Select(x => x!["name"]!.GetValue<string>()!)
    .Where(n => !n.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
    .ToList();
Console.WriteLine($"[site] 目录：{string.Join(", ", modDirs)}");

foreach (var dir in modDirs)
{
    Console.WriteLine($"\n=== {dir} ===");
    var listing = await FsList("/" + dir);
    if (listing is null) { warningsAll.Add($"{dir}: 列目录失败"); continue; }
    var entries = listing["content"]!.AsArray().Where(x => x?["is_dir"]?.GetValue<bool>() != true).ToList();
    var modWarn = new List<string>();
    var fileRecords = new List<(string Name, long Size, string Modified, string Sha, DllInfo? Dll)>();

    foreach (var e in entries)
    {
        var name = e!["name"]!.GetValue<string>()!;
        var modified = e["modified"]?.GetValue<string>() ?? "";
        var bytes = await Download($"/{dir}/{name}");
        if (bytes is null) { modWarn.Add($"下载失败：{name}"); continue; }
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        DllInfo? dll = null;
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            try { dll = ParseDll(bytes); }
            catch (Exception ex) { modWarn.Add($"解析失败 {name}: {ex.GetType().Name}"); }
        }
        fileRecords.Add((name, bytes.Length, modified, sha, dll));
        Console.WriteLine($"  {name}  {bytes.Length} B  {dll?.Guid ?? "-"}  {dll?.Version ?? "-"}");
    }

    // 认领本 mod 的 dll：GUID 与目录名匹配（忽略大小写/点号），否则取第一个可解析的
    var key = dir.Replace(".", "").ToLowerInvariant();
    var own = fileRecords.Where(f => f.Dll?.Guid != null && f.Dll!.Guid!.Replace(".", "").ToLowerInvariant().Contains(key)).ToList();
    var foreign = fileRecords.Where(f => f.Dll?.Guid != null && !own.Contains(f)).ToList();
    if (own.Count == 0)
    {
        var first = fileRecords.FirstOrDefault(f => f.Dll?.Guid != null);
        if (first.Dll != null) { own.Add(first); foreign = fileRecords.Where(f => f.Dll?.Guid != null && f.Name != first.Name).ToList(); }
    }
    foreach (var f in foreign) modWarn.Add($"跳过非本 mod 的 dll：{f.Name}（GUID {f.Dll!.Guid}）");

    var ownDlls = own.Where(f => f.Dll?.Version != null).ToList();
    var versions = ownDlls.Select(f => f.Dll!.Version!).Distinct().OrderByDescending(v => v, Comparer<string>.Create(CompareSemver)).ToList();
    if (versions.Count == 0) { modWarn.Add("没有可解析版本的 dll，跳过"); warningsAll.AddRange(modWarn.Select(w => $"{dir}: {w}")); continue; }
    foreach (var v in versions) if (Semver(v) is null) modWarn.Add($"非 SemVer 版本号：{v}");
    foreach (var f in ownDlls)
    {
        var fm = Regex.Match(f.Name, @"(\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.\-]+)?)");
        if (fm.Success && f.Dll?.Version != null && fm.Groups[1].Value != f.Dll!.Version)
            modWarn.Add($"文件名版本 {fm.Groups[1].Value} 与 dll 内嵌版本 {f.Dll.Version} 不一致：{f.Name}");
    }

    var readmeRepo = repos.FirstOrDefault(r => r.Equals(dir, StringComparison.OrdinalIgnoreCase))
        ?? repos.FirstOrDefault(r => r.Contains(dir, StringComparison.OrdinalIgnoreCase) || dir.Contains(r, StringComparison.OrdinalIgnoreCase));
    var readme = readmeRepo is null ? null : await FetchReadme(readmeRepo);
    if (readme is null) modWarn.Add("未取到 readme（name/author/description/changelog 留空）");
    var changelogs = Changelogs(readme);

    var versionNodes = new JsonArray();
    foreach (var v in versions)
    {
        var files = new JsonArray();
        foreach (var f in ownDlls.Where(f => f.Dll!.Version == v))
            files.Add(new JsonObject { ["path"] = f.Name, ["sha256"] = f.Sha, ["size"] = f.Size });
        // 非 dll 附件（如 StreamingAssets.zip）挂到最新版
        if (v == versions[0])
            foreach (var f in fileRecords.Where(f => f.Dll is null && !f.Name.StartsWith("readme", StringComparison.OrdinalIgnoreCase) && !f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase)))
                files.Add(new JsonObject { ["path"] = f.Name, ["sha256"] = f.Sha, ["size"] = f.Size });

        var node = new JsonObject
        {
            ["version"] = v,
            ["released"] = ownDlls.First(f => f.Dll!.Version == v).Modified,
            ["files"] = files
        };
        if (changelogs.TryGetValue(v, out var cl) && cl.Length > 0) node["changelog"] = cl;
        versionNodes.Add(node);
    }

    var deps = ownDlls.SelectMany(f => f.Dll!.Deps).Where(d => !d.Guid.Equals(dir, StringComparison.OrdinalIgnoreCase)).GroupBy(d => d.Guid).Select(g => g.First()).ToList();
    var depArray = new JsonArray();
    foreach (var d in deps)
    {
        var o = new JsonObject { ["id"] = d.Guid };
        if (!string.IsNullOrWhiteSpace(d.Version) && Semver(d.Version!) is not null) o["minVersion"] = d.Version;
        depArray.Add(o);
    }

    var latestDll = ownDlls.First(f => f.Dll!.Version == versions[0]).Dll!;
    var info = new JsonObject
    {
        ["schema"] = 1,
        ["id"] = dir,
        ["type"] = "bepinex",
        ["guid"] = latestDll.Guid,
        ["name"] = H1(readme ?? "") ?? latestDll.Name ?? dir,
        ["author"] = Clean(Section(readme ?? "", "作者"), 120),
        ["description"] = Clean(Section(readme ?? "", "功能")),
        ["latest"] = versions[0],
        ["dependencies"] = depArray,
        ["versions"] = versionNodes
    };

    var dirOut = Path.Combine(outDir, dir);
    Directory.CreateDirectory(dirOut);
    var json = info.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    await File.WriteAllTextAsync(Path.Combine(dirOut, "info.json"), json, new UTF8Encoding(false));

    summary.AppendLine($"## {dir}");
    summary.AppendLine($"- guid: {latestDll.Guid}｜latest: **{versions[0]}**｜版本数: {versions.Count}｜文件数: {fileRecords.Count}");
    summary.AppendLine($"- 文案来源: {(readme is null ? "（缺 readme）" : readmeRepo)}");
    if (modWarn.Count > 0) { summary.AppendLine("- ⚠ 需人工确认："); foreach (var w in modWarn) summary.AppendLine($"  - {w}"); }
    else summary.AppendLine("- ✅ 无警告");
    summary.AppendLine();
    warningsAll.AddRange(modWarn.Select(w => $"{dir}: {w}"));
    Console.WriteLine($"  -> 写出 {dir}/info.json（{versions.Count} 版本）");
}

var header = new StringBuilder();
header.AppendLine("# Lunaqua v2 — 存量 9 个 mod 的 info.json 补录草案");
header.AppendLine();
header.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
header.AppendLine($"- 来源站：{baseUrl}（OpenList）");
header.AppendLine($"- 文案来源：{GiteaBase}/orgs/{org}/repos 的 readme.md");
header.AppendLine($"- 目录：本目录下每个 mod 一个子目录，内含可直接落站的 info.json");
header.AppendLine();
header.AppendLine("## 人工审核要点");
header.AppendLine();
header.AppendLine("1. **released 字段**用站上文件的 modified 时间近似（补录拿不到 git tag 日期）——如需精确，改为 tag 日期后重推。");
header.AppendLine("2. **name/author/description/changelog** 从 readme 解析；缺 readme 的 mod 需人工补文案。");
header.AppendLine("3. **非 SemVer 版本号**会在下方列出；按规格书 §4.2 要求应统一为 SemVer。");
header.AppendLine("4. 非 dll 附件（如 StreamingAssets.zip）默认挂在最新版；如属特定版本请手动调整。");
header.AppendLine("5. 历史上改名换 GUID 的 dll（如 NaNFixer）按约定**不写入 versions[]**，需从站上删除。");
header.AppendLine();
header.AppendLine($"## 概览（{modDirs.Count} 个目录）");
header.AppendLine();
header.Append(summary);
header.AppendLine("## 全部警告");
header.AppendLine();
if (warningsAll.Count == 0) header.AppendLine("- 无");
else foreach (var w in warningsAll) header.AppendLine($"- {w}");
header.AppendLine();
header.AppendLine("## 推送方式");
header.AppendLine();
header.AppendLine("把每个子目录的 info.json 用 SFTP 推到站上对应 mod 目录根部（与 dll 同级），然后在管理器里刷新验证。");

await File.WriteAllTextAsync(Path.Combine(outDir, "SUMMARY.md"), header.ToString(), new UTF8Encoding(false));
Console.WriteLine($"\n完成 → {outDir}");

record DllInfo(string? Guid, string? Name, string? Version, List<(string Guid, string? Version)> Deps);