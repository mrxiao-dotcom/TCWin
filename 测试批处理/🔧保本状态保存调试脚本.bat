@echo off
echo ==========================================
echo 保本状态保存问题深度调试脚本
echo ==========================================
echo.

echo 【问题描述】
echo 用户双击编辑保本状态为"已执行"，UI显示正确，但文件中executionState仍然是0
echo 预期：executionState应该变为2
echo.

echo 【步骤1】检查当前状态文件内容
echo 查找所有状态文件...
for /f "tokens=*" %%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do (
    echo.
    echo 找到状态文件: %%a
    echo ----------------------------------------
    echo 检查文件修改时间:
    forfiles /p "%%~dpa" /m "%%~nxa" /c "cmd /c echo 文件修改时间: @fdate @ftime"
    echo.
    echo 检查breakEvenConfig部分的executionState值:
    findstr /n -A 5 -B 2 "breakEvenConfig" "%%a" | findstr /n -A 3 -B 1 "executionState"
    echo.
    echo 完整的breakEvenConfig内容:
    findstr /n -A 15 "breakEvenConfig" "%%a"
    echo ----------------------------------------
)

echo.
echo 【步骤2】分析代码流程
echo.
echo 保存流程应该是：
echo 1. 用户选择"已执行" (✓符号)
echo 2. ContractConfigEditDialog.SaveContractConfigToFile()
echo 3. stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, true, 0, "手动设置为executed")
echo 4. ContractMonitoringStateGenerator.UpdateExecutionStatus()
echo 5. state.BreakEvenConfig.ExecutionState = ExecutionState.Executed (应该是2)
echo 6. SaveMonitoringStates() 保存到文件
echo.

echo 【步骤3】编译代码并测试
echo 正在编译...
dotnet build BinanceFuturesTrader.csproj --configuration Release --verbosity quiet
if %errorlevel% neq 0 (
    echo ❌ 编译失败
    pause
    exit /b 1
)
echo ✅ 编译成功

echo.
echo 【步骤4】创建测试验证方案
echo.
echo 测试步骤：
echo 1. 备份当前状态文件
echo 2. 运行程序
echo 3. 双击编辑合约配置
echo 4. 将保本状态改为"已执行"
echo 5. 保存并关闭
echo 6. 立即检查文件内容变化
echo.

echo 【步骤5】创建文件监控脚本
echo 将在后台监控状态文件的变化...

rem 创建文件监控脚本
echo @echo off > monitor_state_file.bat
echo echo 开始监控状态文件变化... >> monitor_state_file.bat
echo for /f "tokens=*" %%%%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^^^>nul') do ( >> monitor_state_file.bat
echo   echo 监控文件: %%%%a >> monitor_state_file.bat
echo   :MONITOR_LOOP >> monitor_state_file.bat
echo   timeout /t 2 /nobreak ^>nul >> monitor_state_file.bat
echo   echo [%%%%TIME%%%%] 检查文件变化... >> monitor_state_file.bat
echo   findstr /n "executionState.*:" "%%%%a" 2^^^>nul ^| findstr /v "null" >> monitor_state_file.bat
echo   goto MONITOR_LOOP >> monitor_state_file.bat
echo ^) >> monitor_state_file.bat

echo.
echo 【步骤6】检查日志输出
echo 确保程序运行时查看以下关键日志：
echo.
echo 关键日志标识：
echo 🔧 开始更新合约状态: [合约键]
echo 🔧 待更新状态: 保本=✓
echo ✅ 保本状态更新为executed
echo 🔍【状态更新开始】[合约键] BreakEven_null = True
echo 📊 保本状态更新: 更新前=[原状态]
echo 📊 保本状态更新: 更新后=[新状态]
echo 💾 开始保存到文件...
echo ✅ 文件保存完成
echo ✅ 验证结果 - 保本状态: True
echo.

echo 【步骤7】可能的问题原因分析
echo.
echo A. JSON序列化问题：
echo    - ExecutionState枚举值没有正确序列化
echo    - 可能序列化为字符串而不是数字
echo.
echo B. 文件保存时机问题：
echo    - 可能被其他进程覆盖
echo    - 文件锁定或权限问题
echo.
echo C. 状态更新逻辑问题：
echo    - UpdateExecutionStatus参数传递错误
echo    - isSuccess参数为false
echo.
echo D. 枚举转换问题：
echo    - ExecutionState.Executed != 2
echo    - 类型转换异常
echo.

echo 【建议的调试步骤】
echo 1. 运行程序并启用详细日志
echo 2. 执行编辑操作
echo 3. 检查日志中的关键信息
echo 4. 立即检查文件内容
echo 5. 如果文件未更新，检查是否有异常或错误日志
echo.

echo ==========================================
echo 调试脚本准备完成
echo ==========================================
echo.
echo 下一步：
echo 1. 运行 monitor_state_file.bat 监控文件变化
echo 2. 启动程序进行测试
echo 3. 观察日志输出和文件变化
echo 4. 如果问题依然存在，检查JSON序列化设置
echo.
pause 