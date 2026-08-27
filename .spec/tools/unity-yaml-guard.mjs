#!/usr/bin/env node
/**
 * Unity serialized-file guard.
 *
 * This is a static gate used before opening or automating Unity. It catches the
 * class of damage that previously broke .meta YAML files and forced Unity into
 * an unsafe import/compile state.
 */
import fs from "node:fs";
import path from "node:path";

const root = process.argv[2] ? path.resolve(process.argv[2]) : process.cwd();
const errors = [];
const warnings = [];

const scanRoots = [
  "Assets",
  "ProjectSettings",
].map((item) => path.join(root, item)).filter((item) => fs.existsSync(item));

const ignoredPathParts = new Set([
  "Library",
  "Logs",
  "Temp",
  "UserSettings",
  "obj",
]);

const yamlRequiredExtensions = new Set([
  ".unity",
  ".prefab",
]);

const yamlIfTextExtensions = new Set([
  ".asset",
  ".mat",
  ".anim",
  ".controller",
  ".overridecontroller",
  ".playable",
  ".mixer",
  ".rendertexture",
  ".physicmaterial",
  ".physicsmaterial2d",
  ".mask",
  ".guiskin",
]);

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function fail(message) {
  errors.push(message);
}

function warn(message) {
  warnings.push(message);
}

function walk(directory, out = []) {
  const parts = path.relative(root, directory).split(path.sep).filter(Boolean);
  if (parts.some((part) => ignoredPathParts.has(part))) return out;

  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(fullPath, out);
    else out.push(fullPath);
  }

  return out;
}

function readUtf8IfText(file) {
  const buffer = fs.readFileSync(file);
  if (buffer.includes(0)) return null;
  return buffer.toString("utf8");
}

function validateMeta(file) {
  const relative = rel(file);
  const bytes = fs.readFileSync(file);
  if (bytes.length === 0) {
    fail(`${relative} 是空文件；Unity 不能导入空 .meta。`);
    return;
  }

  const text = bytes.toString("utf8");
  const lines = text.split(/\r?\n/).map((line) => line.trim());
  if (!lines.some((line) => line === "fileFormatVersion: 2")) {
    fail(`${relative} 缺少 fileFormatVersion: 2。`);
  }
  const guidMatches = lines
    .map((line) => /^guid:\s*([0-9a-f]{32})$/.exec(line))
    .filter(Boolean);
  if (guidMatches.length !== 1) {
    fail(`${relative} 必须且只能有一个 32 位十六进制 GUID；当前数量 ${guidMatches.length}。`);
  }
  if (/\$\d/.test(text)) {
    fail(`${relative} 含有未展开的 PowerShell/正则替换占位符；禁止把 $1 这类内容写进 Unity 序列化文件。`);
  }
}

function validateYamlHeader(file, required) {
  const relative = rel(file);
  const text = readUtf8IfText(file);
  if (text == null) return;

  const trimmedStart = text.slice(0, 256);
  const looksLikeYaml = trimmedStart.startsWith("%YAML 1.1") || trimmedStart.includes("--- !u!");
  if (!required && !looksLikeYaml) return;

  if (!/^%YAML 1\.1\r?\n%TAG !u! tag:unity3d\.com,2011:\r?\n/.test(text)) {
    fail(`${relative} 缺少 Unity YAML 标准文件头。`);
    return;
  }

  const objectCount = (text.match(/^--- !u!\d+/gm) || []).length;
  if (objectCount <= 0) {
    fail(`${relative} 没有任何 Unity 对象块。`);
  }
  if (/\$\d/.test(text)) {
    fail(`${relative} 含有未展开的 PowerShell/正则替换占位符。`);
  }
}

const files = scanRoots.flatMap((directory) => walk(directory));
for (const file of files) {
  const extension = path.extname(file).toLowerCase();
  if (extension === ".meta") {
    validateMeta(file);
    continue;
  }
  if (yamlRequiredExtensions.has(extension)) {
    validateYamlHeader(file, true);
    continue;
  }
  if (yamlIfTextExtensions.has(extension)) {
    validateYamlHeader(file, false);
  }
}

if (files.length === 0) {
  warn("未扫描到 Unity 序列化文件；请确认当前工作目录是 Unity 项目根。");
}

for (const warning of warnings) {
  console.warn(`unity-yaml-guard warning: ${warning}`);
}

if (errors.length > 0) {
  console.error("unity-yaml-guard failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`unity-yaml-guard passed (${files.length} files scanned)`);
