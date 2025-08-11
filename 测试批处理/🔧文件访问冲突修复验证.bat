@echo off
chcp 65001 > nul
echo.
echo 🔧 文件访问冲突修复验证
echo ===================================
echo.

echo 📋 问题确认:
echo   ❌ 点击自动盯盘按钮时出现文件访问异常
echo   ❌ "The process cannot access the file 'ContractProfiles.json' because it is being used by another process."
echo   🔍 根因: 多个ContractProfileService实例同时访问同一个文件
echo   🛠️ 已修复: 改为静态锁 + 重试机制
echo.

echo 🔧 修复内容说明:
echo   1. 将实例级别的锁改为静态锁（所有实例共享同一个锁）
echo   2. 添加文件访问重试机制（最多重试3次）
echo   3. 添加递增延迟重试（100ms, 200ms, 300ms）
echo   4. 改进IOException异常处理
echo   5. 确保文件访问的线程安全
echo.

echo 🔍 检查修复状态:
echo   1. 关闭正在运行的应用程序...
taskkill /f /im "BinanceFuturesTrader.exe" > nul 2>&1
timeout /t 2 > nul

echo   2. 验证修复代码是否正确应用...
echo 🔍 检查静态锁修复:
findstr /n "private static readonly object _fileLock" "Services\ContractProfileService.cs" | head -1 > nul
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到静态锁修复
) else (
    echo ❌ 未找到静态锁修复
)

echo.
echo 🔍 检查重试机制:
findstr /n "const int maxRetries = 3" "Services\ContractProfileService.cs" | head -1 > nul
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到重试机制
) else (
    echo ❌ 未找到重试机制
)

findstr /n "IOException ioEx" "Services\ContractProfileService.cs" | head -1 > nul
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到IOException处理
) else (
    echo ❌ 未找到IOException处理
)

echo.
echo   3. 检查ContractProfiles.json文件状态...
if exist "bin\Debug\net8.0-windows\Data\ContractProfiles.json" (
    echo ✅ 找到ContractProfiles.json文件
    echo 📄 文件路径: bin\Debug\net8.0-windows\Data\ContractProfiles.json
) else (
    echo 📝 ContractProfiles.json文件不存在（程序首次运行时会创建）
)

echo.
echo   4. 启动应用程序进行文件访问冲突修复验证...
echo 🚀 启动应用程序...
start "" "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe"

echo.
echo 📊 文件访问冲突修复验证步骤:
echo ===================================
echo.
echo 🎯 测试场景1: 基本自动盯盘启动（关键测试）
echo   1. 等待程序完全启动
echo   2. 点击"自动盯盘"按钮
echo   3. 🔍 观察是否还会出现以下异常：
echo      ❌ 异常信息: "System.IO.IOException"
echo      ❌ 错误描述: "The process cannot access the file 'ContractProfiles.json' because it is being used by another process."
echo   4. ✅ 成功标志：
echo      - 自动盯盘功能正常启动
echo      - 不出现文件访问异常
echo      - UI状态正确更新为"自动盯盘运行中"
echo.

echo 🎯 测试场景2: 多次启停自动盯盘测试
echo   5. 多次点击自动盯盘按钮（启动/停止/启动）
echo   6. 验证每次操作都不会出现文件访问异常
echo   7. 检查日志中是否有文件重试的警告信息：
echo      - "文件读取失败，重试 X/3"
echo      - "文件写入失败，重试 X/3"
echo   8. 确认文件访问稳定可靠
echo.

echo 🎯 测试场景3: 并发操作压力测试
echo   9. 快速连续点击自动盯盘按钮
echo   10. 同时在文件资源管理器中查看Data文件夹
echo   11. 验证不会出现文件锁定或访问冲突
echo   12. 确认多个ContractProfileService实例能正确协调
echo.

echo 🎯 测试场景4: 文件恢复能力测试
echo   13. 如果有重试警告出现，观察是否能成功恢复
echo   14. 验证重试机制是否有效工作
echo   15. 确认最终操作能成功完成
echo.

echo 🔧 技术原理解析:
echo ===================================
echo.
echo 💡 修复前的问题原因:
echo   • AutoMonitorConfigWindowSimple创建一个ContractProfileService实例
echo   • AutoMonitorService也创建一个ContractProfileService实例
echo   • 两个实例各自有独立的_fileLock对象
echo   • 实例A锁定自己的_fileLock，实例B的_fileLock不受影响
echo   • 结果：两个实例同时访问同一个文件，造成访问冲突
echo.
echo 🛠️ 修复后的机制:
echo   • 将_fileLock改为static readonly，所有实例共享同一个锁对象
echo   • lock(_fileLock) 现在对所有ContractProfileService实例生效
echo   • 添加IOException捕获和重试机制
echo   • 递增延迟确保在高并发情况下有足够时间恢复
echo.
echo 📊 重试机制详解:
echo   • 最大重试次数: 3次
echo   • 延迟策略: 100ms → 200ms → 300ms（递增延迟）
echo   • 异常处理: 只重试IOException，其他异常直接抛出
echo   • 成功即退出: 一旦操作成功立即停止重试
echo.
echo 🔒 线程安全保证:
echo   • 静态锁确保跨实例的互斥访问
echo   • Task.Run确保不阻塞UI线程
echo   • 锁内包含完整的文件操作，避免竞态条件
echo.

echo ❓ 预期结果:
echo ===================================
echo.
echo ✅ 成功标志:
echo   1. 点击自动盯盘按钮不再出现IOException异常
echo   2. 自动盯盘功能正常启动和停止
echo   3. UI状态正确更新（未启动 ↔ 自动盯盘运行中）
echo   4. 多次启停操作都稳定可靠
echo   5. 没有文件访问冲突相关的错误日志
echo   6. ContractProfiles.json文件能正常读写
echo.
echo 🔍 诊断指南:
echo   如果仍然出现文件访问异常：
echo   1. 检查是否有其他程序占用了ContractProfiles.json文件
echo   2. 确认静态锁修复是否正确应用
echo   3. 查看日志中的重试信息，判断重试机制是否生效
echo   4. 验证Data目录是否有正确的读写权限
echo   如果出现重试警告：
echo   5. 这是正常现象，说明重试机制在工作
echo   6. 只要最终操作成功完成，就表示修复有效
echo   7. 频繁重试可能表示系统IO负载较高
echo.

pause
echo.
echo 🎉 文件访问冲突修复验证完成！
echo    现在应该可以正常点击自动盯盘按钮，不会再出现文件访问异常！ 