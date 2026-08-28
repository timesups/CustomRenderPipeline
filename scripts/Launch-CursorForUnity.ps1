# 用 VS2022 环境启动 Cursor，避免 OmniSharp / C# LS 误选 VS2026 MSBuild
# （VS2026 MSBuild 与当前 C# 扩展存在 FrozenSet 程序集冲突，会导致 IntelliSense 失败）

$ErrorActionPreference = 'Stop'

$vs2022 = 'C:\Program Files\Microsoft Visual Studio\2022\Community'
$msbuildBin = Join-Path $vs2022 'MSBuild\Current\Bin'
if (!(Test-Path $msbuildBin)) {
    Write-Error "未找到 VS2022 MSBuild: $msbuildBin"
}

$env:VSINSTALLDIR = "$vs2022\"
$env:VisualStudioVersion = '17.0'
$env:MSBuildSDKsPath = 'C:\Program Files\dotnet\sdk\9.0.313\Sdks'
$env:DOTNET_ROOT = 'C:\Program Files\dotnet'
$env:PATH = "$msbuildBin;C:\Program Files\dotnet;$env:PATH"

# 尽量让 vswhere/MSBuildLocator 优先看到 2022
$env:VSAPPIDDIR = Join-Path $vs2022 'Common7\IDE\'

$projectRoot = Split-Path $PSScriptRoot -Parent
$cursorCandidates = @(
    "$env:LOCALAPPDATA\Programs\cursor\Cursor.exe",
    "$env:LOCALAPPDATA\Programs\Cursor\Cursor.exe",
    'C:\Program Files\Cursor\Cursor.exe'
)
$cursor = $cursorCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $cursor) {
    Write-Error '未找到 Cursor.exe，请把路径补进 scripts/Launch-CursorForUnity.ps1'
}

Write-Host "Starting Cursor with VS2022 MSBuild preference..."
Write-Host "  VSINSTALLDIR=$env:VSINSTALLDIR"
Write-Host "  Project=$projectRoot"
Start-Process -FilePath $cursor -ArgumentList @($projectRoot)
