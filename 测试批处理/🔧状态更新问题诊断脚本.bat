@echo off
chcp 65001 >nul
echo.
echo 🔧 状态更新问题诊断脚本
echo ====================================
echo.

echo 📋 1. 检查编译状态...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ❌ 编译失败！
    pause
    exit /b 1
)
echo ✅ 编译成功

echo.
echo 📂 2. 检查可能创建AutoMonitor目录的代码...

echo.
echo 🔍 搜索所有包含AutoMonitor路径的代码:
findstr /s /i "AutoMonitor" "Views\*.cs" "Services\*.cs" 2>nul | findstr /v "using.*AutoMonitor" | findstr /v "namespace.*AutoMonitor"

echo.
echo 🔍 搜索所有Directory.CreateDirectory调用:
findstr /s /n "Directory\.CreateDirectory" "Views\*.cs" "Services\*.cs" 2>nul

echo.
echo 🔍 搜索Environment.GetFolderPath调用:
findstr /s /n "Environment\.GetFolderPath" "Views\*.cs" "Services\*.cs" 2>nul

echo.
echo 🔍 搜索可能的路径组合:
findstr /s /n "Path\.Combine.*AutoMonitor" "Views\*.cs" "Services\*.cs" 2>nul

echo.
echo 📋 3. 检查已修复的文件...

echo.
echo 🔍 检查ContractConfigEditDialog.xaml.cs修复:
findstr /C:"使用正确的统一状态管理服务" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ SaveContractConfigToFile方法已修复
) else (
    echo   ❌ SaveContractConfigToFile方法修复未找到
)

findstr /C:"从统一状态文件加载已保存的合约配置" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ LoadSavedContractConfig方法已修复
) else (
    echo   ❌ LoadSavedContractConfig方法修复未找到
)

findstr /C:"ApplySavedStateFromUnifiedFile" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ ApplySavedStateFromUnifiedFile方法已添加
) else (
    echo   ❌ ApplySavedStateFromUnifiedFile方法未找到
)

echo.
echo 🔍 检查GetContractConfigFilePath方法状态:
findstr /C:"已废弃：不再使用ContractConfigs.json文件" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ GetContractConfigFilePath方法已标记废弃
) else (
    echo   ❌ GetContractConfigFilePath方法废弃标记未找到
)

findstr /C:"return string.Empty" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ GetContractConfigFilePath方法返回空字符串
) else (
    echo   ❌ GetContractConfigFilePath方法返回值问题
)

echo.
echo 📋 4. 检查状态更新逻辑...

echo.
echo 🔍 检查UpdateExecutionStatus调用:
findstr /C:"UpdateExecutionStatus" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 找到UpdateExecutionStatus调用
) else (
    echo   ❌ 未找到UpdateExecutionStatus调用
)

findstr /C:"手动重置为未触发" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 支持状态重置功能
) else (
    echo   ❌ 状态重置功能未找到
)

echo.
echo 📋 5. 检查可能的问题原因...

echo.
echo 🔍 检查合约键格式处理:
findstr /C:"_editedConfig.ContractName" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ⚠️  直接使用ContractName作为合约键（可能需要解析）
) else (
    echo   ❓ 未找到合约键处理逻辑
)

echo.
echo 🔍 检查ContractMonitoringStateService调用:
findstr /C:"ContractMonitoringStateService" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 使用了统一状态服务
) else (
    echo   ❌ 未使用统一状态服务
)

echo.
echo 📁 6. 检查文件路径结构...

set "appdata_path=%APPDATA%\BinanceFuturesTrader"
echo 🔍 检查AppData目录结构:
if exist "%appdata_path%" (
    echo   ✅ BinanceFuturesTrader目录存在: %appdata_path%
    dir "%appdata_path%" /b
    
    echo.
    echo   🔍 检查是否有AutoMonitor目录:
    if exist "%appdata_path%\AutoMonitor" (
        echo   ⚠️  AutoMonitor目录仍然存在: %appdata_path%\AutoMonitor
        dir "%appdata_path%\AutoMonitor" /b
    ) else (
        echo   ✅ AutoMonitor目录不存在
    )
    
    echo.
    echo   🔍 检查账户目录:
    if exist "%appdata_path%\Accounts" (
        echo   ✅ Accounts目录存在
        dir "%appdata_path%\Accounts" /b
    ) else (
        echo   ❌ Accounts目录不存在
    )
) else (
    echo   ❌ BinanceFuturesTrader目录不存在
)

echo.
echo 📋 诊断总结:
echo ====================================
echo 1. 检查编译是否成功
echo 2. 查找可能创建AutoMonitor目录的代码
echo 3. 验证修复的代码是否正确
echo 4. 分析状态更新可能的问题原因
echo 5. 检查实际的文件目录结构

echo.
echo 💡 如果状态更新仍有问题，可能的原因:
echo   - 合约键格式不匹配（ContractName vs Symbol_PositionSide）
echo   - 状态文件路径问题
echo   - UpdateExecutionStatus参数错误
echo   - 账户名称获取错误

echo.
pause 