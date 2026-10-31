// Bounded, dependency-free structural checks for the RimWorld 1.7 migration.
// No game binaries, external commands, network or runtime code execution.
// Unlike a successful runtime build, a static PASS never means release-ready.
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const modeIndex = process.argv.indexOf("--check");
const mode = modeIndex < 0 ? "all" : process.argv[modeIndex + 1];
if (!["all", "target", "repository", "consumer"].includes(mode)) {
  console.error("usage: node DevTools/validate-rimworld17.mjs --check all|target|repository|consumer");
  process.exit(2);
}
let errors = 0;
let passes = 0;
function assert(name, condition) {
  if (!condition) {
    console.error("FAIL " + name);
    errors++;
  } else {
    console.log("PASS " + name);
    passes++;
  }
}
function read(path) {
  const full = join(root, path);
  if (!existsSync(full)) throw new Error("Required file missing: " + path);
  return readFileSync(full, "utf8");
}
function findElements(xml, tag) {
  const m = xml.match(new RegExp("<" + tag + "\\b[^>]*>([\\s\\S]*?)</" + tag + ">", "i"));
  return m ? m[1] : "";
}
function values(xml, tag) {
  const result = [];
  const regex = new RegExp("<" + tag + "\\b[^>]*>([\\s\\S]*?)</" + tag + ">", "gi");
  let match;
  while ((match = regex.exec(xml)) !== null) result.push(match[1].trim());
  return result;
}
function only17(path) {
  const s = read(path);
  assert(path + ": supportedVersions=1.7", JSON.stringify(values(findElements(s, "supportedVersions"), "li")) === JSON.stringify(["1.7"]));
}
function folder17(path) {
  const s = read(path);
  const valid = /<loadFolders>\s*<v1\.7>\s*<li>1\.7<\/li>\s*<\/v1\.7>\s*<\/loadFolders>/.test(s);
  assert(path + ": single 1.7 load folder", valid && !/<v1\.6>/i.test(s));
}
function absent(path) {
  assert(path + ": retired 1.6 path absent", !existsSync(join(root, path)));
}
function project(path, external) {
  const s = read(path);
  assert(path + ": net10.0", /<TargetFramework>net10\.0<\/TargetFramework>/.test(s));
  assert(path + ": no obsolete Unity reference", !/<Reference\s+Include="UnityEngine\./i.test(s));
  assert(path + ": no 1.6 assembly", !/1\.6[\\/]Assemblies/.test(s));
  if (external) {
    for (const name of ["Assembly-CSharp", "UnityStubs", "Waylith"]) {
      assert(path + ": references " + name, s.includes('<Reference Include="' + name + '">'));
    }
    assert(path + ": no dependency copy", /<GenerateDependencyFile>false<\/GenerateDependencyFile>/.test(s));
  }
}
function filesUnder(path) {
  const dir = join(root, path);
  return existsSync(dir) ? readdirSync(dir, { withFileTypes: true }).filter((x) => x.isFile()).map((x) => x.name) : [];
}
function target() {
  only17("About/About.xml");
  folder17("LoadFolders.xml");
  const about = read("About/About.xml");
  assert("framework package ID preserved", values(about, "packageId").includes("lan.knowledgeframework"));
  assert("framework version matches VERSION", values(about, "modVersion")[0] === read("VERSION").trim());
  absent("1.6");
  absent("Examples/ReferenceConsumer/1.6");
  for (const path of [
    "1.7/Defs/KnowledgeFramework_Defaults.xml",
    "1.7/Defs/MainButtonDefs.xml",
    "1.7/Languages/English/Keyed/KnowledgeFramework.xml",
    "1.7/Languages/English/DefInjected/MainButtonDef/KnowledgeFramework.xml",
    "Examples/ReferenceConsumer/1.7/Defs/ReferenceConsumer_Knowledge.xml",
    "Examples/ReferenceConsumer/1.7/Languages/English/Keyed/ReferenceConsumer.xml"
  ]) assert("1.7 content exists: " + path, existsSync(join(root, path)));
  for (const path of [
    "Source/KnowledgeFramework.csproj",
    "DevTools/BehavioralHarness/KnowledgeFramework.BehavioralHarness.csproj",
    "DevTools/BridgeAdapter/KnowledgeFramework.BridgeAdapter.csproj",
    "Examples/ReferenceConsumer/ReferenceConsumer.csproj"
  ]) project(path, !path.includes("BehavioralHarness"));
  const stack = JSON.parse(read(".rimdev/stack.json"));
  assert("RimTest deployment targets 1.7", stack.deploymentTarget === "1.7/Assemblies/KnowledgeFramework.dll");
  assert("RimTest package excludes old runtime", JSON.stringify(stack.runtimePackage.include).includes("1.7/**") && !JSON.stringify(stack.runtimePackage.include).includes("1.*/**"));
  const builder = read("DevTools/Build-KnowledgeFramework.ps1");
  for (const marker of ["UnityStubs.dll", "Waylith.dll", "Harmony 3", "RimWorldTargetVersion -ne '1.7'"])
    assert("build guard: " + marker, builder.includes(marker));
  const manifest = read("DevTools/New-KnowledgeFrameworkBuildManifest.ps1");
  for (const marker of ["UnityStubs.dll", "Waylith.dll", "assemblyCSharp", "harmony", "1.7"])
    assert("manifest input: " + marker, manifest.includes(marker));
  const release = filesUnder("1.7/Assemblies");
  const binaries = release.filter((x) => x.endsWith(".dll") || x.endsWith(".json"));
  assert("1.7 release output has no unexpected files", binaries.every((x) => ["KnowledgeFramework.dll", "KnowledgeFramework.build.json"].includes(x)));
  assert("release DLL and manifest either both exist or both absent", binaries.includes("KnowledgeFramework.dll") === binaries.includes("KnowledgeFramework.build.json"));
  const adapters = filesUnder("DevTools/BridgeAdapters").filter((x) => x.endsWith(".dll") || x.endsWith(".manifest.json"));
  assert("bridge DLL and manifest count match", adapters.filter((x) => x.endsWith(".dll")).length === adapters.filter((x) => x.endsWith(".manifest.json")).length && adapters.filter((x) => x.endsWith(".dll")).length <= 1);
}
function consumer() {
  only17("Examples/ReferenceConsumer/About/About.xml");
  folder17("Examples/ReferenceConsumer/LoadFolders.xml");
  const about = read("Examples/ReferenceConsumer/About/About.xml");
  assert("reference consumer depends on stable package", values(about, "packageId").includes("lan.knowledgeframework"));
  const def = read("Examples/ReferenceConsumer/1.7/Defs/ReferenceConsumer_Knowledge.xml");
  for (const key of ["reference.field-guide", "observation", "habitat", "temperature", "fieldcraft", "reference.region"]) 
    assert("reference consumer stable Def " + key, def.includes(key));
  const source = read("Examples/ReferenceConsumer/ReferenceConsumerMod.cs");
  for (const api of ["KnowledgeEngine.Submit", "KnowledgeConsumerApi.RegisterDomain", "KnowledgeMigrationService.Import", "KnowledgeConsumerApi.Readiness"])
    assert("reference consumer uses API " + api, source.includes(api));
  assert("reference consumer does not build framework schema", !source.includes("KnowledgeRegistry.BuildDefSchemas"));
  const fixture = read("Examples/ReferenceConsumer/Verify-ReferenceConsumer.ps1");
  assert("fixture moved to 1.7 paths", fixture.includes("1.7/Defs/") && !fixture.includes("1.6/Defs/"));
}
function repository() {
  target();
  consumer();
  const pn = JSON.parse(read(".projectnexus/project.json"));
  assert("ProjectNexus project identity", pn.projectId === "knowledgeframework");
  for (const check of pn.capabilities.checks) {
    assert("bounded ProjectNexus direct runner " + check.capabilityId, check.command.executable === "node" &&
      Array.isArray(check.command.args) && check.command.args[0] === "DevTools/validate-rimworld17.mjs");
  }
  const ci = read(".github/workflows/ci.yml");
  assert("CI validates 1.7 static contract", ci.includes("DevTools/Test-RimWorld17Contract.ps1"));
}
try {
  if (mode === "all" || mode === "repository") repository();
  else if (mode === "target") target();
  else consumer();
} catch (error) {
  console.error("FAIL validator exception: " + String(error && error.message ? error.message : error));
  errors++;
}
console.log("KF17STATIC:v1|mode=" + mode + "|pass=" + passes + "|fail=" + errors +
  "|runtime=NOT_RUN|result=" + (errors === 0 ? "pass" : "fail"));
process.exitCode = errors === 0 ? 0 : 1;
