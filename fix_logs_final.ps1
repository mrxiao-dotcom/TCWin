# 彻底移除 🔒【RefreshPositionDataAsync】保护状态日志
$file = "Views\AutoMonitorConfigWindowSimple.xaml.cs"

if (Test-Path $file) {
    $content = Get-Content $file -Encoding UTF8
    $newContent = @()
    
    foreach ($line in $content) {
        # 跳过包含 🔒【RefreshPositionDataAsync】 的行
        if ($line -notmatch "🔒.*RefreshPositionDataAsync.*保护") {
            $newContent += $line
        } else {
            Write-Host "删除行: $line"
        }
    }
    
    $newContent | Set-Content $file -Encoding UTF8
    Write-Host "日志清理完成: 移除了 🔒【RefreshPositionDataAsync】保护状态日志"
} else {
    Write-Host "文件不存在: $file"
} 