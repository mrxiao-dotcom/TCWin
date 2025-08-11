@echo off
echo ==========================================
echo 状态保存完整验证脚本
echo ==========================================
echo.

echo 【目标】验证保本状态从UI编辑到文件保存的完整流程
echo.

echo 【步骤1】编译最新代码
dotnet build BinanceFuturesTrader.csproj --configuration Release --verbosity quiet
if %errorlevel% neq 0 (
    echo ❌ 编译失败
    pause
    exit /b 1
)
echo ✅ 编译成功

echo.
echo 【步骤2】备份当前状态文件
echo 查找并备份状态文件...
for /f "tokens=*" %%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do (
    echo 找到状态文件: %%a
    copy "%%a" "%%a.backup_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%" >nul 2>&1
    echo ✅ 已备份状态文件
    
    echo.
    echo 当前文件内容中的executionState值:
    findstr /n "executionState" "%%a" 2>nul
    echo.
)

echo.
echo 【步骤3】监控流程说明
echo.
echo 🔍 关键日志监控点：
echo.
echo 1. 编辑对话框保存阶段：
echo    🔧 开始更新合约状态: [合约键]
echo    ✅ 保本状态更新为executed
echo.
echo 2. ContractMonitoringStateService阶段：
echo    🔍【状态更新开始】[合约键] BreakEven_null = True
echo    📋 合约 [合约键]: 保本ExecutionState=[数值]([枚举值])
echo    ⚡ 发现非默认状态: [合约键] - [状态]
echo.
echo 3. ContractMonitoringStateGenerator阶段：
echo    📊 保本状态更新: 更新前ExecutionState=[数值]
echo    🎯 传入参数: isSuccess=True
echo    🔄 设置ExecutionState: 2(Executed)
echo    📊 保本状态更新: 更新后ExecutionState=2
echo.
echo 4. JSON序列化阶段：
echo    📊 JSON中发现executionState值: 2
echo    ✅ 保存后文件状态: 存在=True
echo.
echo 5. 重新加载阶段：
echo    🔍 从文件读取保本状态: ExecutionState=2(Executed)
echo    🔄 保本状态转换: ExecutionState 2 → UI显示 '√'
echo.

echo 【测试流程】
echo 1. 启动程序
echo 2. 打开"启动盯盘面板"
echo 3. 双击合约配置行
echo 4. 修改保本状态为"已执行"(√)
echo 5. 点击保存
echo 6. 观察日志输出
echo 7. 立即检查文件内容
echo.

echo 【预期结果】
echo - 日志应显示ExecutionState从0变为2
echo - JSON序列化应显示executionState值为2
echo - 文件中应包含 "executionState": 2
echo - UI重新加载应正确显示√符号
echo.

echo 【问题诊断】
echo 如果executionState仍然是0，检查以下日志：
echo.
echo A. 如果没有看到"🔧 开始更新合约状态"：
echo    - 编辑对话框保存没有被调用
echo    - 检查双击事件和保存按钮
echo.
echo B. 如果看到"开始更新"但没有"设置ExecutionState: 2"：
echo    - UpdateExecutionStatus参数可能错误
echo    - 检查isSuccess参数是否为true
echo.
echo C. 如果看到"设置ExecutionState: 2"但JSON中是0：
echo    - 状态更新后被其他地方覆盖
echo    - 检查是否有重新加载或刷新操作
echo.
echo D. 如果JSON中是2但文件中是0：
echo    - 文件保存失败或被其他进程覆盖
echo    - 检查文件权限和锁定状态
echo.

echo 【额外验证】
echo 完成测试后，立即运行以下命令验证文件内容：
echo.
for /f "tokens=*" %%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do (
    echo findstr /n "executionState" "%%a"
)
echo.

echo ==========================================
echo 验证脚本准备完成
echo ==========================================
echo.
echo 请按照上述步骤进行测试，并仔细观察日志输出！
echo.
pause 