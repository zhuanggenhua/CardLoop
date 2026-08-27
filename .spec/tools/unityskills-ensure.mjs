#!/usr/bin/env node
/**
 * UnitySkills REST service ensure tool for CardLoop.
 *
 * It never closes Unity and never starts a second editor for this project.
 * When Unity is already open, it only waits for the plugin-side auto-start.
 * When Unity is closed, it cold-starts only if Library/UnitySkills/cli_config.json
 * explicitly enables the Unity CLI binding for this project.
 */
import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { spawn, spawnSync } from "node:child_process";

const root = process.argv[2]?.startsWith("--") || !process.argv[2]
  ? process.cwd()
  : path.resolve(process.argv[2]);
const args = process.argv.slice(process.argv[2]?.startsWith("--") || !process.argv[2] ? 2 : 3);

function usage() {
  return [
    "Usage:",
    "  node .spec/tools/unityskills-ensure.mjs [projectRoot] [--timeout-ms 600000] [--poll-ms 2000] [--check-only]",
    "",
    "Behavior:",
    "  - If /health is reachable, exits OK.",
    "  - If Unity is already open for this project, waits for plugin auto-start.",
    "  - If Unity is closed, cold-starts only when Library/UnitySkills/cli_config.json enables Unity CLI coldStart.",
    "  - Never closes Unity, never starts a second editor for the same project.",
  ].join("\n");
}

function optionNumber(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  const raw = args[index + 1];
  const value = Number(raw);
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

function hasFlag(name) {
  return args.includes(name);
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function getHealth(port, timeoutMs = 700) {
  return new Promise((resolve) => {
    const request = http.get(
      { host: "127.0.0.1", port, path: "/health", timeout: timeoutMs },
      (response) => {
        let data = "";
        response.setEncoding("utf8");
        response.on("data", (chunk) => { data += chunk; });
        response.on("end", () => {
          if (response.statusCode < 200 || response.statusCode >= 300) {
            resolve(null);
            return;
          }
          try {
            resolve({ ...JSON.parse(data), port });
          } catch {
            resolve(null);
          }
        });
      },
    );
    request.on("timeout", () => {
      request.destroy();
      resolve(null);
    });
    request.on("error", () => resolve(null));
  });
}

async function findHealth() {
  for (let port = 8090; port <= 8100; port += 1) {
    const health = await getHealth(port);
    if (health) return health;
  }
  return null;
}

function runUnityVerifyStatus() {
  const guard = path.join(root, ".spec", "tools", "unity-verify.mjs");
  if (!fs.existsSync(guard)) {
    throw new Error(`缺少 Unity 验证 guard：${guard}`);
  }

  const result = spawnSync(process.execPath, [guard, "status"], {
    cwd: root,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
  if (result.status !== 0) {
    throw new Error(`unity-verify status 失败：${(result.stderr || result.stdout || "").trim()}`);
  }
  return JSON.parse(result.stdout);
}

function hasLiveProjectUnity(state) {
  return Array.isArray(state.projectProcesses) && state.projectProcesses.length > 0;
}

function readCliConfig() {
  const configPath = path.join(root, "Library", "UnitySkills", "cli_config.json");
  if (!fs.existsSync(configPath)) return { configPath, config: null };
  try {
    return { configPath, config: JSON.parse(fs.readFileSync(configPath, "utf8")) };
  } catch (error) {
    throw new Error(`UnitySkills CLI 配置无法读取：${configPath}；${error.message}`);
  }
}

function coldStartEnabled(config) {
  if (!config || config.enabled !== true) return false;
  if (config.features && config.features.coldStart === false) return false;
  return true;
}

function commandPathExists(commandPath) {
  if (!commandPath) return false;
  if (fs.existsSync(commandPath)) return true;
  if (path.basename(commandPath) !== commandPath) return false;

  const lookup = spawnSync(process.platform === "win32" ? "where.exe" : "which", [commandPath], {
    cwd: root,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
  return lookup.status === 0;
}

function isUnityEditorExecutable(commandPath) {
  return path.normalize(commandPath).replaceAll("\\", "/").toLowerCase().endsWith("/editor/unity.exe");
}

function coldStartArguments(config) {
  if (isUnityEditorExecutable(config.cliPath)) {
    return ["-projectPath", root, "-unityskills-coldstart"];
  }

  return ["open", root, "--args", "-unityskills-coldstart"];
}

function coldStartUnity(configPath, config) {
  if (!coldStartEnabled(config)) {
    throw new Error(`Unity CLI 冷启动未启用：${configPath}`);
  }
  if (!commandPathExists(config.cliPath)) {
    throw new Error(`Unity CLI / Editor 路径无效：${config.cliPath || "<empty>"}`);
  }

  const launchArgs = coldStartArguments(config);
  if (isUnityEditorExecutable(config.cliPath)) {
    const child = spawn(config.cliPath, launchArgs, {
      cwd: root,
      detached: true,
      stdio: "ignore",
    });
    child.unref();
    return;
  }

  const result = spawnSync(
    config.cliPath,
    launchArgs,
    { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 120000 },
  );
  if (result.error) {
    throw new Error(`Unity CLI / Editor 冷启动失败：${result.error.message}`);
  }
  if (result.status !== 0) {
    throw new Error(`Unity CLI / Editor 冷启动返回非 0：${result.status}\n${(result.stderr || result.stdout || "").trim()}`);
  }
}

function printHealth(health) {
  console.log(JSON.stringify({
    ok: true,
    port: health.port,
    currentMode: health.currentMode,
    mainThreadIdleMs: health.mainThreadIdleMs,
    queuedRequests: health.queuedRequests ?? health.pendingRequests ?? health.queueLength ?? 0,
    isCompiling: Boolean(health.isCompiling),
    isUpdating: Boolean(health.isUpdating),
  }, null, 2));
}

async function waitForHealth(timeoutMs, pollMs) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    const health = await findHealth();
    if (health) return health;
    await sleep(pollMs);
  }
  return null;
}

async function main() {
  if (hasFlag("--help")) {
    console.log(usage());
    return;
  }

  const timeoutMs = optionNumber("--timeout-ms", 600000);
  const pollMs = optionNumber("--poll-ms", 2000);
  const firstHealth = await findHealth();
  if (firstHealth) {
    printHealth(firstHealth);
    return;
  }

  const state = runUnityVerifyStatus();
  if (hasLiveProjectUnity(state)) {
    if (hasFlag("--check-only")) {
      throw new Error("Unity 已打开但 UnitySkills /health 当前不可达；等待插件自启或查看 Unity Console。");
    }
    console.log("WAIT: Unity 已打开，等待 UnitySkills 插件自启；不会启动第二个编辑器。");
    const health = await waitForHealth(timeoutMs, pollMs);
    if (!health) {
      throw new Error("等待超时：Unity 仍在运行，但 UnitySkills /health 没有恢复。请查看 Unity Console 或 Library/UnitySkills 日志。");
    }
    printHealth(health);
    return;
  }

  if (hasFlag("--check-only")) {
    throw new Error("Unity 未打开且 UnitySkills /health 不可达；check-only 不会冷启动。");
  }

  const { configPath, config } = readCliConfig();
  coldStartUnity(configPath, config);
  const health = await waitForHealth(timeoutMs, pollMs);
  if (!health) {
    throw new Error("Unity CLI 已发起冷启动，但 UnitySkills /health 在超时前没有恢复。");
  }
  printHealth(health);
}

main().catch((error) => {
  console.error(`BLOCKED: ${error.message}`);
  process.exit(2);
});
