@echo off
chcp 65001 > nul
echo 🔧 推仓状态保存问题增强诊断脚本
echo =============================================
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

echo 🔍 推仓状态保存问题专项诊断...
echo.

:: 创建诊断报告文件
set "reportFile=推仓状态保存问题诊断_%YY%%MM%%DD%_%HH%%Min%.md"

echo # 🔧 推仓状态保存问题诊断报告 > "%reportFile%"
echo. >> "%reportFile%"
echo **诊断时间**: %datestamp% %timestamp% >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 🔍 问题现象 >> "%reportFile%"
echo. >> "%reportFile%"
echo **用户反映**：修改推仓状态后，保存没有发现文件内的执行状态被改变 >> "%reportFile%"
echo **关键发现**：保存过程日志缺失，可能是UI状态同步问题 >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 📋 可能的原因分析 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 1. UI控件状态同步问题 >> "%reportFile%"
echo. >> "%reportFile%"
echo **问题**：推仓状态的UI控件变化可能没有正确同步到 _editedConfig >> "%reportFile%"
echo **症状**：保存时 _editedConfig 中的推仓状态仍然是原始值 >> "%reportFile%"
echo. >> "%reportFile%"

:: 检查推仓状态更新相关代码
echo ### 2. 代码逻辑检查 >> "%reportFile%"
echo. >> "%reportFile%"

echo **检查推仓状态更新事件处理**： >> "%reportFile%"
findstr /n /i "PushTier.*SelectionChanged\|PushTier.*SelectedIndexChanged" "Views\ContractConfigEditDialog.xaml.cs" > temp_push_events.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到推仓状态选择事件处理 >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_push_events.txt >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ✅ 找到推仓状态选择事件处理
) else (
    echo ❌ 未找到推仓状态选择事件处理 >> "%reportFile%"
    echo ❌ 未找到推仓状态选择事件处理 - 这可能是问题所在！
)

echo. >> "%reportFile%"
echo **检查推仓状态更新逻辑**： >> "%reportFile%"
findstr /n /i "UpdatePushTierStatus\|_editedConfig\.PushTier.*Status" "Views\ContractConfigEditDialog.xaml.cs" > temp_push_update.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到推仓状态更新逻辑 >> "%reportFile%"
    echo ```csharp >> "%reportFile%"
    type temp_push_update.txt | head -10 >> "%reportFile%" 2>nul
    echo ``` >> "%reportFile%"
    echo ✅ 找到推仓状态更新逻辑
) else (
    echo ❌ 未找到推仓状态更新逻辑 >> "%reportFile%"
    echo ❌ 未找到推仓状态更新逻辑 - 这是关键问题！
)

echo.
echo ### 3. 状态同步时机检查 >> "%reportFile%"
echo. >> "%reportFile%"

echo **检查保存按钮点击处理**： >> "%reportFile%"
findstr /n /i "SaveButton.*Click\|保存.*Click" "Views\ContractConfigEditDialog.xaml.cs" > temp_save_click.txt 2>nul
if %errorlevel% equ 0 (
    echo ✅ 找到保存按钮点击处理 >> "%reportFile%"
    echo ✅ 找到保存按钮点击处理
) else (
    echo ❌ 未找到保存按钮点击处理 >> "%reportFile%"
    echo ❌ 未找到保存按钮点击处理
)

echo.
echo ## 🔧 紧急修复方案 >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 方案1：增加UI状态同步验证 >> "%reportFile%"
echo. >> "%reportFile%"
echo 在保存按钮点击时，强制同步所有UI控件状态到 _editedConfig： >> "%reportFile%"
echo. >> "%reportFile%"
echo ```csharp >> "%reportFile%"
echo // 🔧 【紧急修复】保存前强制同步推仓状态 >> "%reportFile%"
echo private void SyncPushTierStatusFromUI() >> "%reportFile%"
echo { >> "%reportFile%"
echo     try >> "%reportFile%"
echo     { >> "%reportFile%"
echo         // 同步推仓阶梯1-4的状态 >> "%reportFile%"
echo         if (_pushTier1StatusComboBox?.SelectedItem is ComboBoxItem item1) >> "%reportFile%"
echo         { >> "%reportFile%"
echo             _editedConfig.PushTier1Status = item1.Tag?.ToString() ?? "-"; >> "%reportFile%"
echo             _logger?.LogCritical($"🔧【强制同步】推仓T1状态: {_editedConfig.PushTier1Status}"); >> "%reportFile%"
echo         } >> "%reportFile%"
echo         // ... 类似处理T2, T3, T4 >> "%reportFile%"
echo     } >> "%reportFile%"
echo     catch (Exception ex) >> "%reportFile%"
echo     { >> "%reportFile%"
echo         _logger?.LogError(ex, "同步推仓状态失败"); >> "%reportFile%"
echo     } >> "%reportFile%"
echo } >> "%reportFile%"
echo ``` >> "%reportFile%"
echo. >> "%reportFile%"

echo ### 方案2：增加实时状态验证 >> "%reportFile%"
echo. >> "%reportFile%"
echo 在保存前详细记录UI控件状态和 _editedConfig 状态的对比： >> "%reportFile%"
echo. >> "%reportFile%"
echo ```csharp >> "%reportFile%"
echo // 🔧 【调试验证】对比UI和配置状态 >> "%reportFile%"
echo private void VerifyUIConfigSync() >> "%reportFile%"
echo { >> "%reportFile%"
echo     _logger?.LogCritical("🔍【状态验证】开始对比UI和配置状态:"); >> "%reportFile%"
echo     >> "%reportFile%"
echo     // 检查推仓状态 >> "%reportFile%"
echo     var uiT1 = _pushTier1StatusComboBox?.SelectedItem?.Tag?.ToString() ?? "未知"; >> "%reportFile%"
echo     var configT1 = _editedConfig.PushTier1Status ?? "未知"; >> "%reportFile%"
echo     _logger?.LogCritical($"🔍 推仓T1: UI={uiT1}, Config={configT1}, 同步={uiT1 == configT1}"); >> "%reportFile%"
echo     >> "%reportFile%"
echo     // ... 类似检查其他阶梯 >> "%reportFile%"
echo } >> "%reportFile%"
echo ``` >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 🚨 立即行动建议 >> "%reportFile%"
echo. >> "%reportFile%"

echo 1. **添加状态同步日志**：在保存按钮点击时添加详细的状态同步日志 >> "%reportFile%"
echo 2. **强制UI同步**：在保存前强制同步所有UI控件状态 >> "%reportFile%"
echo 3. **验证状态一致性**：保存前验证UI状态与配置状态是否一致 >> "%reportFile%"
echo. >> "%reportFile%"

echo ## 🧪 测试验证步骤 >> "%reportFile%"
echo. >> "%reportFile%"

echo 1. 修改推仓状态后，**不要立即保存** >> "%reportFile%"
echo 2. 先查看日志中是否有状态变化记录 >> "%reportFile%"
echo 3. 如果没有状态变化记录，说明UI事件处理有问题 >> "%reportFile%"
echo 4. 如果有状态变化但保存无效，说明保存逻辑有问题 >> "%reportFile%"
echo. >> "%reportFile%"

:: 清理临时文件
del temp_*.txt 2>nul

echo.
echo ✅ 诊断完成！
echo.
echo 📋 详细诊断报告已保存到: %reportFile%
echo.
echo 🔍 关键发现：
echo   缺少推仓状态修改的保存过程日志
echo   可能是UI控件状态同步问题
echo.
echo 🚨 紧急测试：
echo   1. 请修改一个推仓状态
echo   2. 观察是否出现类似这样的日志：
echo      🔥【变化跟踪】推仓: T1=√, T2=-, T3=-, T4=-
echo   3. 如果没有，说明UI状态没有同步到配置
echo.

pause 