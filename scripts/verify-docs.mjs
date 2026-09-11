// 校验唯一文档入口、仓库内链接/锚点、JSON 示例和新手示例编译；不访问应用或生产设备。
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync, spawnSync } from "node:child_process";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = name => fs.readFileSync(path.join(root, name), "utf8");
const errors = [];
const entries = [...new Set(execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
  { cwd: root, encoding: "utf8" }).split("\0").filter(Boolean))]
  .filter(name => fs.existsSync(path.join(root, name)));
const docs = entries.filter(name => name.startsWith("docs/") && name.endsWith(".md")).sort();
const expected = ["docs/development.zh-CN.md", "docs/quickstart.zh-CN.md"];
if (JSON.stringify(docs) !== JSON.stringify(expected)) errors.push("docs 中只能维护使用指南和开发文档两份 Markdown。");
let links = 0, jsonExamples = 0;
const stripCode = text => text.replace(/^```[^\n]*\n[\s\S]*?^```\s*$/gm, "");
const anchors = text => {
  const clean = stripCode(text);
  const ids = [...clean.matchAll(/<a\s+id="([^"]+)"/g)].map(match => match[1]);
  const used = new Map();
  for (const match of clean.matchAll(/^#{1,6}\s+(.+)$/gm)) {
    const base = match[1].toLowerCase().replace(/<[^>]+>/g, "")
      .replace(/[^\p{L}\p{N}\p{M}_\-\s]/gu, "").replace(/ /g, "-");
    const count = used.get(base) ?? 0;
    ids.push(count ? `${base}-${count}` : base); used.set(base, count + 1);
  }
  return new Set(ids);
};
function checkLink(name, target) {
  // 也检查官网/NuGet 指回本仓库 main 文档的链接，不发网络请求。
  const prefix = "https://github.com/Z18393520308/AgenticUI-NET/blob/main/";
  const repoLink = target.startsWith(prefix);
  if (repoLink) target = target.slice(prefix.length);
  else if (/^[a-z][a-z0-9+.-]*:/i.test(target)) return;
  const [relative, fragment] = target.split("#");
  const destination = repoLink ? path.resolve(root, decodeURIComponent(relative))
    : relative ? path.resolve(root, path.dirname(name), decodeURIComponent(relative)) : path.join(root, name);
  links++;
  if (!fs.existsSync(destination)) { errors.push(`${name}: 目标不存在 ${target}`); return; }
  if (fragment && destination.endsWith(".md") && !anchors(fs.readFileSync(destination, "utf8")).has(decodeURIComponent(fragment)))
    errors.push(`${name}: 锚点不存在 ${target}`);
}
for (const name of entries.filter(name => name.endsWith(".md"))) {
  const text = read(name);
  for (const match of stripCode(text).matchAll(/\[[^\]\n]*\]\(([^)\s]+)\)/g)) checkLink(name, match[1]);
  for (const match of text.matchAll(/^```json\s*\n([\s\S]*?)^```/gm)) {
    try { JSON.parse(match[1]); jsonExamples++; }
    catch (error) { errors.push(`${name}: JSON 示例无效：${error.message}`); }
  }
}
for (const match of read("AgenticUI.NET.sln").matchAll(/^\s*(docs\\[^=]+?)\s*=/gm))
  checkLink("AgenticUI.NET.sln", match[1].replaceAll("\\", "/"));
for (const match of read("website/src/App.jsx").matchAll(/\$\{githubUrl\}\/blob\/main\/([^`]+)`/g))
  checkLink("README.md", match[1]);
const actionSection = read("src/AgenticUI.Core/Protocol.cs").split("public static class AgenticActions")[1]
  .split("public static class AgenticEvents")[0];
const reference = read("docs/development.zh-CN.md");
for (const match of actionSection.matchAll(/public const string \w+ = "([^"]+)"/g))
  if (!new RegExp(`\\b${match[1]}\\b`).test(reference)) errors.push(`开发文档缺少动作 ${match[1]}`);
if (errors.length) { console.error(errors.join("\n")); process.exit(1); }
console.log(`文档检查通过：2 份主文档，${links} 个本地/仓库文档链接，${jsonExamples} 个 JSON 示例。`);

if (process.argv.includes("--build-examples")) {
  // 从文档提取完整示例，只生成到 Git 忽略的 artifacts，不另外维护一份示例源码。
  const guide = read("docs/quickstart.zh-CN.md");
  const output = path.join(root, "artifacts", "documentation-examples");
  const files = new Map([...guide.matchAll(/<!-- verify-file: ([\w/.\-]+) -->\s*\n```\w+\n([\s\S]*?)\n```/g)]
    .map(match => [match[1], match[2] + "\n"]));
  for (const name of ["wpf/MainWindow.xaml", "wpf/MainWindow.xaml.cs", "wpf/App.xaml.cs", "winforms/Program.cs", "client/Program.cs"])
    if (!files.has(name)) throw new Error(`缺少完整文档示例 ${name}`);
  const version = guide.match(/dotnet add package AgenticUI\.Wpf --version ([\d.]+)/)?.[1];
  if (!version) throw new Error("无法确定使用指南的新手示例 NuGet 版本。");
  const sourceArg = process.argv.indexOf("--package-source");
  const sourceValue = sourceArg >= 0 ? process.argv[sourceArg + 1] : null;
  if (sourceArg >= 0 && (!sourceValue || sourceValue.startsWith("--")))
    throw new Error("--package-source 后必须给出本次打包目录。");
  const packageSource = sourceValue ? path.resolve(root, sourceValue) : null;
  if (packageSource) {
    for (const id of ["Core", "Remote", "Wpf", "WinForms"])
      if (!fs.existsSync(path.join(packageSource, `AgenticUI.${id}.${version}.nupkg`)))
        throw new Error(`本次打包目录缺少 AgenticUI.${id}.${version}.nupkg`);
  }
  const writeGenerated = (name, text) => {
    const file = path.resolve(output, name);
    if (!file.startsWith(output + path.sep)) throw new Error("示例路径越界。");
    fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, text);
  };
  for (const [name, text] of files) writeGenerated(name, text);
  const xml = value => value.replaceAll("&", "&amp;").replaceAll('"', "&quot;")
    .replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  // 每次验证使用独立缓存，避免旧同版本本地包掩盖实际打包/公开源问题。
  const cache = fs.mkdtempSync(path.join(output, packageSource ? "packages-local-" : "packages-public-"));
  writeGenerated("NuGet.Config", `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear />${packageSource ? `<add key="release" value="${xml(packageSource)}" />` : ""}<add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><clear />${packageSource ? '<packageSource key="release"><package pattern="AgenticUI.*" /></packageSource>' : ""}<packageSource key="nuget"><package pattern="*" /></packageSource></packageSourceMapping>
  <config><add key="globalPackagesFolder" value="${xml(cache)}" /></config>
</configuration>`);
  writeGenerated("wpf/App.xaml", '<Application x:Class="WpfQuickStart.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" StartupUri="MainWindow.xaml" />');
  for (const [group, name, framework, ui] of [
    ["wpf", "WpfQuickStart", "net8.0-windows", "WPF"],
    ["winforms", "WinFormsQuickStart", "net8.0-windows", "WindowsForms"],
    ["client", "ControlClient", "net8.0", null]
  ]) {
    const packages = ["AgenticUI.Remote", ...(group === "wpf" ? ["AgenticUI.Wpf"] : group === "winforms" ? ["AgenticUI.WinForms"] : [])];
    const project = `${group}/${name}.csproj`;
    writeGenerated(project, `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>${framework}</TargetFramework><OutputType>${ui ? "WinExe" : "Exe"}</OutputType><RootNamespace>${name}</RootNamespace><EnableWindowsTargeting>true</EnableWindowsTargeting>${ui ? `<Use${ui}>true</Use${ui}>` : ""}</PropertyGroup><ItemGroup>${packages.map(id => `<PackageReference Include="${id}" Version="${version}" />`).join("")}</ItemGroup></Project>`);
    const result = spawnSync("dotnet", ["build", path.join(output, project), "-c", "Release", "--nologo",
      `-p:RestoreConfigFile=${path.join(output, "NuGet.Config")}`], { cwd: root, stdio: "inherit" });
    if (result.error) throw result.error;
    if (result.status !== 0) process.exit(result.status ?? 1);
  }
  console.log(`3 个文档示例已使用${packageSource ? "本次打包产物" : "公开 NuGet"} ${version} 编译；未运行 Windows UI 或连接业务服务。`);
}
