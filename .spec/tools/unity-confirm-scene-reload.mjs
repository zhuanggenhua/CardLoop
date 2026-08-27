#!/usr/bin/env node
/**
 * Confirm Unity's "opened scene changed on disk" dialog for this project.
 *
 * This is intentionally narrow: it only activates a visible Unity editor whose
 * title contains the expected project and scene, then sends Enter. Use it only
 * after the reload dialog is known to be present. It does not edit files, close
 * Unity, or start new editor automation.
 */
import { spawnSync } from "node:child_process";
import path from "node:path";

const args = process.argv.slice(2);

function usage() {
  return [
    "Usage:",
    "  node .spec/tools/unity-confirm-scene-reload.mjs --project CardLoop --scene FoundationTest --confirm-reload-dialog",
    "",
    "Rules:",
    "  - Windows only.",
    "  - Requires --confirm-reload-dialog.",
    "  - Targets one visible Unity window whose title contains the project and scene.",
    "  - Sends Enter once; use only when Unity is showing the scene reload confirmation dialog.",
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

function fail(message) {
  console.error(message);
  process.exit(1);
}

function escapePowerShellSingleQuoted(value) {
  return String(value).replaceAll("'", "''");
}

if (args.includes("--help") || args.includes("-h")) {
  console.log(usage());
  process.exit(0);
}

if (process.platform !== "win32") {
  fail("unity-confirm-scene-reload 当前只支持 Windows，因为 Unity 弹窗确认依赖 WScript.Shell。");
}

if (!hasFlag("--confirm-reload-dialog")) {
  fail("拒绝执行：必须显式传入 --confirm-reload-dialog，表示已经确认 Unity 正在显示场景重载弹窗。\n\n" + usage());
}

const project = readOption("--project", path.basename(process.cwd()));
const scene = readOption("--scene", null);
const projectLiteral = escapePowerShellSingleQuoted(project);
const sceneLiteral = scene == null ? "" : escapePowerShellSingleQuoted(scene);

const script = `
$ErrorActionPreference = 'Stop'
$project = '${projectLiteral}'
$scene = '${sceneLiteral}'

$nativeCode = @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class UnitySceneReloadDialogNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWnd, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
'@
Add-Type $nativeCode -ErrorAction SilentlyContinue

$unityWindows = Get-Process Unity -ErrorAction SilentlyContinue |
  Where-Object {
    $_.MainWindowHandle -ne 0 -and
    $_.MainWindowTitle -like "*$project*" -and
    ($scene.Length -eq 0 -or $_.MainWindowTitle -like "*$scene*")
  } |
  Select-Object Id, MainWindowTitle, Responding

if ($null -eq $unityWindows) {
  throw "未找到标题同时包含项目 '$project' 和场景 '$scene' 的可见 Unity 主窗口。"
}

if (@($unityWindows).Count -ne 1) {
  $titles = ($unityWindows | ForEach-Object { "$($_.Id): $($_.MainWindowTitle)" }) -join " | "
  throw "命中了多个 Unity 主窗口，拒绝发送按键：$titles"
}

$target = @($unityWindows)[0]
if (-not $target.Responding) {
  throw "Unity 主窗口无响应，拒绝发送按键：PID $($target.Id)"
}

$dialogs = New-Object System.Collections.Generic.List[object]
[UnitySceneReloadDialogNative]::EnumWindows({
  param($hWnd, $lParam)
  [uint32]$windowProcessId = 0
  [UnitySceneReloadDialogNative]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId) | Out-Null
  if ($windowProcessId -eq [uint32]$target.Id -and [UnitySceneReloadDialogNative]::IsWindowVisible($hWnd)) {
    $titleBuffer = New-Object System.Text.StringBuilder 512
    [UnitySceneReloadDialogNative]::GetWindowText($hWnd, $titleBuffer, $titleBuffer.Capacity) | Out-Null
    $classBuffer = New-Object System.Text.StringBuilder 256
    [UnitySceneReloadDialogNative]::GetClassName($hWnd, $classBuffer, $classBuffer.Capacity) | Out-Null
    $title = $titleBuffer.ToString()
    $className = $classBuffer.ToString()
    $isReloadDialog =
      $className -eq '#32770' -and (
        $title -like '*打开场景已在外部被修改*' -or
        $title -like '*scene*changed*disk*' -or
        $title -like '*Scene*changed*disk*'
      )
    if ($isReloadDialog) {
      $dialogs.Add([PSCustomObject]@{
        Handle = $hWnd
        Title = $title
        ClassName = $className
      }) | Out-Null
    }
  }
  return $true
}, [IntPtr]::Zero) | Out-Null

if ($dialogs.Count -ne 1) {
  $dialogTitles = ($dialogs | ForEach-Object { "$($_.ClassName):$($_.Title)" }) -join " | "
  throw "未唯一命中 Unity 场景重载弹窗，拒绝发送按键：$dialogTitles"
}

$dialog = $dialogs[0]
$reloadButtons = New-Object System.Collections.Generic.List[object]
[UnitySceneReloadDialogNative]::EnumChildWindows($dialog.Handle, {
  param($hWnd, $lParam)
  $titleBuffer = New-Object System.Text.StringBuilder 512
  [UnitySceneReloadDialogNative]::GetWindowText($hWnd, $titleBuffer, $titleBuffer.Capacity) | Out-Null
  $classBuffer = New-Object System.Text.StringBuilder 256
  [UnitySceneReloadDialogNative]::GetClassName($hWnd, $classBuffer, $classBuffer.Capacity) | Out-Null
  $title = $titleBuffer.ToString()
  $className = $classBuffer.ToString()
  if ($className -eq 'Button' -and (
      $title -like '*重新加载*' -or
      $title -like '*Reload*'
    )) {
    $reloadButtons.Add([PSCustomObject]@{
      Handle = $hWnd
      Title = $title
    }) | Out-Null
  }
  return $true
}, [IntPtr]::Zero) | Out-Null

if ($reloadButtons.Count -ne 1) {
  $buttonTitles = ($reloadButtons | ForEach-Object { "$($_.Title)" }) -join " | "
  throw "未唯一命中 Unity 场景重载按钮，拒绝发送点击：$buttonTitles"
}

$reloadButton = $reloadButtons[0]
$shell = New-Object -ComObject WScript.Shell
[UnitySceneReloadDialogNative]::ShowWindow($dialog.Handle, 9) | Out-Null
[UnitySceneReloadDialogNative]::SetForegroundWindow($dialog.Handle) | Out-Null
Start-Sleep -Milliseconds 300

$BM_CLICK = 0x00F5
[UnitySceneReloadDialogNative]::SendMessage($reloadButton.Handle, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 800

[PSCustomObject]@{
  ok = $true
  pid = $target.Id
  title = $target.MainWindowTitle
  dialogTitle = $dialog.Title
  buttonTitle = $reloadButton.Title
  sent = 'BM_CLICK'
} | ConvertTo-Json -Compress
`;

const result = spawnSync(
  "powershell",
  ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
  { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
);

if (result.status !== 0) {
  const stderr = String(result.stderr || "").trim();
  const stdout = String(result.stdout || "").trim();
  fail(stderr || stdout || `unity-confirm-scene-reload failed with exit code ${result.status}`);
}

const output = String(result.stdout || "").trim();
console.log(output || JSON.stringify({ ok: true, sent: "ENTER" }));
