# ===================================================================
# 自动版本管理脚本 - AutoVersion.ps1
# 功能：自动升级版本号，维护版本历史记录
# 作者：Trading Tools Team
# ===================================================================

param(
    [string]$ProjectFile = "BinanceFuturesTrader.csproj",
    [string]$VersionType = "patch",  # major, minor, patch
    [string]$UpdateMessage = "",     # 可选的更新说明
    [switch]$Interactive = $false    # 是否交互式输入更新内容
)

# 颜色输出函数
function Write-ColorText {
    param($Text, $Color = "White")
    Write-Host $Text -ForegroundColor $Color
}

function Write-Success { param($Text) Write-ColorText "✅ $Text" "Green" }
function Write-Info { param($Text) Write-ColorText "ℹ️ $Text" "Cyan" }
function Write-Warning { param($Text) Write-ColorText "⚠️ $Text" "Yellow" }
function Write-Error { param($Text) Write-ColorText "❌ $Text" "Red" }

# 检查项目文件是否存在
if (-not (Test-Path $ProjectFile)) {
    Write-Error "Project file not found: $ProjectFile"
    exit 1
}

Write-Info "Starting auto version management..."
Write-Info "Project file: $ProjectFile"

# 读取当前版本号
$projectContent = Get-Content $ProjectFile -Raw
$versionPattern = '<Version>([0-9]+)\.([0-9]+)\.?([0-9]*)</Version>'
$assemblyVersionPattern = '<AssemblyVersion>([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)</AssemblyVersion>'

$versionMatch = [regex]::Match($projectContent, $versionPattern)
$assemblyMatch = [regex]::Match($projectContent, $assemblyVersionPattern)

if (-not $versionMatch.Success) {
    Write-Error "Cannot find version number in project file"
    exit 1
}

# 解析当前版本
$currentMajor = [int]$versionMatch.Groups[1].Value
$currentMinor = [int]$versionMatch.Groups[2].Value
$currentPatch = if ($versionMatch.Groups[3].Value) { [int]$versionMatch.Groups[3].Value } else { 0 }

Write-Info "Current Version: $currentMajor.$currentMinor.$currentPatch"

# 计算新版本号
switch ($VersionType.ToLower()) {
    "major" {
        $newMajor = $currentMajor + 1
        $newMinor = 0
        $newPatch = 0
    }
    "minor" {
        $newMajor = $currentMajor
        $newMinor = $currentMinor + 1
        $newPatch = 0
    }
    default {  # patch
        $newMajor = $currentMajor
        $newMinor = $currentMinor
        $newPatch = $currentPatch + 1
    }
}

$newVersion = "$newMajor.$newMinor.$newPatch"
$newAssemblyVersion = "$newMajor.$newMinor.$newPatch.0"
$newFileVersion = $newAssemblyVersion

Write-Success "New version: $newVersion"

# 交互式输入更新内容
if ($Interactive -and -not $UpdateMessage) {
    Write-ColorText "`n📝 Please enter update content:" "Yellow"
    Write-ColorText "💡 Tip: You can input multiple lines, press Enter twice to finish" "Gray"
    
    $updates = @()
    $emptyLineCount = 0
    
    while ($true) {
        $line = Read-Host
        if ([string]::IsNullOrWhiteSpace($line)) {
            $emptyLineCount++
            if ($emptyLineCount -ge 2) { break }
        } else {
            $emptyLineCount = 0
            if ($line.Trim() -ne "") {
                $updates += "- $($line.Trim())"
            }
        }
    }
    
    $UpdateMessage = $updates -join "`n"
}

# 如果没有提供更新内容，使用默认内容
if (-not $UpdateMessage) {
    $UpdateMessage = "- Version update and code optimization"
}

# 更新项目文件中的版本号
$newContent = $projectContent
$newContent = $newContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
$newContent = $newContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$newAssemblyVersion</AssemblyVersion>"
$newContent = $newContent -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$newFileVersion</FileVersion>"

# 保存项目文件
Set-Content $ProjectFile $newContent -Encoding UTF8
Write-Success "Project file updated"

# 更新版本历史文件
$versionHistoryFile = "VERSION_HISTORY.md"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$versionEntry = @"

## Version $newVersion
**Release Date:** $timestamp  
**Update Type:** $($VersionType.ToUpper())

### 🚀 Update Content
$UpdateMessage

### 📊 Technical Info
$("- AssemblyVersion: " + $newAssemblyVersion)
$("- FileVersion: " + $newFileVersion)
$("- BuildTime: " + $timestamp)

---
"@

# 如果版本历史文件不存在，创建它
if (-not (Test-Path $versionHistoryFile)) {
    $header = @"
# 📋 Version History

This file records all version update history of **Binance Futures Trader**.

---
"@
    Set-Content $versionHistoryFile $header -Encoding UTF8
}

# 将新版本信息插入到文件开头（在标题后）
$existingContent = Get-Content $versionHistoryFile -Raw
$headerEnd = $existingContent.IndexOf("---")
if ($headerEnd -ge 0) {
    $beforeHeader = $existingContent.Substring(0, $headerEnd + 3)
    $afterHeader = $existingContent.Substring($headerEnd + 3)
    $newHistoryContent = $beforeHeader + $versionEntry + $afterHeader
} else {
    $newHistoryContent = $existingContent + $versionEntry
}

Set-Content $versionHistoryFile $newHistoryContent -Encoding UTF8
Write-Success "Version history updated: $versionHistoryFile"

# 创建版本标签文件（供其他脚本使用）
$versionInfoFile = "version.json"
$versionInfo = @{
    version = $newVersion
    assemblyVersion = $newAssemblyVersion
    fileVersion = $newFileVersion
    releaseDate = $timestamp
    updateType = $VersionType
    updateMessage = $UpdateMessage
    previousVersion = "$currentMajor.$currentMinor.$currentPatch"
} | ConvertTo-Json -Depth 3

Set-Content $versionInfoFile $versionInfo -Encoding UTF8
Write-Success "Version info saved: $versionInfoFile"

# 输出摘要
Write-ColorText "`n🎉 Version upgrade completed!" "Green"
Write-ColorText "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Gray"
Write-ColorText "📦 Version: $currentMajor.$currentMinor.$currentPatch → $newVersion" "White"
Write-ColorText "🏷️ Type: $($VersionType.ToUpper())" "White"
Write-ColorText "📝 File: $ProjectFile" "White"
Write-ColorText "📚 History: $versionHistoryFile" "White"
Write-ColorText "ℹ️ Info: $versionInfoFile" "White"
Write-ColorText "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Gray"

# 询问是否立即编译
$buildChoice = Read-Host "`n🔨 Build project now? (Y/n)"
if ($buildChoice -ne "n" -and $buildChoice -ne "N") {
    Write-Info "Starting build..."
    & dotnet build $ProjectFile --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Build successful! New version ready 🚀"
        
        # 显示编译后的文件信息
        $outputPath = "bin/Release/net6.0-windows"
        if (Test-Path "$outputPath/BinanceFuturesTrader.exe") {
            $fileInfo = Get-Item "$outputPath/BinanceFuturesTrader.exe"
            $fileVersion = (Get-ItemProperty $fileInfo.FullName).VersionInfo.FileVersion
            Write-Info "Output file: $($fileInfo.FullName)"
            Write-Info "File version: $fileVersion"
            Write-Info "File size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB"
        }
    } else {
        Write-Error "Build failed, please check code"
        exit 1
    }
} else {
    Write-Info "Version updated, please build manually"
}

Write-Success "Version management completed!" 