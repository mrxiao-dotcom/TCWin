# Simple encoding fix script for Chinese characters
$filePath = "Views/AutoMonitorDashboard.xaml.cs"
$content = Get-Content $filePath -Raw -Encoding UTF8

# Fix basic encoding issues
$content = $content -replace "鏈惎鍔?", "未启动"
$content = $content -replace "鏈厤缃?", "未配置"
$content = $content -replace "鏈惎鐢?", "未启用"
$content = $content -replace "30秒?", "30秒"
$content = $content -replace "计算中\?\.?", "计算中.."
$content = $content -replace "无冷却?", "无冷却"
$content = $content -replace "开始自动盯盘监控?", "开始自动盯盘监控"
$content = $content -replace "绯荤粺灏辩华", "系统就绪"
$content = $content -replace "鍚姩鐩洏", "启动盯盘"
$content = $content -replace "鍋滄鐩洏", "停止盯盘"

# Fix string literal syntax issues
$content = $content -replace '= "([^"]*);', '= "$1";'
$content = $content -replace '= "([^"]*)"?([^;]*);', '= "$1$2";'

# Fix quotes issues
$content = $content -replace '"([^"]*)"?([^;]*);', '"$1$2";'

# Save the fixed content
$content | Out-File -FilePath $filePath -Encoding UTF8
Write-Host "Fixed basic encoding issues" -ForegroundColor Green 