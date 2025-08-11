# 🔧 批量移除 RefreshPositionDataAsync 保护状态日志

$filePath = "Views\AutoMonitorConfigWindowSimple.xaml.cs"
$content = Get-Content $filePath -Raw

# 移除所有包含 "🔒【RefreshPositionDataAsync】保护" 的日志行及其 if 块
$patterns = @(
    # 移除推仓保护日志
    'if \(config\.PushTier1Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护推仓1状态不被覆盖.*?\);\s*\}',
    'if \(config\.PushTier2Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护推仓2状态不被覆盖.*?\);\s*\}',
    'if \(config\.PushTier3Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护推仓3状态不被覆盖.*?\);\s*\}',
    'if \(config\.PushTier4Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护推仓4状态不被覆盖.*?\);\s*\}',
    
    # 移除止盈保护日志
    'if \(config\.ProfitTier1Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护止盈1状态不被覆盖.*?\);\s*\}',
    'if \(config\.ProfitTier2Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护止盈2状态不被覆盖.*?\);\s*\}',
    'if \(config\.ProfitTier3Status != "-"\)\s*\{\s*AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护止盈3状态不被覆盖.*?\);\s*\}',
    
    # 移除保本保护日志
    'AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]新配置保护保本状态不被覆盖.*?\);',
    'AddLog\(\$"🔒\[【RefreshPositionDataAsync】\]保护保本状态不被覆盖.*?\);'
)

foreach ($pattern in $patterns) {
    $content = $content -replace $pattern, "", "Multiline,Singleline"
}

# 清理多余的空行
$content = $content -replace '\n\s*\n\s*\n', "`n`n"

# 写回文件
$content | Set-Content $filePath -NoNewline

Write-Host "✅ 已成功移除所有 RefreshPositionDataAsync 保护状态日志" -ForegroundColor Green
Write-Host "📁 文件已更新: $filePath" -ForegroundColor Cyan 