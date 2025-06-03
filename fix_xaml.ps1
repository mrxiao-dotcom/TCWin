$content = Get-Content "MainWindow.xaml"
$newContent = @()

for ($i = 0; $i -lt $content.Length; $i++) {
    $newContent += $content[$i]
    
    # 在第918行（索引917）后添加缺失的Card结束标签
    if ($i -eq 917) {
        $newContent += "            </materialDesign:Card>"
    }
}

$newContent | Set-Content "MainWindow.xaml"
Write-Host "XAML文件已修复" 