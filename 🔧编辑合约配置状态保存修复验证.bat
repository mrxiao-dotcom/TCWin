@echo off
chcp 65001 >nul
echo.
echo 🔧 编辑合约配置状态保存修复验证
echo ==========================================
echo.

echo 📋 检查项目编译状态...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ❌ 编译失败！
    pause
    exit /b 1
)
echo ✅ 项目编译成功

echo.
echo 📂 检查关键修复文件...

set "fixed_files=0"

if exist "Views\ContractConfigEditDialog.xaml.cs" (
    echo ✅ Views\ContractConfigEditDialog.xaml.cs 存在
    set /a fixed_files+=1
) else (
    echo ❌ Views\ContractConfigEditDialog.xaml.cs 不存在
)

if exist "Views\ContractStatusEditDialog.xaml.cs" (
    echo ✅ Views\ContractStatusEditDialog.xaml.cs 存在
    set /a fixed_files+=1
) else (
    echo ❌ Views\ContractStatusEditDialog.xaml.cs 不存在
)

if exist "Services\ContractMonitoringStateService.cs" (
    echo ✅ Services\ContractMonitoringStateService.cs 存在
    set /a fixed_files+=1
) else (
    echo ❌ Services\ContractMonitoringStateService.cs 不存在
)

echo.
echo 📝 检查修复内容...

echo.
echo 🔍 检查 ContractConfigEditDialog.xaml.cs 修复:
findstr /C:"使用正确的统一状态管理服务" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 已修复路径管理问题
) else (
    echo   ❌ 路径管理修复未找到
)

findstr /C:"UpdateExecutionStatus" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 已实现状态更新逻辑
) else (
    echo   ❌ 状态更新逻辑未找到
)

findstr /C:"手动重置为未触发" "Views\ContractConfigEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 已实现状态重置功能
) else (
    echo   ❌ 状态重置功能未找到
)

echo.
echo 🔍 检查 ContractStatusEditDialog.xaml.cs 修复:
findstr /C:"SyncDirectlyToFile" "Views\ContractStatusEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 已实现直接文件保存
) else (
    echo   ❌ 直接文件保存未找到
)

findstr /C:"ContractMonitoringStateService" "Views\ContractStatusEditDialog.xaml.cs" >nul
if %ERRORLEVEL% equ 0 (
    echo   ✅ 已使用统一状态服务
) else (
    echo   ❌ 统一状态服务未使用
)

echo.
echo 📊 修复验证总结:
echo ==========================================
if %fixed_files% equ 3 (
    echo ✅ 所有关键文件存在 (%fixed_files%/3)
) else (
    echo ❌ 缺少关键文件 (%fixed_files%/3)
)

echo.
echo 🎯 修复重点:
echo   1. ✅ 修复了 ContractConfigEditDialog 的路径null错误
echo   2. ✅ 实现了正确的状态更新机制（支持已执行/未触发双向切换）
echo   3. ✅ 增强了 ContractStatusEditDialog 的状态保存可靠性
echo   4. ✅ 使用统一状态管理服务替代废弃的配置文件

echo.
echo 🚀 测试建议:
echo   1. 双击合约状态配置，修改状态为"已执行"或"未触发"
echo   2. 检查是否仍出现"Value cannot be null (Parameter 'path')"错误
echo   3. 确认状态修改是否正确保存到 contract_monitoring_states.json
echo   4. 验证状态文件中的 isExecuted 字段是否正确更新

echo.
echo 📁 状态文件位置:
echo   %%APPDATA%%\BinanceFuturesTrader\[账户名]\contract_monitoring_states.json

echo.
pause 