@echo off
chcp 65001 >nul
echo ============================
echo 🔧 测试API超时控制机制
echo ============================

echo.
echo 📋 测试目标：
echo   • 验证30秒API超时控制是否正常工作
echo   • 观察超时后的友好错误提示
echo   • 确认不会无限卡死
echo.

echo 🔧 新增的超时诊断点：
echo   [PERFORM-16-1] API超时控制已创建(30秒)
echo   [PERFORM-16-2] 开始超时包装的API调用
echo   [PERFORM-16-3] 等待API调用或超时...
echo   [PERFORM-16-TIMEOUT] API调用超时(30秒) ← 如果30秒后看到此项
echo   [PERFORM-17] StartMonitoringAsync调用完成 ← 如果正常完成
echo   [PERFORM-ERROR] 启动异常 ← 如果出现其他错误
echo   [PERFORM-TIMEOUT] 确认为超时错误，不重试 ← 超时处理确认
echo.

echo 🚀 测试步骤：
echo   1. 启动TCWin程序
echo   2. 删除旧的诊断文件
echo   3. 点击"启动盯盘"按钮
echo   4. 等待最多30秒观察结果
echo.

echo ⏳ 1. 清理旧的诊断文件...
if exist "emergency_log.txt" (
    del "emergency_log.txt"
    echo ✅ 已删除旧的 emergency_log.txt
) else (
    echo ℹ️  未找到旧的诊断文件
)

echo.
echo 🎯 现在请：
echo   1. 点击"启动盯盘"按钮
echo   2. 观察程序反应（最多等待30秒）
echo   3. 然后按任意键查看诊断结果
echo.
pause

echo.
echo 📊 检查诊断结果...

if exist "emergency_log.txt" (
    echo ✅ 找到诊断文件，分析结果：
    echo.
    echo 📄 完整诊断日志：
    echo ----------------------------------------
    type "emergency_log.txt"
    echo ----------------------------------------
    echo.
    
    echo 📊 关键结果分析：
    
    findstr /C:"[PERFORM-16-TIMEOUT]" "emergency_log.txt" >nul
    if %errorlevel% == 0 (
        echo 🚨 发现超时标记 - API调用超时控制正常工作！
        echo ✅ 超时机制验证：成功
        echo 💡 说明：AutoMonitorService.StartMonitoringAsync方法确实卡死了
        echo 💡 超时保护防止了程序无限卡死
        echo.
        echo 📈 超时详情：
        findstr /C:"[PERFORM-16" "emergency_log.txt"
        echo.
        echo 🔧 建议的修复方向：
        echo   • 检查AutoMonitorService内部的死锁或阻塞
        echo   • 验证网络连接和API配置
        echo   • 考虑添加更细粒度的超时控制
    ) else (
        findstr /C:"[PERFORM-17]" "emergency_log.txt" >nul
        if %errorlevel% == 0 (
            echo ✅ API调用成功完成！
            echo 💡 StartMonitoringAsync方法正常工作了
            echo.
            echo 📈 调用耗时：
            findstr /C:"[PERFORM-17]" "emergency_log.txt"
            echo.
            echo 🎉 问题已解决：
            echo   • API调用不再卡死
            echo   • 启动流程恢复正常
            echo   • 可以移除超时控制（可选）
        ) else (
            findstr /C:"[PERFORM-ERROR]" "emergency_log.txt" >nul
            if %errorlevel% == 0 (
                echo ❌ 发现其他启动错误
                echo 💡 不是超时问题，而是其他异常
                echo.
                echo 📈 错误详情：
                findstr /C:"[PERFORM-ERROR]" "emergency_log.txt"
                echo.
                echo 🔧 需要根据具体错误类型进行修复
            ) else (
                findstr /C:"[PERFORM-16-3]" "emergency_log.txt" >nul
                if %errorlevel% == 0 (
                    echo ⚠️  API调用仍在等待中，可能：
                    echo   • 调用时间超过了脚本等待时间
                    echo   • 调用正在缓慢执行但未完成
                    echo   • 超时机制本身有问题
                    echo.
                    echo 💡 建议继续等待或检查程序状态
                ) else (
                    echo 🔍 需要检查最后记录的诊断点
                    echo.
                    echo 📄 最后几条记录：
                    powershell -Command "Get-Content 'emergency_log.txt' -Tail 5"
                )
            )
        )
    )
) else (
    echo ❌ 未找到诊断文件
    echo 💡 可能原因：
    echo   • 程序未启动或崩溃
    echo   • 诊断代码未执行
    echo   • 文件路径问题
)

echo.
echo 📋 总结：
echo   此测试验证了30秒API超时控制机制
echo   如果看到超时标记，说明超时保护正常工作
echo   如果API正常完成，说明问题已解决
echo.

pause 