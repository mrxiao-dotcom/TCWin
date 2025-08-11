@echo off
chcp 65001 > nul
echo.
echo ============================================
echo 🔧 多重超时保护测试脚本
echo ============================================
echo.
echo 📋 测试目标：验证第二次调用不再卡死
echo.
echo 🎯 问题分析：
echo    - 第一次调用成功完成（有完整日志）
echo    - 第二次调用卡死在 [PERFORM-16-3] 等待API调用或超时...
echo    - 第二次调用的超时机制失效
echo.
echo 🔧 修复方案：
echo    1. 添加服务状态检查和重置
echo    2. 多重超时保护（30秒+35秒）
echo    3. 独立的超时检查任务
echo    4. 移除可能有问题的CancellationToken
echo.
echo ⚠️  重要说明：
echo    - 现在有两层超时保护：30秒和35秒
echo    - 每次调用前会检查和重置服务状态
echo    - 强制超时任务独立运行，不会被卡死
echo.
echo ============================================
echo 🚀 测试步骤
echo ============================================
echo.
echo 1. 启动程序并导航到自动盯盘面板
echo.
echo 2. 第一次点击"启动盯盘"按钮
echo.
echo 3. 等待30秒超时，应该弹出错误对话框
echo.
echo 4. 点击错误对话框的"确定"按钮
echo.
echo 5. 再次点击"启动盯盘"按钮（关键测试）
echo.
echo 6. 等待最多35秒，观察是否有超时保护
echo.
echo 7. 检查是否有完整的诊断日志
echo.
echo ============================================
echo 📊 关键诊断点
echo ============================================
echo.
echo 🔍 第一次调用预期日志：
echo    [PERFORM-15] 即将调用 _autoMonitorService.StartMonitoringAsync
echo    [PERFORM-15-1] 检查服务状态
echo    [PERFORM-16-3] 等待API调用或超时...
echo    [PERFORM-16-TIMEOUT] API调用超时(30秒)
echo    [UI-PROCESS-09] 在UI线程显示MessageBox
echo.
echo 🔍 第二次调用预期日志：
echo    [PERFORM-15-1] 检查服务状态
echo    [PERFORM-15-2] 服务仍在运行，强制停止    ← 关键
echo    [PERFORM-15-3] 服务已停止
echo    [PERFORM-16-3] 等待API调用或超时...
echo    [PERFORM-16-TIMEOUT] API调用超时(30秒)     ← 关键
echo    或
echo    [PERFORM-16-FORCE-TIMEOUT] 强制超时(35秒)  ← 备用
echo.
echo ============================================
echo 📋 测试检查清单
echo ============================================
echo.
echo 第一次调用：
echo □ 1. 30秒后是否弹出错误对话框？
echo □ 2. 是否有完整的UI处理日志？
echo □ 3. 按钮状态是否恢复？
echo.
echo 第二次调用：
echo □ 4. 是否有服务状态检查日志？
echo □ 5. 是否有服务重置日志？
echo □ 6. 30秒后是否再次弹出错误对话框？
echo □ 7. 是否有[PERFORM-16-TIMEOUT]日志？
echo □ 8. 界面是否保持响应？
echo.
echo ============================================
echo 🟢 成功标准
echo ============================================
echo.
echo ✅ 第二次调用成功标准：
echo    - 有服务状态检查和重置日志
echo    - 30秒或35秒后有超时日志
echo    - 弹出错误对话框
echo    - 界面保持响应
echo    - 可以继续第三次、第四次调用
echo.
echo ============================================
echo 🔴 失败标准
echo ============================================
echo.
echo ❌ 如果第二次调用仍然：
echo    - 卡死在[PERFORM-16-3]没有后续日志
echo    - 没有超时保护触发
echo    - 界面完全无响应
echo    - 不能继续操作
echo.
echo ============================================
echo 🆘 进一步修复方案
echo ============================================
echo.
echo 如果仍然卡死，可能需要：
echo    1. 完全重写API调用机制
echo    2. 使用Process.Start分离进程
echo    3. 添加硬件级别的watchdog
echo    4. 检查AutoMonitorService内部实现
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
    echo 🔍 查看最近的服务状态日志...
    echo.
    echo ----------------------------------------
    echo 📋 服务状态相关日志：
    findstr /C:"PERFORM-15" emergency_log.txt | tail -20
    echo.
    echo 📋 超时相关日志：
    findstr /C:"PERFORM-16-TIMEOUT\|PERFORM-16-FORCE-TIMEOUT" emergency_log.txt | tail -10
    echo.
    echo 📋 最近的UI处理日志：
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
echo 🟢 如果第二次调用成功：
echo    说明服务状态重置和多重超时保护有效
echo    可以正常使用系统了
echo.
echo 🔴 如果第二次调用仍然卡死：
echo    说明问题更深层，可能是：
echo    1. AutoMonitorService内部死锁
echo    2. 异步任务堆积
echo    3. 资源泄漏
echo    4. 需要重新设计整个调用机制
echo.
echo ⚠️  请提供：
echo    1. 两次调用是否都有完整日志？
echo    2. 第二次调用卡死在哪个诊断点？
echo    3. 是否有服务重置相关日志？
echo    4. 界面是否能继续响应？
echo.
echo 感谢您的耐心测试！
echo.
pause 