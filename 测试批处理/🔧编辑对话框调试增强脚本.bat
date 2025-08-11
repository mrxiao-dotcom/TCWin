@echo off
echo ==========================================
echo 编辑对话框调试增强脚本
echo ==========================================
echo.

echo 【问题】保存逻辑没有被触发
echo 从日志可以看出：
echo - ✅ 编辑对话框打开：🖱️ 双击编辑合约配置: BTCUSDT LONG
echo - ❌ 保存逻辑缺失：没有 🔧 开始更新合约状态: BTCUSDT_LONG
echo - ❌ 文件未更新：executionState 仍然是 0
echo.

echo 【可能原因】
echo 1. 编辑对话框UI问题 - 状态下拉框没有正确选项
echo 2. 保存按钮没有正确绑定事件
echo 3. SaveContractConfigToFile方法有异常
echo 4. 用户操作步骤不正确
echo.

echo 【调试步骤】
echo.
echo 步骤1: 编译最新代码并检查是否有编译错误
dotnet build BinanceFuturesTrader.csproj --configuration Release --verbosity normal
if %errorlevel% neq 0 (
    echo ❌ 编译失败，请先解决编译错误
    pause
    exit /b 1
)
echo ✅ 编译成功
echo.

echo 步骤2: 创建测试指导
echo.
echo 🔍 详细测试步骤（请严格按照执行）：
echo.
echo 1. 启动程序
echo 2. 点击"启动盯盘面板"
echo 3. 双击 BTCUSDT LONG 行（应该看到：🖱️ 双击编辑合约配置）
echo 4. 编辑对话框应该弹出
echo.
echo 🎯 关键操作（请仔细确认）：
echo 5. 在编辑对话框中找到"保本状态"字段
echo 6. 确认当前显示是 "-" 或 "未触发"
echo 7. 点击保本状态下拉框
echo 8. 选择 "√" 或 "已执行" 选项
echo 9. 确认下拉框显示已变为 "√"
echo 10. 点击"保存"按钮（不是取消）
echo 11. 应该弹出"保存成功"的消息框
echo 12. 点击消息框的"确定"按钮
echo.
echo ⚠️ 关键检查点：
echo - 编辑对话框是否真的弹出了？
echo - 保本状态下拉框是否有 "√" 选项？
echo - 是否真的修改了状态？
echo - 是否点击了"保存"而不是"取消"？
echo - 是否看到了"保存成功"消息框？
echo.

echo 步骤3: 预期的成功日志序列
echo.
echo 如果操作正确，您应该看到以下日志（按顺序）：
echo.
echo A. 编辑对话框保存开始：
echo    🔧 开始更新合约状态: BTCUSDT_LONG
echo    🔧 原始合约名: BTCUSDT LONG
echo    🔧 标准化合约键: BTCUSDT_LONG
echo    🔧 待更新状态: 保本=√
echo    ✅ 保本状态更新为executed
echo.
echo B. 状态服务处理：
echo    🔍【状态更新开始】BTCUSDT_LONG BreakEven_null = True
echo    📊 保本状态更新: 更新前ExecutionState=0(NotTriggered)
echo    🎯 传入参数: isSuccess=True
echo    🔄 设置ExecutionState: 2(Executed)
echo    📊 保本状态更新: 更新后ExecutionState=2(Executed)
echo.
echo C. 文件保存：
echo    📋 合约 BTCUSDT_LONG: 保本ExecutionState=2(Executed)
echo    ⚡ 发现非默认状态: BTCUSDT_LONG - Executed
echo    📊 JSON中发现executionState值: 2
echo    ✅ 文件保存完成
echo.

echo 步骤4: 问题排查指南
echo.
echo 如果没有看到上述日志，请检查：
echo.
echo A. 如果编辑对话框没有弹出：
echo    - 检查双击事件是否正确
echo    - 查看是否有异常日志
echo.
echo B. 如果对话框弹出但没有保本状态下拉框：
echo    - 编辑对话框UI设计有问题
echo    - 需要检查XAML文件
echo.
echo C. 如果有下拉框但没有"√"选项：
echo    - CreateStatusComboBox方法有问题
echo    - 选项没有正确添加
echo.
echo D. 如果选择了"√"但点击保存没有反应：
echo    - SaveButton_Click事件没有绑定
echo    - SaveContractConfigToFile方法有异常
echo.
echo E. 如果看到"保存成功"但没有更新日志：
echo    - SaveContractConfigToFile方法的状态检查逻辑有问题
echo    - 状态值没有正确传递
echo.

echo 步骤5: 手动验证文件变化
echo.
echo 测试完成后，立即检查文件内容：
for /f "tokens=*" %%a in ('dir "%APPDATA%\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do (
    echo 文件路径: %%a
    echo 检查命令: findstr /n -A 10 "breakEvenConfig" "%%a"
    echo 查找executionState: findstr /n "executionState.*[^0]" "%%a"
)
echo.

echo 步骤6: 如果问题仍然存在
echo.
echo 如果按照上述步骤操作仍然没有看到预期日志，请：
echo 1. 截图编辑对话框的界面
echo 2. 记录具体的操作步骤
echo 3. 复制完整的日志输出
echo 4. 检查是否有任何错误或异常消息
echo.

echo ==========================================
echo 调试脚本准备完成
echo ==========================================
echo.
echo 请按照上述步骤仔细测试，特别注意确认每个操作步骤！
echo.
pause 