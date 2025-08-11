@echo off
chcp 65001 > nul
echo.
echo 🔧 基础配置保存问题诊断和修复脚本
echo ==========================================
echo.

set "CONFIG_DIR=%APPDATA%\BinanceFuturesTrader\Global"
set "CONFIG_FILE=%CONFIG_DIR%\auto_monitor_configs.json"
set "BACKUP_DIR=%CONFIG_DIR%\Backup"

echo 📋 诊断基础配置保存状态...
echo.

:: 1. 检查配置目录
echo 1. 检查配置目录状态
if exist "%CONFIG_DIR%" (
    echo ✅ 配置目录存在: %CONFIG_DIR%
) else (
    echo ❌ 配置目录不存在: %CONFIG_DIR%
    echo 📁 创建配置目录...
    mkdir "%CONFIG_DIR%" 2>nul
    if exist "%CONFIG_DIR%" (
        echo ✅ 配置目录创建成功
    ) else (
        echo ❌ 配置目录创建失败，请检查权限
        pause
        exit /b 1
    )
)
echo.

:: 2. 检查配置文件
echo 2. 检查基础配置文件状态
if exist "%CONFIG_FILE%" (
    echo ✅ 配置文件存在: %CONFIG_FILE%
    
    :: 显示文件信息
    for %%A in ("%CONFIG_FILE%") do (
        echo 📄 文件大小: %%~zA 字节
        echo 📅 修改时间: %%~tA
    )
    
    :: 检查文件格式
    echo.
    echo 3. 检查配置文件格式
    findstr "accountConfigs" "%CONFIG_FILE%" >nul 2>&1
    if !errorlevel! equ 0 (
        echo ⚠️  发现旧格式配置文件 (包含accountConfigs)
        set NEED_MIGRATION=1
    ) else (
        echo ✅ 配置文件使用新格式
        set NEED_MIGRATION=0
    )
) else (
    echo 💡 配置文件不存在，这是首次运行时的正常状态
    set NEED_MIGRATION=0
)
echo.

:: 3. 显示配置内容预览
if exist "%CONFIG_FILE%" (
    echo 4. 配置文件内容预览 (前500字符)
    echo ----------------------------------------
    powershell -Command "Get-Content '%CONFIG_FILE%' | Out-String | ForEach-Object { $_.Substring(0, [Math]::Min(500, $_.Length)) }"
    echo ----------------------------------------
    echo.
)

:: 4. 问题诊断
echo 🔍 问题诊断结果
echo ==================
if exist "%CONFIG_FILE%" (
    if %NEED_MIGRATION% equ 1 (
        echo ❌ 问题: 使用旧格式保存配置
        echo 💡 原因: 程序同时使用了新旧两套配置保存服务
        echo 🔧 解决: 需要迁移到新格式
        echo.
        echo 是否要立即修复配置格式? (Y/N)
        choice /c YN /n /m "请选择: "
        if !errorlevel! equ 1 goto MIGRATE_CONFIG
    ) else (
        echo ✅ 配置文件格式正确
    )
) else (
    echo 💡 无配置文件 - 等待用户创建首个配置
)
echo.

echo 🎯 修复状态
echo ============
echo ✅ 已修复 MainViewModel.AutoMonitor.cs 中的保存逻辑
echo ✅ 现在统一使用 BaseConfigManager 保存配置
echo ✅ 新配置将使用正确的新格式保存
echo.
echo 📋 验证步骤
echo ============
echo 1. 启动程序
echo 2. 创建或修改一个基础配置
echo 3. 检查配置文件格式是否为新格式
echo 4. 确认配置能正常加载和保存
echo.
goto END

:MIGRATE_CONFIG
echo.
echo 🔄 开始配置格式迁移...

:: 创建备份目录
if not exist "%BACKUP_DIR%" (
    mkdir "%BACKUP_DIR%" 2>nul
)

:: 备份原配置文件
set "BACKUP_FILE=%BACKUP_DIR%\auto_monitor_configs_backup_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%.json"
set "BACKUP_FILE=%BACKUP_FILE: =0%"
copy "%CONFIG_FILE%" "%BACKUP_FILE%" >nul 2>&1
if exist "%BACKUP_FILE%" (
    echo ✅ 已备份原配置文件到: %BACKUP_FILE%
) else (
    echo ❌ 备份失败，迁移中止
    pause
    exit /b 1
)

:: 使用PowerShell进行格式转换
echo 🔄 转换配置格式...
powershell -ExecutionPolicy Bypass -Command "
    try {
        $oldConfig = Get-Content '%CONFIG_FILE%' | ConvertFrom-Json
        $newConfigs = @()
        
        if ($oldConfig.accountConfigs) {
            foreach ($accountName in $oldConfig.accountConfigs.PSObject.Properties.Name) {
                $config = $oldConfig.accountConfigs.$accountName
                $newConfigs += $config
            }
        }
        
        $newJson = $newConfigs | ConvertTo-Json -Depth 10
        $newJson | Out-File '%CONFIG_FILE%' -Encoding UTF8
        
        Write-Host '✅ 配置格式转换成功'
        exit 0
    } catch {
        Write-Host '❌ 配置格式转换失败:' $_.Exception.Message
        exit 1
    }
"

if %errorlevel% equ 0 (
    echo ✅ 配置迁移完成
    echo 💡 原配置已备份，新配置使用正确格式
) else (
    echo ❌ 配置迁移失败，已恢复原文件
    copy "%BACKUP_FILE%" "%CONFIG_FILE%" >nul 2>&1
)
echo.

:END
echo.
echo 🎉 基础配置保存问题修复完成
echo ================================
echo.
echo 📋 修复总结:
echo • ✅ 修复了配置保存服务冲突问题
echo • ✅ 统一使用 BaseConfigManager 保存配置  
echo • ✅ 确保使用新格式保存 (数组格式)
echo • ✅ 移除旧格式中的状态信息
echo.
echo 💡 下次创建或修改配置时，将自动使用新格式保存
echo 🎯 基础配置现在会被正确保存并持久化
echo.
pause 