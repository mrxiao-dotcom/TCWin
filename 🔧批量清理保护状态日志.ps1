# PowerShell脚本：批量清理保护状态日志
$file = "Views\AutoMonitorConfigWindowSimple.xaml.cs"

Write-Host "正在清理保护状态日志..." -ForegroundColor Yellow

# 读取文件内容
$content = Get-Content $file -Encoding UTF8

# 定义要删除的日志模式
$patterns = @(
    'AddLog\(\$"🔒\[RefreshPositionDataAsync\]保护推仓\d状态不被覆盖.*?\);',
    'AddLog\(\$"🔒\[RefreshPositionDataAsync\]保护止盈\d状态不被覆盖.*?\);',
    'AddLog\(\$"🔒\[RefreshPositionDataAsync\]新配置保护保本状态不被覆盖.*?\);'
)

# 统计删除的行数
$deletedLines = 0

# 处理每一行
$newContent = @()
foreach ($line in $content) {
    $shouldKeep = $true
    
    foreach ($pattern in $patterns) {
        if ($line -match $pattern) {
            $shouldKeep = $false
            $deletedLines++
            Write-Host "删除: $($line.Trim())" -ForegroundColor Red
            break
        }
    }
    
    if ($shouldKeep) {
        $newContent += $line
    }
}

# 写回文件
$newContent | Set-Content $file -Encoding UTF8

Write-Host "✅ 清理完成！删除了 $deletedLines 行保护状态日志" -ForegroundColor Green
Write-Host "🔧 请重新编译项目以验证效果" -ForegroundColor Cyan

# 立即编译验证
Write-Host "正在编译项目..." -ForegroundColor Yellow
dotnet build BinanceFuturesTrader.csproj --verbosity minimal

Write-Host "✅ 批量清理和编译完成！" -ForegroundColor Green 