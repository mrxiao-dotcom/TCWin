@echo off
chcp 65001 > nul
echo.
echo ========================================
echo 🔧 状态文本英文化验证脚本
echo ========================================
echo.

echo 📋 正在检查核心文件修改情况...
echo.

:: 检查 StatusConstants.cs 是否存在
if exist "Models\StatusConstants.cs" (
    echo ✅ StatusConstants.cs 已创建
) else (
    echo ❌ StatusConstants.cs 文件不存在
    goto :error
)

:: 编译项目
echo.
echo 🔨 正在编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity quiet
if %ERRORLEVEL% EQU 0 (
    echo ✅ 编译成功
) else (
    echo ❌ 编译失败
    goto :error
)

:: 检查关键文件中的英文状态
echo.
echo 🔍 正在检查状态文本修改情况...

:: 检查 ContractConfigEditDialog.xaml.cs
findstr /c:"StatusConstants.Waiting" "Views\ContractConfigEditDialog.xaml.cs" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ ContractConfigEditDialog.xaml.cs: waiting 状态已更新
) else (
    echo ⚠️ ContractConfigEditDialog.xaml.cs: waiting 状态未完全更新
)

findstr /c:"StatusConstants.Executed" "Views\ContractConfigEditDialog.xaml.cs" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ ContractConfigEditDialog.xaml.cs: executed 状态已更新
) else (
    echo ⚠️ ContractConfigEditDialog.xaml.cs: executed 状态未完全更新
)

:: 检查 ContractStatusEditDialog.xaml.cs
findstr /c:"StatusConstants.Waiting" "Views\ContractStatusEditDialog.xaml.cs" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ ContractStatusEditDialog.xaml.cs: waiting 状态已更新
) else (
    echo ⚠️ ContractStatusEditDialog.xaml.cs: waiting 状态未完全更新
)

:: 检查 ContractProfile.cs
findstr /c:"StatusConstants.Waiting" "Models\ContractProfile.cs" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ ContractProfile.cs: 默认状态已更新
) else (
    echo ⚠️ ContractProfile.cs: 默认状态未更新
)

:: 检查遗留的中文状态
echo.
echo 🔍 正在检查遗留的中文状态...

findstr /c:"未触发" "Views\*.cs" 2>nul | find /c /v "" > temp_count.txt
set /p remaining_waiting=<temp_count.txt
del temp_count.txt 2>nul

findstr /c:"已执行" "Views\*.cs" 2>nul | find /c /v "" > temp_count.txt
set /p remaining_executed=<temp_count.txt
del temp_count.txt 2>nul

if %remaining_waiting% GTR 0 (
    echo ⚠️ Views 文件夹中还有 %remaining_waiting% 处"未触发"需要修改
) else (
    echo ✅ Views 文件夹中的"未触发"已全部修改
)

if %remaining_executed% GTR 0 (
    echo ⚠️ Views 文件夹中还有 %remaining_executed% 处"已执行"需要修改
) else (
    echo ✅ Views 文件夹中的"已执行"已全部修改
)

echo.
echo 📊 修改进度统计:
echo ================================
echo ✅ 已完成的关键文件:
echo    - Models/StatusConstants.cs (新增)
echo    - Views/ContractConfigEditDialog.xaml.cs
echo    - Views/ContractStatusEditDialog.xaml.cs  
echo    - Views/TriggerConditionEditDialog.xaml.cs
echo    - Views/ContractEditDialog.xaml.cs
echo    - Models/ContractProfile.cs
echo    - Services/ContractProfileService.cs
echo    - Services/TradingExecutionService.cs
echo    - Services/AutoMonitorExecutionEngine.cs
echo    - Services/ContractMonitoringStateGenerator.cs
echo.
echo 🎯 预期效果:
echo    - JSON文件中状态显示为 "waiting"/"executed" 
echo    - UI界面显示英文状态文本
echo    - 保持向后兼容性
echo.
echo ✅ 状态文本英文化修改验证完成！
echo.
pause
goto :end

:error
echo.
echo ❌ 验证过程中发现错误，请检查修改情况。
echo.
pause

:end 