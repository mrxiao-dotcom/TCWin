@echo off
chcp 65001
echo.
echo ====================================================
echo 🚨 测试直接调用修复 - 消除Task.Run死锁问题
echo ====================================================
echo.

echo 🔍 **关键修复内容：**
echo.
echo 1. **移除Task.Run包装**
echo    - 原: await Task.Run(async () => ...)
echo    - 新: await PerformStartMonitoringAsync().ConfigureAwait(false)
echo.
echo 2. **线程安全文件写入**
echo    - 原: File.AppendAllText(emergencyLogPath, ...)
echo    - 新: WriteEmergencyLog(...)
echo.
echo 3. **完整超时保护**
echo    - 30秒API调用超时
echo    - 35秒强制超时保护
echo    - 异常处理和友好提示
echo.
echo 4. **ConfigureAwait(false)**
echo    - 避免UI线程上下文死锁
echo    - 提高异步操作效率
echo.

echo ====================================================
echo 🚀 **测试步骤：**
echo ====================================================
echo.
echo 1. 点击"启动盯盘"按钮
echo 2. 观察是否出现：
echo    - [HANDLE-06-DIRECT] 直接调用PerformStartMonitoringAsync，避免Task.Run死锁
echo    - [HANDLE-06-2] 直接调用完成
echo    - [HANDLE-07] 调用执行完成
echo.
echo 3. 如果API超时，应该在30秒内返回错误
echo 4. 界面应该保持响应，不再无限卡死
echo.

echo ====================================================
echo 🎯 **预期结果：**
echo ====================================================
echo.
echo ✅ 正常情况：
echo    - 30秒内完成启动或超时
echo    - 界面保持响应
echo    - 所有诊断日志正常显示
echo.
echo ❌ 异常情况：
echo    - 超时后显示友好错误提示
echo    - 不再出现无限卡死
echo    - 日志文件访问冲突已解决
echo.

echo ====================================================
echo 🔧 **如果仍有问题：**
echo ====================================================
echo.
echo 1. 检查emergency_log.txt文件是否有文件锁定
echo 2. 查看普通日志是否有错误信息
echo 3. 确认API配置是否正确
echo 4. 重启应用程序测试
echo.

echo ====================================================
echo 💡 **关键改进：**
echo ====================================================
echo.
echo 1. **彻底消除Task.Run死锁**
echo    - 直接在UI线程调用异步方法
echo    - 使用ConfigureAwait(false)避免上下文死锁
echo.
echo 2. **线程安全日志系统**
echo    - 使用互斥锁保护文件访问
echo    - 异常容错机制
echo.
echo 3. **完整超时控制**
echo    - 多层超时保护
echo    - 友好的错误提示
echo.

echo ====================================================
echo 🚨 **请立即测试并反馈结果！**
echo ====================================================
echo.
pause 