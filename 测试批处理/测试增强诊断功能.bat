@echo off
chcp 65001 >nul
echo ============================
echo 🔍 测试增强诊断功能
echo ============================

echo.
echo 📋 测试目标：
echo   • 验证超早期诊断系统是否正常工作
echo   • 精确定位界面卡死的位置
echo   • 测试Logger组件的工作状态
echo.

echo 🔧 诊断点说明：
echo.
echo 📱 UI层诊断：
echo   [EMERGENCY-01~04] 按钮点击基础检查
echo   [EMERGENCY-05~09] Logger组件状态检查
echo   [EMERGENCY-10~15] 按钮处理主逻辑
echo.
echo 🔧 处理层诊断：
echo   [HANDLE-01~07] HandleStartMonitoring方法
echo   [TASK-01~02] Task.Run执行过程
echo.
echo 🎯 核心层诊断：
echo   [PERFORM-01~07] 配置获取和验证
echo   [PERFORM-15~17] StartMonitoringAsync调用
echo.
echo 🚨 关键检查点：
echo   [PERFORM-16] 开始调用StartMonitoringAsync ← 最可能卡死的位置
echo   [PERFORM-17] StartMonitoringAsync调用完成 ← 如果看不到此项说明API卡死
echo.

echo 🚀 测试步骤：
echo   1. 启动TCWin程序
echo   2. 删除旧的诊断文件（如果存在）
echo   3. 点击"启动盯盘"按钮
echo   4. 立即查看诊断文件内容
echo.

echo ⏳ 1. 清理旧的诊断文件...
if exist "emergency_log.txt" (
    del "emergency_log.txt"
    echo ✅ 已删除旧的 emergency_log.txt
) else (
    echo ℹ️  未找到旧的 emergency_log.txt
)

if exist "emergency_error.txt" (
    del "emergency_error.txt"
    echo ✅ 已删除旧的 emergency_error.txt
) else (
    echo ℹ️  未找到旧的 emergency_error.txt
)

echo.
echo 🎯 请按照以下步骤测试：
echo.
echo 📱 步骤1: 启动程序
echo   • 打开TCWin程序
echo   • 选择账户
echo   • 导航到自动盯盘界面
echo.
echo 🔘 步骤2: 点击测试
echo   • 点击"启动盯盘"按钮
echo   • 注意观察按钮状态变化
echo   • 如果界面卡死，不要强制关闭
echo.
echo 📄 步骤3: 查看诊断结果
echo   • 立即检查项目根目录
echo   • 查看 emergency_log.txt 文件
echo   • 查看 emergency_error.txt 文件（如果存在）
echo.
echo 🔍 诊断结果分析：
echo.
echo ✅ 如果看到 [EMERGENCY-14] 按钮点击处理完成
echo   → 说明按钮处理成功，问题可能在HandleStartMonitoring方法
echo.
echo ⚠️  如果最后一条是 [EMERGENCY-07] 即将调用Logger.LogCritical
echo   → 说明Logger调用时卡死，可能是Logger配置问题
echo.
echo ❌ 如果最后一条是 [EMERGENCY-09] Logger.LogCritical调用失败
echo   → 说明Logger调用抛出异常，需要检查Logger初始化
echo.
echo 🔧 如果最后一条是 [EMERGENCY-13] 执行启动盯盘流程
echo   → 说明问题在HandleStartMonitoring方法内部
echo.
echo 📊 完整的成功流程应该包含：
echo   [EMERGENCY-01] 按钮点击事件被触发
echo   [EMERGENCY-02] UI线程状态: True
echo   [EMERGENCY-03] 服务为null: False
echo   [EMERGENCY-04] 服务状态: False
echo   [EMERGENCY-05] Logger为null: False
echo   [EMERGENCY-06] Logger类型: [具体类型]
echo   [EMERGENCY-07] 即将调用Logger.LogCritical
echo   [EMERGENCY-08] Logger.LogCritical调用成功
echo   [EMERGENCY-10] 开始执行主逻辑
echo   [EMERGENCY-11] 服务运行状态: False
echo   [EMERGENCY-13] 执行启动盯盘流程
echo   [EMERGENCY-14] 按钮点击处理完成
echo.
echo 🎯 测试完成后，请将诊断文件内容发送给技术支持：
echo   • emergency_log.txt 的完整内容
echo   • emergency_error.txt 的内容（如果存在）
echo   • 界面的具体表现（是否卡死、按钮状态等）
echo.
echo ⏸️  现在请切换到程序进行测试...
echo ⏸️  测试完成后按任意键继续查看诊断结果
pause

echo.
echo 📄 查看诊断结果：
echo.

if exist "emergency_log.txt" (
    echo ✅ 找到 emergency_log.txt，内容如下：
    echo ========================================
    type "emergency_log.txt"
    echo ========================================
    echo.
    
    echo 📊 诊断结果分析：
    findstr /C:"[PERFORM-17]" "emergency_log.txt" >nul
    if %errorlevel% == 0 (
        echo ✅ 找到 [PERFORM-17] - StartMonitoringAsync调用完成
        echo 💡 API调用正常，问题可能在后续处理中
        echo.
        echo 📈 检查API调用耗时：
        findstr /C:"[PERFORM-17]" "emergency_log.txt"
    ) else (
        findstr /C:"[PERFORM-16]" "emergency_log.txt" >nul
        if %errorlevel% == 0 (
            echo 🚨 找到 [PERFORM-16] 但没有 [PERFORM-17] - API调用卡死
            echo 💡 问题确认：AutoMonitorService.StartMonitoringAsync方法卡死
            echo 💡 可能原因：
            echo   • API网络连接超时
            echo   • API认证失败导致长时间等待
            echo   • 内部死锁或无限循环
            echo   • 依赖服务未响应
        ) else (
            findstr /C:"[TASK-01]" "emergency_log.txt" >nul
            if %errorlevel% == 0 (
                echo ⚠️  找到 [TASK-01] 但没有后续步骤
                echo 💡 问题在Task.Run内部，配置获取阶段可能卡死
            ) else (
                findstr /C:"[HANDLE-06]" "emergency_log.txt" >nul
                if %errorlevel% == 0 (
                    echo ⚠️  最后停在 [HANDLE-06] - Task.Run创建时卡死
                    echo 💡 可能是线程池问题或Task创建异常
                ) else (
                    echo 🔍 根据最后一条记录分析问题位置
                )
            )
        )
    )
) else (
    echo ❌ 未找到 emergency_log.txt
    echo 💡 可能的原因：
    echo   • 按钮点击事件未触发
    echo   • 文件写入权限问题
    echo   • 程序在诊断代码执行前就卡死
)

echo.
if exist "emergency_error.txt" (
    echo ⚠️  找到 emergency_error.txt，内容如下：
    echo ========================================
    type "emergency_error.txt"
    echo ========================================
    echo.
    echo 💡 存在emergency_error.txt说明连基本的文件写入都有问题
) else (
    echo ℹ️  未找到 emergency_error.txt（这是正常的）
)

echo.
echo 🔧 基于诊断结果的建议：
echo.
echo 如果问题在Logger：
echo   • 检查日志配置
echo   • 尝试以管理员身份运行
echo   • 检查磁盘空间
echo.
echo 如果问题在HandleStartMonitoring：
echo   • 问题在启动流程内部
echo   • 需要进一步诊断AutoMonitorService
echo   • 可能是网络或API问题
echo.
echo 如果完全没有诊断文件：
echo   • 检查文件权限
echo   • 检查防火墙设置
echo   • 尝试重启程序
echo.
echo 📞 请将以上诊断结果发送给技术支持以获得进一步帮助。
echo.
pause 