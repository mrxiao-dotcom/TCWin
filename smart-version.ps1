# ===================================================================
# 智能版本检测脚本 - smart-version.ps1
# 功能：分析Git提交信息自动判断版本升级类型
# 作者：Trading Tools Team
# ===================================================================

param(
    [int]$CommitCount = 5,      # 分析最近的提交数量
    [switch]$Interactive = $false,  # 是否交互式确认
    [switch]$DryRun = $false        # 仅预览，不执行升级
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

Write-Info "🤖 Starting smart version detection..."

# 检查是否在Git仓库中
try {
    $gitStatus = git status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Current directory is not a Git repository"
        exit 1
    }
} catch {
    Write-Error "Git is not installed or available"
    exit 1
}

# 获取最近的提交记录
Write-Info "Analyzing recent $CommitCount commits..."
try {
    $gitLog = git log --oneline -n $CommitCount 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cannot get Git commit history"
        exit 1
    }
} catch {
    Write-Error "Failed to get Git log"
    exit 1
}

Write-Info "Commit history:"
$gitLog | ForEach-Object { Write-Host "  📝 $_" -ForegroundColor Gray }

# 分析提交信息中的关键词
$commitText = $gitLog -join " "

# 定义版本类型检测规则
$majorPatterns = @(
    "BREAKING", "major", "refactor", "architecture", "重构", "架构", "重大"
)

$minorPatterns = @(
    "feat", "feature", "add", "implement", "upgrade", "enhance"
)

$patchPatterns = @(
    "fix", "bug", "patch", "hotfix", "bugfix", "improve"
)

# 检测版本类型
$hasMajor = $false
$hasMinor = $false
$hasPatch = $false

foreach ($pattern in $majorPatterns) {
    if ($commitText -match $pattern) {
        $hasMajor = $true
        Write-Warning "Detected Major keyword: $pattern"
        break
    }
}

if (-not $hasMajor) {
    foreach ($pattern in $minorPatterns) {
        if ($commitText -match $pattern) {
            $hasMinor = $true
            Write-Info "Detected Minor keyword: $pattern"
            break
        }
    }
}

if (-not $hasMajor -and -not $hasMinor) {
    foreach ($pattern in $patchPatterns) {
        if ($commitText -match $pattern) {
            $hasPatch = $true
            Write-Info "Detected Patch keyword: $pattern"
            break
        }
    }
}

# 确定版本类型
if ($hasMajor) {
    $versionType = "major"
    $versionIcon = "🎯"
    $versionDesc = "Major version update"
} elseif ($hasMinor) {
    $versionType = "minor"
    $versionIcon = "📈"
    $versionDesc = "Minor version update"
} elseif ($hasPatch) {
    $versionType = "patch"
    $versionIcon = "📦"
    $versionDesc = "Patch version update"
} else {
    $versionType = "patch"
    $versionIcon = "📦"
    $versionDesc = "Default version update"
    Write-Warning "No specific version type detected, using default patch upgrade"
}

# 显示检测结果
Write-ColorText "`n🎯 Smart Detection Result" "Yellow"
Write-ColorText "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Gray"
Write-ColorText "$versionIcon Recommended version type: $($versionType.ToUpper())" "White"
Write-ColorText "📝 Update description: $versionDesc" "White"
Write-ColorText "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Gray"

# 仅预览模式
if ($DryRun) {
    Write-Success "Preview mode: Recommend using $versionType version upgrade"
    exit 0
}

# 交互式确认
if ($Interactive) {
    $confirm = Read-Host "`n🤔 Use recommended version type '$versionType'? (Y/n/custom)"
    
    if ($confirm -eq "n" -or $confirm -eq "N") {
        Write-Info "User cancelled version upgrade"
        exit 0
    } elseif ($confirm -eq "custom" -or $confirm -eq "c") {
        Write-ColorText "`n📋 Please select version type:" "Yellow"
        Write-Host "  1. patch  - Fix version (recommended for bug fixes)"
        Write-Host "  2. minor  - Minor version (recommended for new features)"
        Write-Host "  3. major  - Major version (recommended for major changes)"
        
        $choice = Read-Host "`nPlease enter option (1-3)"
        switch ($choice) {
            "1" { $versionType = "patch" }
            "2" { $versionType = "minor" }
            "3" { $versionType = "major" }
            default { 
                Write-Warning "Invalid choice, using recommended type: $versionType"
            }
        }
    }
}

# 执行版本升级
Write-Success "Executing version upgrade: $versionType"

# 检查AutoVersion.ps1是否存在
if (-not (Test-Path "AutoVersion.ps1")) {
    Write-Error "AutoVersion.ps1 file not found"
    Write-Info "Please ensure running this script in project root directory"
    exit 1
}

# 生成更新说明
$updateMessage = @()
$updateMessage += "Smart detection upgrade ($versionType)"
$updateMessage += ""
$updateMessage += "Based on commit analysis:"

$gitLog | ForEach-Object { 
    $updateMessage += "- $_"
}

$updateMessageText = $updateMessage -join "`n"

# 调用AutoVersion.ps1
try {
    if ($Interactive) {
        & .\AutoVersion.ps1 -VersionType $versionType -Interactive
    } else {
        & .\AutoVersion.ps1 -VersionType $versionType -UpdateMessage $updateMessageText
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "🎉 Smart version upgrade completed!"
    } else {
        Write-Error "Version upgrade failed"
        exit 1
    }
} catch {
    Write-Error "Error executing version upgrade: $($_.Exception.Message)"
    exit 1
}

Write-ColorText "`n💡 Tip: You can configure this script as external tool in VS2022" "Gray"
Write-ColorText "   Title: 🤖 Smart Version Upgrade" "Gray"
Write-ColorText "   Command: powershell.exe" "Gray"
Write-ColorText "   Args: -ExecutionPolicy Bypass -File `"$(ProjectDir)smart-version.ps1`" -Interactive" "Gray" 