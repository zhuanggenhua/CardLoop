#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync, spawnSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath } from "node:url";

const lintPath = fileURLToPath(new URL("./spec-lint.mjs", import.meta.url));

function run(command, args, cwd) {
  return execFileSync(command, args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function makeFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "spec-lint-"));
  run("git", ["init", "-q"], root);
  run("git", ["config", "core.symlinks", "false"], root);

  write(path.join(root, "AGENTS.md"), [
    "# Test",
    ".spec/AGENTS.md",
    ".spec/rules/system.md",
    ".spec/knowledge/README.md",
    "",
  ].join("\n"));
  write(path.join(root, "CLAUDE.md"), [
    "# CLAUDE.md",
    "",
    "@.spec/AGENTS.md",
    "",
    "@.spec/knowledge/README.md",
    "",
    "@.spec/rules/system.md",
    "",
  ].join("\n"));
  write(path.join(root, ".spec/AGENTS.md"), "# Spec\n");
  write(path.join(root, ".spec/rules/system.md"), "# Rules\n");
  write(path.join(root, ".spec/decisions/README.md"), "# Decisions\n");
  write(path.join(root, ".spec/tasks/README.md"), "# Tasks\n");
  write(path.join(root, ".spec/knowledge/README.md"), [
    "---",
    "name: knowledge",
    "description: test knowledge index",
    "metadata:",
    "  type: index",
    "---",
    "# Knowledge",
    "",
    "| 文档 | 一句话 |",
    "|------|--------|",
    "| [standards/code-design.md](standards/code-design.md) | 写业务代码、拆职责、选设计模式、审查 SOLID / 反模式 / 防护性架构时查。 |",
    "| [lessons.md](lessons.md) | test |",
    "",
  ].join("\n"));
  write(path.join(root, ".spec/knowledge/standards/code-design.md"), [
    "---",
    "name: code-design",
    "description: test code design",
    "---",
    "# Code Design",
    "",
  ].join("\n"));
  write(path.join(root, ".spec/knowledge/lessons.md"), [
    "---",
    "name: lessons",
    "description: test lessons",
    "---",
    "# Lessons",
    "",
  ].join("\n"));
  write(path.join(root, ".spec/skills/example/SKILL.md"), [
    "---",
    "name: example",
    "description: test skill",
    "---",
    "# Example",
    "",
  ].join("\n"));
  write(path.join(root, ".spec/agents/reviewer.agent.md"), [
    "---",
    "name: reviewer",
    "description: test agent",
    "---",
    "# Reviewer",
    "",
  ].join("\n"));

  write(path.join(root, ".agents/skills"), "../.spec/skills");
  write(path.join(root, ".claude/skills"), "../.spec/skills");
  write(path.join(root, ".claude/agents"), "../.spec/agents");
  run("git", ["add", "AGENTS.md", ".spec"], root);

  const skillsHash = run("git", ["hash-object", "-w", ".agents/skills"], root).trim();
  const agentsHash = run("git", ["hash-object", "-w", ".claude/agents"], root).trim();
  run("git", ["update-index", "--add", "--cacheinfo", `120000,${skillsHash},.agents/skills`], root);
  run("git", ["update-index", "--add", "--cacheinfo", `120000,${skillsHash},.claude/skills`], root);
  run("git", ["update-index", "--add", "--cacheinfo", `120000,${agentsHash},.claude/agents`], root);

  return root;
}

test("accepts host adapters stored as Git symlinks", () => {
  const root = makeFixture();
  assert.equal(run(process.execPath, [lintPath, root], root).trim(), "spec-lint passed");
});

test("rejects .codex/skills as a project skill source", () => {
  const root = makeFixture();
  write(path.join(root, ".codex/skills/bad/SKILL.md"), [
    "---",
    "name: bad",
    "description: bad source",
    "---",
    "# Bad",
    "",
  ].join("\n"));
  run("git", ["add", ".codex/skills/bad/SKILL.md"], root);

  const result = spawnSync(process.execPath, [lintPath, root], { cwd: root, encoding: "utf8" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /\.codex\/skills/);
});
