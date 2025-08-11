@echo off
echo ==========================================
echo 界面状态和文件内容不同步问题调试脚本
echo ==========================================
echo.

echo 【问题描述】
echo 用户反映：文件中ExecutionState=2(已执行)，但界面显示不是勾号
echo 预期：文件中的状态应该在界面正确显示
echo.

echo 【步骤1】检查状态文件内容
echo 查找status文件并检查ExecutionState值...
for /f "tokens=*" %%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do (
    echo 找到状态文件: %%a
    echo 检查文件内容中的ExecutionState值:
    findstr /n "executionState" "%%a" 2>nul
    echo 检查breakEvenConfig部分:
    findstr /n -A 10 -B 2 "breakEvenConfig" "%%a" 2>nul
    echo.
)
echo.

echo 【步骤2】分析问题可能的原因
echo.
echo 根据代码分析，可能的问题原因：
echo 1. JSON反序列化时ExecutionState没有正确加载
echo 2. IsExecuted属性计算逻辑有问题  
echo 3. UI更新时机不对
echo 4. 数据类型转换问题
echo.

echo 【步骤3】验证ExecutionState枚举值
echo 当前枚举定义：
echo   NotTriggered = 0
echo   Executing = 1  
echo   Executed = 2
echo.
echo 如果文件中是2，应该对应Executed状态
echo IsExecuted属性应该返回true
echo 界面应该显示"√"
echo.

echo 【步骤4】编译最新代码进行测试
echo 正在编译项目...
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ❌ 编译失败，请先解决编译错误
    pause
    exit /b 1
)
echo ✅ 编译成功
echo.

echo 【步骤5】解决方案 - 确保界面始终从文件加载最新状态
echo.
echo 问题分析：
echo - PopulateConfigFromState方法使用 state.BreakEvenConfig.IsExecuted
echo - IsExecuted属性依赖于 ExecutionState == ExecutionState.Executed  
echo - 如果ExecutionState=2但IsExecuted返回false，说明枚举比较有问题
echo.
echo 建议的修复方案：
echo 1. 在PopulateConfigFromState中直接检查ExecutionState数值
echo 2. 添加详细的调试日志输出状态转换过程
echo 3. 确保每次打开面板都重新从文件读取
echo.

echo 【步骤6】创建修复补丁
echo 正在创建修复代码...

echo 【修复内容预览】
echo 将修改PopulateConfigFromState方法，改为直接检查ExecutionState值：
echo.
echo 修复前：
echo   config.BreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "√" : "-";
echo.
echo 修复后：  
echo   // 直接检查ExecutionState数值，确保正确显示
echo   var executionState = (int)state.BreakEvenConfig.ExecutionState;
echo   config.BreakEvenStatus = (executionState == 2) ? "√" : 
echo                           (executionState == 1) ? "⚡" : "-";
echo.

echo ==========================================
echo 调试脚本执行完成
echo ==========================================
echo.
echo 下一步操作：
echo 1. 检查上述状态文件内容
echo 2. 如果确认文件中ExecutionState=2但界面不显示勾号
echo 3. 运行修复脚本应用补丁
echo 4. 重新测试界面显示
echo.
pause 