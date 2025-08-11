$file = "Views\AutoMonitorConfigWindowSimple.xaml.cs"
$content = Get-Content $file

# 移除所有包含RefreshPositionDataAsync保护的日志行
$newContent = $content | Where-Object { $_ -notmatch "🔒.*RefreshPositionDataAsync.*保护" }

$newContent | Set-Content $file

Write-Host "日志清理完成" 