#!/usr/bin/env node
/**
 * Unity verification guard for CardLoop.
 *
 * This tool does not kill processes or close Unity. It blocks unsafe
 * verification paths before they can trigger editor crashes again. The only
 * cleanup it performs is deleting this project's stale Temp/UnityLockfile after
 * proving no Unity/import/shader process is working on the project.
 */
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync, spawnSync } from "node:child_process";

const root = path.resolve(process.cwd());
const args = process.argv.slice(2);
const command = args[0] || "help";

function usage() {
  return [
    "Usage:",
    "  node .spec/tools/unity-verify.mjs status",
    "  node .spec/tools/unity-verify.mjs preflight --mode batch",
    "  node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills",
    "  node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool puerts-mcp --purpose <runtime-player|screenshot|profiler|js-eval>",
    "  node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool aibridge --fallback",
    "  node .spec/tools/unity-verify.mjs clean-stale-lockfile --confirm-stale-lockfile",
    "  node .spec/tools/unity-verify.mjs batch-test --unity <Unity.exe> --testPlatform <EditMode|PlayMode> --testResults <file> --logFile <file> [--testFilter <name>] [--execute]",
    "",
    "Rules:",
    "  - UnitySkills is the default editor automation tool.",
    "  - AIBridge is fallback-only.",
    "  - Puerts MCP is specialized-only; generic editor automation is blocked.",
    "  - Never closes Unity through Puerts/MCP/EditorApplication.Exit.",
    "  - Refuses -runTests combined with -quit.",
    "  - Refuses batch verification while this project has Unity/import/shader processes.",
    "  - Runs unity-yaml-guard before Unity automation.",
    "  - Blocks UnitySkills automation when /health shows a stale main thread, compiling, or asset updating.",
    "  - Refuses project Temp as --testResults output; use Logs for durable Unity test XML.",
    "  - Refuses screenshot/visual-evidence PlayMode filters in batchmode; use editor automation or a screenshot-specialized path.",
    "  - Cleans only Temp/UnityLockfile when explicitly confirmed and no Unity process is active.",
  ].join("\n");
}

function readOption(name, fallback = null) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  const value = args[index + 1];
  if (!value || value.startsWith("--")) return fallback;
  return value;
}

function hasFlag(name) {
  return args.includes(name);
}

function normalizeForCompare(value) {
  return path.resolve(value).toLowerCase().replaceAll("\\", "/");
}

function powershellJson(script) {
  try {
    const output = execFileSync(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
      { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim();
    if (!output) return [];
    const parsed = JSON.parse(output);
    return Array.isArray(parsed) ? parsed : [parsed];
  } catch (error) {
    const output = String(error.stdout || "").trim();
    if (!output) return [];
    try {
      const parsed = JSON.parse(output);
      return Array.isArray(parsed) ? parsed : [parsed];
    } catch {
      return [{ error: String(error.message || error) }];
    }
  }
}

function getUnityProcesses() {
  const ps = [
    "Get-CimInstance Win32_Process |",
    "Where-Object { $_.Name -in @('Unity.exe','AssetImportWorker.exe','UnityShaderCompiler.exe') } |",
    "Select-Object Name,ProcessId,CommandLine,ExecutablePath |",
    "ConvertTo-Json -Compress",
  ].join(" ");
  const processes = powershellJson(ps);
  const rootKey = normalizeForCompare(root);
  const normalized = processes.map((process) => {
    const rawName = String(process.Name || process.ProcessName || process.name || "unknown");
    const name = rawName.endsWith(".exe") ? rawName : `${rawName}.exe`;
    const commandLine = String(process.CommandLine || "");
    const processPath = String(process.ExecutablePath || process.Path || "");
    const normalizedCommandLine = commandLine.toLowerCase().replaceAll("\\", "/");
    const normalizedPath = processPath.toLowerCase().replaceAll("\\", "/");
    const shaderParentPid = extractShaderCompilerParentPid(commandLine);
    const isAssetImportWorker = /assetimportworker|(\s|")-name(\s|")+(assetimport|assetimportworker)/i.test(commandLine);
    const isBatch = normalizedCommandLine.includes("-batchmode") || normalizedCommandLine.includes("-batchmode");
    const isProjectUnity = normalizedCommandLine.includes(rootKey);
    const role = name === "UnityShaderCompiler.exe"
      ? "shader-compiler"
      : isAssetImportWorker
        ? "asset-import-worker"
        : name === "Unity.exe" && isProjectUnity && !isBatch
          ? "main-editor"
          : "unity-process";
    return {
      name,
      pid: process.ProcessId || process.Id || process.pid || null,
      commandLine,
      path: processPath,
      role,
      shaderParentPid,
      projectRelated: isProjectUnity,
      commandLineKnown: commandLine.length > 0 || processPath.length > 0,
      error: process.error || null,
    };
  });

  const projectUnityPids = new Set(
    normalized
      .filter((process) => process.projectRelated && process.name === "Unity.exe")
      .map((process) => process.pid),
  );

  return normalized.map((process) => {
    if (process.role === "shader-compiler" && projectUnityPids.has(process.shaderParentPid)) {
      return { ...process, projectRelated: true };
    }
    return process;
  });
}

function extractShaderCompilerParentPid(commandLine) {
  const match = /ShaderCompilerIPC-(\d+)-/i.exec(commandLine);
  return match ? Number(match[1]) : null;
}

function getState() {
  const processes = getUnityProcesses();
  const projectProcesses = processes.filter((process) => process.projectRelated || !process.commandLineKnown);
  const lockfile = path.join(root, "Temp", "UnityLockfile");
  const crashRoot = path.join(os.homedir(), "AppData", "Local", "Temp", "Unity", "Editor", "Crashes");
  const latestCrash = latestDirectory(crashRoot);
  return {
    projectRoot: root,
    lockfile,
    lockfileExists: fs.existsSync(lockfile),
    processes,
    projectProcesses,
    latestCrash,
  };
}

function latestDirectory(directory) {
  if (!fs.existsSync(directory)) return null;
  const entries = fs.readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => {
      const fullPath = path.join(directory, entry.name);
      return { path: fullPath, mtimeMs: fs.statSync(fullPath).mtimeMs };
    })
    .sort((a, b) => b.mtimeMs - a.mtimeMs);
  return entries[0] || null;
}

function fail(message, state = null) {
  console.error(`BLOCKED: ${message}`);
  if (state) printState(state);
  process.exit(2);
}

function printState(state) {
  console.log(JSON.stringify({
    projectRoot: state.projectRoot,
    lockfileExists: state.lockfileExists,
    lockfile: state.lockfile,
    projectProcesses: state.projectProcesses.map((process) => ({
      name: process.name,
      pid: process.pid,
      role: process.role,
      projectRelated: process.projectRelated,
      commandLineKnown: process.commandLineKnown,
    })),
    unitySkillsHealth: state.unitySkillsHealth || null,
    latestCrash: state.latestCrash,
  }, null, 2));
}

function runYamlGuard() {
  const guardPath = path.join(root, ".spec", "tools", "unity-yaml-guard.mjs");
  if (!fs.existsSync(guardPath)) {
    fail(`Unity YAML guard is missing: ${guardPath}`);
  }
  const result = spawnSync(process.execPath, [guardPath, root], {
    cwd: root,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
  if (result.status !== 0) {
    const output = [result.stdout, result.stderr].filter(Boolean).join("\n").trim();
    fail(`Unity serialized-file guard failed before verification.\n${output}`);
  }
}

function getUnitySkillsHealthSync() {
  for (let port = 8090; port <= 8100; port += 1) {
    const result = getJsonFromLocalhostSync(port, "/health", 700);
    if (result.ok) return { ...result.value, port };
  }
  return null;
}

function getJsonFromLocalhostSync(port, requestPath, timeoutMs) {
  const start = Date.now();
  const child = spawnSync(process.execPath, ["-e", `
const http = require("http");
const req = http.get({ host: "127.0.0.1", port: ${port}, path: "${requestPath}", timeout: ${timeoutMs} }, (res) => {
  let data = "";
  res.setEncoding("utf8");
  res.on("data", (chunk) => data += chunk);
  res.on("end", () => {
    if (res.statusCode < 200 || res.statusCode >= 300) process.exit(2);
    process.stdout.write(data);
  });
});
req.on("timeout", () => { req.destroy(); process.exit(3); });
req.on("error", () => process.exit(4));
`], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], timeout: timeoutMs + 300 });

  if (child.status !== 0 || !child.stdout) return { ok: false, elapsedMs: Date.now() - start };
  try {
    return { ok: true, value: JSON.parse(child.stdout), elapsedMs: Date.now() - start };
  } catch {
    return { ok: false, elapsedMs: Date.now() - start };
  }
}

function assertUnitySkillsHealth() {
  const health = getUnitySkillsHealthSync();
  if (health == null) {
    fail("UnitySkills REST /health is not reachable on ports 8090-8100. Editor automation cannot start safely.");
  }

  const mainThreadIdleMs = Number(health.mainThreadIdleMs ?? -1);
  const queuedRequests = Number(health.queuedRequests ?? health.pendingRequests ?? health.queueLength ?? 0);
  const isCompiling = Boolean(health.isCompiling);
  const isUpdating = Boolean(health.isUpdating);

  if (mainThreadIdleMs > 120000 && !isCompiling && !isUpdating) {
    fail([
      "UnitySkills server is alive, but Unity's editor main thread has not processed the request loop for over 120 seconds.",
      "This is the repeated stuck-editor condition. Do not call main-thread skills, do not send CloseMainWindow/WM_CLOSE/Alt+F4/taskkill, and do not start another verification path.",
      `health=${JSON.stringify({ port: health.port, mainThreadIdleMs, queuedRequests, isCompiling, isUpdating })}`,
    ].join(" "));
  }

  if (mainThreadIdleMs > 30000 && (isCompiling || isUpdating)) {
    fail([
      "Unity is still compiling or updating assets. Editor automation must wait instead of adding requests.",
      `health=${JSON.stringify({ port: health.port, mainThreadIdleMs, queuedRequests, isCompiling, isUpdating })}`,
    ].join(" "));
  }

  return health;
}

function assertNoUnsafeUnityArgs(unityArgs) {
  const joined = unityArgs.join(" ");
  if (joined.includes("EditorApplication.Exit")) {
    fail("unsafe EditorApplication.Exit path is forbidden for Unity verification.");
  }
  if (unityArgs.includes("-runTests") && unityArgs.includes("-quit")) {
    fail("Unity Test Runner must not combine -runTests with -quit in this project.");
  }
}

function assertToolPolicyForEditorAutomation() {
  const tool = readOption("--tool", "unityskills");
  const normalizedTool = tool.toLowerCase();
  if (normalizedTool === "unityskills") return { tool: normalizedTool };

  if (normalizedTool === "aibridge") {
    if (!hasFlag("--fallback")) {
      fail("AIBridge is fallback-only. Pass --fallback after documenting why UnitySkills is not suitable.");
    }
    return { tool: normalizedTool, fallback: true };
  }

  if (normalizedTool === "puerts-mcp" || normalizedTool === "puerts") {
    const purpose = readOption("--purpose");
    const allowedPurposes = new Set(["runtime-player", "screenshot", "profiler", "js-eval"]);
    if (!allowedPurposes.has(purpose)) {
      fail("Puerts MCP is specialized-only. Allowed --purpose values: runtime-player, screenshot, profiler, js-eval.");
    }
    return { tool: "puerts-mcp", purpose };
  }

  fail(`unknown Unity automation tool: ${tool}`);
}

function assertPreflight(mode) {
  runYamlGuard();
  const state = getState();
  const unityEditors = state.projectProcesses.filter((process) => process.role === "main-editor");
  const busyWorkers = state.projectProcesses.filter((process) =>
    process.role === "asset-import-worker" || process.role === "shader-compiler");

  if (mode === "batch") {
    if (state.projectProcesses.length > 0) {
      fail("batch verification requires no Unity/import/shader process for this project.", state);
    }
    if (state.lockfileExists) {
      fail("Temp/UnityLockfile exists. Do not delete it automatically; classify whether it is stale first.", state);
    }
    return state;
  }

  if (mode === "editor-automation") {
    const toolPolicy = assertToolPolicyForEditorAutomation();
    if (unityEditors.length !== 1) {
      fail("editor automation requires exactly one already-open Unity editor for this project.", state);
    }
    if (toolPolicy.tool === "unityskills") {
      const health = assertUnitySkillsHealth();
      state.unitySkillsHealth = {
        port: health.port,
        mainThreadIdleMs: health.mainThreadIdleMs,
        queuedRequests: health.queuedRequests ?? health.pendingRequests ?? health.queueLength ?? 0,
        isCompiling: Boolean(health.isCompiling),
        isUpdating: Boolean(health.isUpdating),
      };
      if ((state.unitySkillsHealth.isCompiling || state.unitySkillsHealth.isUpdating) && busyWorkers.length > 0) {
        fail("editor automation is blocked because UnitySkills /health reports compiling or asset updating.", state);
      }
    } else if (busyWorkers.length > 0) {
      fail("editor automation is blocked while Unity asset import or shader compiler worker state cannot be verified through UnitySkills /health.", state);
    }
    return state;
  }

  fail(`unknown preflight mode: ${mode}`);
}

function cleanStaleLockfile() {
  if (!hasFlag("--confirm-stale-lockfile")) {
    fail("clean-stale-lockfile requires --confirm-stale-lockfile.");
  }

  const state = getState();
  if (state.projectProcesses.length > 0) {
    fail("cannot clean Temp/UnityLockfile while Unity/import/shader process exists for this project.", state);
  }

  if (!state.lockfileExists) {
    console.log("OK: Temp/UnityLockfile does not exist.");
    printState(state);
    return;
  }

  const normalizedLockfile = normalizeForCompare(state.lockfile);
  const expectedLockfile = normalizeForCompare(path.join(root, "Temp", "UnityLockfile"));
  if (normalizedLockfile !== expectedLockfile) {
    fail(`refusing to clean unexpected lockfile path: ${state.lockfile}`, state);
  }

  fs.rmSync(state.lockfile, { force: true });
  state.lockfileExists = false;
  console.log(`OK: removed stale lockfile: ${state.lockfile}`);
  printState(state);
}

function buildBatchTestCommand() {
  const unity = readOption("--unity");
  const testPlatform = readOption("--testPlatform");
  const testResults = readOption("--testResults");
  const logFile = readOption("--logFile");
  const testFilter = readOption("--testFilter");
  if (!unity || !testPlatform || !testResults || !logFile) {
    console.error(usage());
    process.exit(1);
  }
  if (!["EditMode", "PlayMode"].includes(testPlatform)) {
    fail("--testPlatform must be EditMode or PlayMode.");
  }
  const testResultsPath = path.resolve(testResults);
  assertDurableTestResultsPath(testResultsPath);
  assertBatchTestFilterPolicy(testPlatform, testFilter);
  const unityArgs = [
    "-batchmode",
    "-projectPath", root,
    "-runTests",
    "-testPlatform", testPlatform,
    "-testResults", testResultsPath,
    "-logFile", path.resolve(logFile),
  ];
  if (testFilter) unityArgs.push("-testFilter", testFilter);
  assertNoUnsafeUnityArgs(unityArgs);
  return { unity, unityArgs, testResultsPath };
}

function assertBatchTestFilterPolicy(testPlatform, testFilter) {
  if (testPlatform !== "PlayMode" || !testFilter) return;

  if (!/(screenshot|visualevidence|capturevisual|capturescreenshot)/i.test(testFilter)) return;

  fail([
    "Screenshot or visual-evidence PlayMode tests must not run through Unity batchmode in this project.",
    `Blocked testFilter=${testFilter}`,
    "Use the editor-automation guard with UnitySkills, or the screenshot-specialized puerts path, after confirming the editor is healthy.",
  ].join(" "));
}

function assertDurableTestResultsPath(testResultsPath) {
  const projectTemp = path.resolve(root, "Temp");
  const relative = path.relative(projectTemp, testResultsPath);
  const isInsideProjectTemp = relative === "" || (!relative.startsWith("..") && !path.isAbsolute(relative));
  if (!isInsideProjectTemp) return;

  fail([
    "Unity test results XML must not be written under this project's Temp directory.",
    `Unsafe testResults=${testResultsPath}`,
    "Use a durable project path such as Logs\\<name>.xml so missing or empty XML can be treated as a verification-infrastructure failure.",
  ].join(" "));
}

function assertBatchTestReport(testResultsPath, status) {
  if (status !== 0) return;
  if (!fs.existsSync(testResultsPath)) {
    fail([
      "Unity batch test exited with code 0, but did not write the requested test results XML.",
      `Missing testResults=${testResultsPath}`,
      "Treat this as a verification-infrastructure failure, not as proof that tests passed.",
    ].join(" "));
  }

  const stats = fs.statSync(testResultsPath);
  if (stats.size <= 0) {
    fail([
      "Unity batch test wrote an empty test results XML.",
      `Empty testResults=${testResultsPath}`,
      "Treat this as a verification-infrastructure failure, not as proof that tests passed.",
    ].join(" "));
  }
}

if (command === "help" || hasFlag("--help")) {
  console.log(usage());
  process.exit(0);
}

if (command === "status") {
  printState(getState());
  process.exit(0);
}

if (command === "preflight") {
  const mode = readOption("--mode", "batch");
  const state = assertPreflight(mode);
  const tool = mode === "editor-automation" ? readOption("--tool", "unityskills") : "batchmode";
  console.log(`OK: Unity verification preflight passed for mode=${mode}, tool=${tool}.`);
  printState(state);
  process.exit(0);
}

if (command === "clean-stale-lockfile") {
  cleanStaleLockfile();
  process.exit(0);
}

if (command === "batch-test") {
  const { unity, unityArgs, testResultsPath } = buildBatchTestCommand();
  assertPreflight("batch");
  console.log([unity, ...unityArgs.map((arg) => arg.includes(" ") ? `"${arg}"` : arg)].join(" "));
  if (!hasFlag("--execute")) {
    console.log("DRY RUN: add --execute to run Unity.");
    process.exit(0);
  }
  const result = spawnSync(unity, unityArgs, { cwd: root, stdio: "inherit" });
  const status = result.status ?? 1;
  assertBatchTestReport(testResultsPath, status);
  process.exit(status);
}

console.error(usage());
process.exit(1);
