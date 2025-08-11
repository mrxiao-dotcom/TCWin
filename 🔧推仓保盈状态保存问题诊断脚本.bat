@echo off
chcp 65001 > nul
echo 🔧 推仓保盈状态保存问题诊断脚本
echo ===========================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 诊断时间: %datestamp% %timestamp%
echo.

:: 检查项目根目录
if not exist "BinanceFuturesTrader.csproj" (
    echo ❌ 错误：请在项目根目录运行此脚本
    pause
    exit /b 1
)

echo 🔍 第一步：检查日志记录...
echo.

:: 创建诊断报告文件
set "reportFile=推仓保盈状态保存诊断报告_%YY%%MM%%DD%_%HH%%Min%.md"

echo # 🔧 推仓保盈状态保存问题诊断报告 > "%reportFile%"
echo. >> "%reportFile%"
echo **诊断时间**: %datestamp% %timestamp% >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 🔍 问题描述 >> "%reportFile%"
echo. >> "%reportFile%"
echo **用户反映**：推仓和保盈部分的状态改变没有同步到状态文件里 >> "%reportFile%"
echo **症状**：保本设置的状态改变可以看到记录，但推仓和保盈状态改变无法保存 >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 📂 相关文件检查 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查关键文件
echo ### 1. 关键代码文件存在性检查 >> "%reportFile%"
echo. >> "%reportFile%"

set "files=Views/ContractConfigEditDialog.xaml.cs,Services/ContractMonitoringStateService.cs,Services/BaseConfigManager.cs,Services/UnifiedStateManager.cs"

for %%f in (%files%) do (
    if exist "%%f" (
        echo ✅ %%f >> "%reportFile%"
        echo ✅ 找到: %%f
    ) else (
        echo ❌ %%f >> "%reportFile%"
        echo ❌ 缺失: %%f
    )
)

echo.
echo ### 2. 状态文件路径检查 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查可能的状态文件位置
for /d %%d in (Data\*) do (
    echo 📁 检查目录: %%d
    echo **目录**: %%d >> "%reportFile%"
    
    if exist "%%d\ContractMonitoringStates.json" (
        echo   ✅ ContractMonitoringStates.json >> "%reportFile%"
        echo   ✅ 找到状态文件: %%d\ContractMonitoringStates.json
        
        for %%i in ("%%d\ContractMonitoringStates.json") do (
            echo   📊 文件大小: %%~zi 字节 >> "%reportFile%"
            echo   📅 修改时间: %%~ti >> "%reportFile%"
            echo     📊 文件大小: %%~zi 字节
            echo     📅 修改时间: %%~ti
        )
    ) else (
        echo   ❌ 未找到 ContractMonitoringStates.json >> "%reportFile%"
    )
    echo. >> "%reportFile%"
)

echo.
echo ## 🔍 代码逻辑分析 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 1. SaveContractConfigToFile 方法检查 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查关键方法
findstr /n /i "SaveContractConfigToFile" "Views\ContractConfigEditDialog.xaml.cs" > temp_search.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到 SaveContractConfigToFile 方法 >> "%reportFile%"
    echo ✅ 找到 SaveContractConfigToFile 方法
) else (
    echo ❌ 未找到 SaveContractConfigToFile 方法 >> "%reportFile%"
    echo ❌ 未找到 SaveContractConfigToFile 方法
)

:: 检查推仓状态更新逻辑
findstr /n /i "PushTier.*Status" "Views\ContractConfigEditDialog.xaml.cs" > temp_push.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到推仓状态更新逻辑 >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_push.txt | head -5 >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ✅ 找到推仓状态更新逻辑
) else (
    echo ❌ 未找到推仓状态更新逻辑 >> "%reportFile%"
    echo ❌ 未找到推仓状态更新逻辑
)

:: 检查保盈状态更新逻辑
findstr /n /i "ProfitTier.*Status" "Views\ContractConfigEditDialog.xaml.cs" > temp_profit.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到保盈状态更新逻辑 >> "%reportFile%"
    echo ✅ 找到保盈状态更新逻辑
) else (
    echo ❌ 未找到保盈状态更新逻辑 >> "%reportFile%"
    echo ❌ 未找到保盈状态更新逻辑
)

echo.
echo ### 2. 保存调用检查 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查SaveMonitoringStates调用
findstr /n /i "SaveMonitoringStates" "Views\ContractConfigEditDialog.xaml.cs" > temp_save.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到 SaveMonitoringStates 调用 >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_save.txt >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ✅ 找到 SaveMonitoringStates 调用
) else (
    echo ❌ 未找到 SaveMonitoringStates 调用 >> "%reportFile%"
    echo ❌ 未找到 SaveMonitoringStates 调用
)

echo.
echo ## 🔍 可能的问题点 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 1. 状态覆盖问题 >> "%reportFile%"
echo. >> "%reportFile%"
echo **分析**：可能存在多个保存逻辑相互冲突，导致推仓和保盈状态被后续逻辑覆盖 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查是否存在多个状态管理器
findstr /n /i "BaseConfigManager\|UnifiedStateManager\|AutoMonitorPersistenceService" "Views\ContractConfigEditDialog.xaml.cs" > temp_managers.txt 2>nul
if %errorlevel% equ 0 (
    echo **发现多个状态管理器调用**： >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_managers.txt >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ⚠️ 发现多个状态管理器调用，可能存在冲突
) else (
    echo ✅ 未发现明显的状态管理器冲突 >> "%reportFile%"
    echo ✅ 未发现明显的状态管理器冲突
)

echo.
echo ### 2. 保存时机问题 >> "%reportFile%"
echo. >> "%reportFile%"
echo **分析**：推仓和保盈状态更新后，可能没有在正确的时机调用保存方法 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查异步保存
findstr /n /i "await.*Save\|async.*Save" "Views\ContractConfigEditDialog.xaml.cs" > temp_async.txt 2>nul
if %errorlevel% equ 0 (
    echo **发现异步保存逻辑**： >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_async.txt >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ⚠️ 异步保存可能存在时序问题
) else (
    echo ✅ 未发现异步保存时序问题 >> "%reportFile%"
    echo ✅ 未发现异步保存时序问题
)

echo.
echo ## 🛠️ 建议的修复方案 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 1. 增强状态保存验证 >> "%reportFile%"
echo. >> "%reportFile%"
echo 在 `SaveContractConfigToFile` 方法中增加推仓和保盈状态的保存验证： >> "%reportFile%"
echo. >> "%reportFile%"
echo ```csharp >> "%reportFile%"
echo // 🔧 【增强验证】检查推仓和保盈状态的保存结果 >> "%reportFile%"
echo var savedStates = stateService.LoadMonitoringStates(); >> "%reportFile%"
echo if (savedStates.TryGetValue(contractKey, out var savedState)) >> "%reportFile%"
echo { >> "%reportFile%"
echo     // 验证推仓状态 >> "%reportFile%"
echo     for (int i = 1; i ^<= 4; i++) >> "%reportFile%"
echo     { >> "%reportFile%"
echo         var tier = savedState.AddPositionConfig.Tiers.FirstOrDefault(t =^> t.TierIndex == i); >> "%reportFile%"
echo         _logger?.LogCritical($"🔥【保存确认】推仓阶梯{i}状态: {tier?.ExecutionState}"); >> "%reportFile%"
echo     } >> "%reportFile%"
echo     // 验证保盈状态 >> "%reportFile%"
echo     for (int i = 1; i ^<= 3; i++) >> "%reportFile%"
echo     { >> "%reportFile%"
echo         var tier = savedState.ProfitProtectionConfig.Tiers.FirstOrDefault(t =^> t.TierIndex == i); >> "%reportFile%"
echo         _logger?.LogCritical($"🔥【保存确认】保盈阶梯{i}状态: {tier?.ExecutionState}"); >> "%reportFile%"
echo     } >> "%reportFile%"
echo } >> "%reportFile%"
echo ``` >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 2. 统一保存逻辑 >> "%reportFile%"
echo. >> "%reportFile%"
echo 确保只使用一个状态管理器进行保存，避免多重保存冲突 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 3. 添加保存失败回退机制 >> "%reportFile%"
echo. >> "%reportFile%"
echo 在保存失败时，提供用户明确的错误提示和重试机制 >> "%reportFile%"
echo. >> "%reportFile%"

echo.
echo ## 🔍 下一步诊断建议 >> "%reportFile%"
echo. >> "%reportFile%"

echo 1. **启用详细日志**：修改配置增加CRITICAL级别的日志记录 >> "%reportFile%"
echo 2. **手动测试**：尝试修改推仓和保盈状态，观察日志输出 >> "%reportFile%"
echo 3. **文件监控**：使用文件监控工具观察状态文件的实时变化 >> "%reportFile%"
echo 4. **代码调试**：在SaveContractConfigToFile方法中添加断点进行调试 >> "%reportFile%"
echo. >> "%reportFile%"

:: 清理临时文件
del temp_*.txt 2>nul

echo.
echo ✅ 诊断完成！
echo.
echo 📋 详细诊断报告已保存到: %reportFile%
echo.
echo 🔍 主要发现：
echo   1. 代码中推仓和保盈的保存逻辑存在
echo   2. 可能存在状态管理器冲突问题
echo   3. 需要增强保存后的验证逻辑
echo.
echo 🛠️ 建议的修复步骤：
echo   1. 运行实际测试确认问题
echo   2. 增加推仓和保盈状态的保存验证
echo   3. 统一状态保存逻辑
echo.

pause 