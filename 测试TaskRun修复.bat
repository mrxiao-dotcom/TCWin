@echo off
chcp 65001 > nul
echo.
echo ============================================
echo 🔧 Task.Run Await 修复测试脚本
echo ============================================
echo.
echo 📋 测试目标：验证Task.Run await卡死修复效果
echo.
echo 🎯 发现的问题：
echo    - Task.Run内部正常完成 [TASK-02]
echo    - 但await Task.Run卡死，没有[HANDLE-07]日志
echo    - 可能是取消令牌timeoutCts.Token导致的
echo.
echo 🔧 修复方案：
echo    - 移除Task.Run的取消令牌参数
echo    - 添加HANDLE-06-1和HANDLE-06-2诊断点
echo    - 确保Task.Run能正常返回结果
echo.
echo ⚠️  重要说明：
echo    - API超时控制(30秒)仍然存在
echo    - 只是移除了Task.Run外层的取消令牌
echo    - 预期能看到完整的处理流程
echo.
echo ============================================
echo 🚀 开始测试
echo ============================================
echo.
echo 1. 启动程序并导航到自动盯盘面板
echo.
echo 2. 确认按钮状态为"启动盯盘"
echo.
echo 3. 点击"启动盯盘"按钮
echo.
echo 4. 等待30秒API超时
echo.
echo 5. 观察是否出现错误对话框
echo.
echo 6. 检查是否有完整的诊断日志
echo.
echo ============================================
echo 📊 关键诊断点顺序
echo ============================================
echo.
echo 🔍 预期日志顺序：
echo    [HANDLE-06] 即将执行Task.Run
echo    [HANDLE-06-1] 开始await Task.Run         ← 新增
echo    [TASK-01] Task.Run内部开始执行
echo    [PERFORM-16-TIMEOUT] API调用超时(30秒)
echo    [TASK-02] PerformStartMonitoringAsync完成
echo    [HANDLE-06-2] await Task.Run完成         ← 新增
echo    [HANDLE-07] Task.Run执行完成             ← 关键！
echo    [UI-PROCESS-01] 开始处理Task.Run结果
echo    [UI-PROCESS-03] 处理失败结果
echo    [UI-PROCESS-08] MessageBox.Show开始调用
echo    [UI-PROCESS-10] MessageBox显示完成
echo.
echo ============================================
echo 📋 成功标准
echo ============================================
echo.
echo ✅ 必须看到的日志：
echo    □ [HANDLE-06-1] 开始await Task.Run
echo    □ [TASK-02] PerformStartMonitoringAsync完成
echo    □ [HANDLE-06-2] await Task.Run完成
echo    □ [HANDLE-07] Task.Run执行完成
echo    □ [UI-PROCESS-01] 开始处理Task.Run结果
echo    □ 错误对话框正常显示
echo    □ 按钮状态恢复为"启动盯盘"
echo.
echo ============================================
echo 🔴 失败标准
echo ============================================
echo.
echo ❌ 如果仍然缺少以下日志：
echo    - [HANDLE-06-2] await Task.Run完成
echo    - [HANDLE-07] Task.Run执行完成
echo    - [UI-PROCESS-XX] 相关日志
echo.
echo    说明问题仍然存在，需要进一步调查
echo.
echo ============================================
echo 🆘 进一步调试方案
echo ============================================
echo.
echo 如果修复后仍然卡死，可能原因：
echo    1. Task.Run内部异常传播有问题
echo    2. PerformStartMonitoringAsync的异常处理
echo    3. 其他线程同步问题
echo.
echo 下一步调试：
echo    1. 检查Task.Run异常处理机制
echo    2. 添加更多异常捕获诊断
echo    3. 考虑直接调用而不使用Task.Run
echo.
echo ============================================
echo ⏰ 开始测试，请按Enter键继续...
echo ============================================
pause
echo.
echo 🔍 测试完成，查看诊断日志...
echo.
if exist emergency_log.txt (
    echo 📄 emergency_log.txt 文件存在
    echo.
    echo 🔍 查看最近的Task.Run相关日志...
    echo.
    echo ----------------------------------------
    echo 📋 HANDLE-06相关日志：
    findstr /C:"HANDLE-06" emergency_log.txt | tail -10
    echo.
    echo 📋 TASK相关日志：
    findstr /C:"TASK-" emergency_log.txt | tail -10
    echo.
    echo 📋 UI-PROCESS相关日志：
    findstr /C:"UI-PROCESS" emergency_log.txt | tail -10
    echo ----------------------------------------
    echo.
) else (
    echo ❌ emergency_log.txt 文件不存在
    echo    请确保程序已启动并执行了启动盯盘操作
    echo.
)
echo.
echo ============================================
echo 🎯 结果分析
echo ============================================
echo.
echo 🟢 如果看到完整的日志链：
echo    表示Task.Run await修复成功！
echo    程序可以正常处理API超时并更新界面
echo.
echo 🔴 如果仍然缺少HANDLE-07日志：
echo    说明Task.Run await仍然有问题
echo    需要进一步调试异常处理机制
echo.
echo ⚠️  请报告你看到的最后一个诊断点编号
echo    以及是否弹出了错误对话框
echo.
echo 感谢您的测试！
echo.
pause 