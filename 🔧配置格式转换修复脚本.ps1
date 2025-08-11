# 🔧 基础配置格式转换修复脚本
Write-Host "🔧 开始转换基础配置格式..." -ForegroundColor Yellow

$configPath = "$env:APPDATA\BinanceFuturesTrader\Global\auto_monitor_configs.json"
$backupPath = "$env:APPDATA\BinanceFuturesTrader\Global\auto_monitor_configs_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"

try {
    # 检查配置文件是否存在
    if (-not (Test-Path $configPath)) {
        Write-Host "❌ 配置文件不存在: $configPath" -ForegroundColor Red
        Write-Host "💡 请先启动程序并创建一个基础配置" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "📄 配置文件存在: $configPath" -ForegroundColor Green
    
    # 显示文件信息
    $fileInfo = Get-Item $configPath
    Write-Host "📊 文件大小: $($fileInfo.Length) 字节" -ForegroundColor Cyan
    Write-Host "📅 修改时间: $($fileInfo.LastWriteTime)" -ForegroundColor Cyan

    # 备份原文件
    Copy-Item $configPath $backupPath -Force
    Write-Host "✅ 已备份原配置文件到: $backupPath" -ForegroundColor Green

    # 读取原配置
    $jsonContent = Get-Content $configPath -Raw -Encoding UTF8
    Write-Host "📖 读取文件内容长度: $($jsonContent.Length) 字符" -ForegroundColor Cyan

    # 显示文件内容预览
    $preview = if ($jsonContent.Length -gt 300) { $jsonContent.Substring(0, 300) + "..." } else { $jsonContent }
    Write-Host "📝 文件内容预览:" -ForegroundColor Cyan
    Write-Host $preview

    # 解析JSON
    $oldConfig = $jsonContent | ConvertFrom-Json
    Write-Host "✅ JSON解析成功" -ForegroundColor Green

    # 检查是否是旧格式
    if ($oldConfig.accountConfigs) {
        Write-Host "🔍 检测到旧格式 (accountConfigs 结构)" -ForegroundColor Yellow
        
        # 转换为新格式
        $newConfigs = @()
        
        foreach ($accountName in $oldConfig.accountConfigs.PSObject.Properties.Name) {
            $accountConfig = $oldConfig.accountConfigs.$accountName
            Write-Host "🔄 转换账户配置: $accountName -> $($accountConfig.name)" -ForegroundColor Cyan
            
            # 清理配置，只保留基础配置部分，移除状态信息
            $cleanConfig = @{
                name = $accountConfig.name
                isEnabled = $accountConfig.isEnabled
                scanIntervalSeconds = $accountConfig.scanIntervalSeconds
                cooldownSeconds = $accountConfig.cooldownSeconds
                createTime = $accountConfig.createTime
                lastModifiedTime = $accountConfig.lastModifiedTime
                breakEvenConfig = @{
                    isEnabled = $accountConfig.breakEvenConfig.isEnabled
                    triggerProfitAmount = $accountConfig.breakEvenConfig.triggerProfitAmount
                }
                addPositionConfig = @{
                    isEnabled = $accountConfig.addPositionConfig.isEnabled
                    tiers = @()
                }
                profitProtectionConfig = @{
                    isEnabled = $accountConfig.profitProtectionConfig.isEnabled
                    tiers = @()
                }
            }
            
            # 转换推仓配置（移除状态信息）
            if ($accountConfig.addPositionConfig.tiers) {
                foreach ($tier in $accountConfig.addPositionConfig.tiers) {
                    $cleanTier = @{
                        tierIndex = $tier.tierIndex
                        isEnabled = $tier.isEnabled
                        triggerProfitAmount = $tier.triggerProfitAmount
                        riskMultiplier = $tier.riskMultiplier
                        stopLossRatio = $tier.stopLossRatio
                        profitProtectionAmount = $tier.profitProtectionAmount
                        description = $tier.description
                    }
                    $cleanConfig.addPositionConfig.tiers += $cleanTier
                }
            }
            
            # 转换保盈配置（移除状态信息）
            if ($accountConfig.profitProtectionConfig.tiers) {
                foreach ($tier in $accountConfig.profitProtectionConfig.tiers) {
                    $cleanTier = @{
                        tierIndex = $tier.tierIndex
                        isEnabled = $tier.isEnabled
                        triggerProfitAmount = $tier.triggerProfitAmount
                        profitProtectionAmount = $tier.profitProtectionAmount
                        description = $tier.description
                    }
                    $cleanConfig.profitProtectionConfig.tiers += $cleanTier
                }
            }
            
            $newConfigs += $cleanConfig
        }
        
        Write-Host "🔄 转换完成，共转换 $($newConfigs.Count) 个基础配置" -ForegroundColor Green
        
        # 转换为新格式JSON
        $newJson = $newConfigs | ConvertTo-Json -Depth 10
        
        # 保存新格式
        $newJson | Out-File $configPath -Encoding UTF8
        Write-Host "✅ 新格式配置已保存到: $configPath" -ForegroundColor Green
        
        # 验证转换结果
        $newFileInfo = Get-Item $configPath
        Write-Host "📊 转换后文件大小: $($newFileInfo.Length) 字节" -ForegroundColor Cyan
        
        # 验证能否正确解析为数组
        $verifyConfig = Get-Content $configPath -Raw | ConvertFrom-Json
        if ($verifyConfig -is [array]) {
            Write-Host "✅ 验证成功：新格式为数组，包含 $($verifyConfig.Count) 个配置" -ForegroundColor Green
            foreach ($config in $verifyConfig) {
                Write-Host "  - 配置名称: $($config.name)" -ForegroundColor Cyan
            }
        } else {
            Write-Host "❌ 验证失败：转换后格式不是数组" -ForegroundColor Red
        }
        
    } elseif ($oldConfig -is [array]) {
        Write-Host "✅ 配置已经是新格式 (数组格式)" -ForegroundColor Green
        Write-Host "📊 包含 $($oldConfig.Count) 个基础配置" -ForegroundColor Cyan
        foreach ($config in $oldConfig) {
            Write-Host "  - 配置名称: $($config.name)" -ForegroundColor Cyan
        }
        Write-Host "💡 无需转换" -ForegroundColor Cyan
        
    } else {
        Write-Host "❌ 无法识别的配置格式" -ForegroundColor Red
        Write-Host "🔍 根元素类型: $($oldConfig.GetType().Name)" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ 转换失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "📋 详细错误: $($_.Exception.ToString())" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 配置格式转换完成！" -ForegroundColor Green
Write-Host "💡 现在可以重新启动程序，基础配置应该能正常加载了" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 转换摘要:" -ForegroundColor Yellow
Write-Host "- 原文件已备份到: $backupPath" -ForegroundColor Gray
Write-Host "- 新格式文件: $configPath" -ForegroundColor Gray
Write-Host "- 格式: 旧格式 (accountConfigs) -> 新格式 (数组)" -ForegroundColor Gray 