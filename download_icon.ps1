# 下载示例图标的PowerShell脚本
# 币安期货交易管理器图标下载器

Write-Host "🎨 币安期货交易管理器 - 图标下载器" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# 检查Icons文件夹是否存在
if (-not (Test-Path "Icons")) {
    Write-Host "❌ Icons文件夹不存在，正在创建..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "Icons" -Force
    Write-Host "✅ Icons文件夹创建成功" -ForegroundColor Green
}

# 示例图标URL列表（免费图标）
$iconUrls = @{
    "trading-chart" = "https://cdn-icons-png.flaticon.com/512/2991/2991148.png"
    "bitcoin" = "https://cdn-icons-png.flaticon.com/512/5968/5968260.png"
    "finance" = "https://cdn-icons-png.flaticon.com/512/2942/2942813.png"
    "analytics" = "https://cdn-icons-png.flaticon.com/512/1055/1055687.png"
}

Write-Host "📋 可用的图标主题：" -ForegroundColor White
Write-Host "1. trading-chart - 交易图表" -ForegroundColor Gray
Write-Host "2. bitcoin - 比特币" -ForegroundColor Gray  
Write-Host "3. finance - 金融" -ForegroundColor Gray
Write-Host "4. analytics - 数据分析" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "请选择图标主题 (1-4)"

$selectedTheme = switch ($choice) {
    "1" { "trading-chart" }
    "2" { "bitcoin" }
    "3" { "finance" }
    "4" { "analytics" }
    default { "trading-chart" }
}

$selectedUrl = $iconUrls[$selectedTheme]
Write-Host "🎯 选择的主题: $selectedTheme" -ForegroundColor Green

try {
    Write-Host "📥 正在下载图标..." -ForegroundColor Yellow
    
    # 下载PNG图片
    $pngPath = "Icons\temp_icon.png"
    Invoke-WebRequest -Uri $selectedUrl -OutFile $pngPath -UseBasicParsing
    
    Write-Host "✅ PNG图标下载成功" -ForegroundColor Green
    Write-Host "⚠️  注意：下载的是PNG格式，需要转换为ICO格式" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "🔧 转换步骤：" -ForegroundColor Cyan
    Write-Host "1. 访问 https://convertio.co/png-ico/" -ForegroundColor White
    Write-Host "2. 上传 Icons\temp_icon.png 文件" -ForegroundColor White
    Write-Host "3. 转换为ICO格式" -ForegroundColor White
    Write-Host "4. 下载转换后的文件并重命名为 app.ico" -ForegroundColor White
    Write-Host "5. 将 app.ico 放入 Icons 文件夹" -ForegroundColor White
    Write-Host ""
    Write-Host "💡 或者使用在线工具直接搜索ICO格式的图标：" -ForegroundColor Cyan
    Write-Host "   - https://iconarchive.com/" -ForegroundColor White
    Write-Host "   - https://www.flaticon.com/" -ForegroundColor White
    
} catch {
    Write-Host "❌ 下载失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 手动获取图标的方法：" -ForegroundColor Cyan
    Write-Host "1. 访问 https://www.flaticon.com/" -ForegroundColor White
    Write-Host "2. 搜索 'trading', 'finance', 'chart' 等关键词" -ForegroundColor White
    Write-Host "3. 选择合适的图标并下载ICO格式" -ForegroundColor White
    Write-Host "4. 重命名为 app.ico 并放入 Icons 文件夹" -ForegroundColor White
}

Write-Host ""
Write-Host "🎨 完成图标设置后，运行以下命令编译项目：" -ForegroundColor Cyan
Write-Host "   dotnet build" -ForegroundColor White
Write-Host ""
Write-Host "✨ 图标设置完成后，你的程序将拥有专业的视觉标识！" -ForegroundColor Green

Read-Host "按任意键退出" 