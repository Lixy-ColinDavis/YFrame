param(
    [string]$SourceBinDir,
    [string]$OutputDir,
    [switch]$DebugMode
)

$ErrorActionPreference = "Stop"

$coreSrc = $SourceBinDir
$coreDst = Join-Path $OutputDir "payload\core"
$pluginDst = Join-Path $OutputDir "payload\plugins"

Write-Host "Source: $coreSrc"
Write-Host "Output: $OutputDir"

# Clean old output
if (Test-Path $coreDst) {
    Remove-Item $coreDst -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path $pluginDst) {
    Remove-Item $pluginDst -Recurse -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Milliseconds 200

New-Item -ItemType Directory -Force -Path $coreDst | Out-Null

# Collect YFrame core files only
Write-Host "Collecting YFrame core framework files..."
if (Test-Path $coreSrc) {
    $files = Get-ChildItem $coreSrc -File
    foreach ($f in $files) {
        # 跳过 .pdb 调试符号文件
        if ($f.Extension -eq '.pdb') { continue }
        Copy-Item $f.FullName -Destination $coreDst -Force
    }
    $runtimesSrc = Join-Path $coreSrc "runtimes"
    if (Test-Path $runtimesSrc) {
        $runtimesDst = Join-Path $coreDst "runtimes"
        Copy-Item $runtimesSrc -Destination $runtimesDst -Recurse -Force
    }
}
else {
    Write-Host "  WARNING: Core dir not found: $coreSrc"
}

Write-Host "Payload collection complete (core framework only)."
