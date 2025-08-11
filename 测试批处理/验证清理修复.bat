@echo off
chcp 65001 >nul
echo ============================================
echo 🧹 验证状态清理修复
echo ============================================
echo.

echo 🔧 本次修复内容：
echo   ✅ 修复了数据源不同步问题
echo   ✅ 清理操作现在同步更新所有数据源
echo   ✅ UI立即强制刷新，无需等待
echo   ✅ 详细的清理验证和反馈
echo.

echo 📋 测试步骤：
echo 1. 启动程序，设置API账号
echo 2. 进入自动盯盘配置，添加合约
echo 3. 启动自动盯盘，等待触发一些执行
echo 4. 打开监控面板
echo 5. 点击"🧹 清理状态"按钮
echo 6. 查看效果验证
echo.

echo ✅ 修复后应该看到：
echo   • 点击清理状态后，立即弹出确认对话框
echo   • 确认后显示详细清理信息（合约数量、触发记录等）
echo   • 保本状态立即变为"未触发"（不再是"已触发"）
echo   • 执行历史中出现"🧹 状态清理"记录
echo   • 所有推仓进度重置为0
echo   • UI立即刷新，无延迟
echo.

echo 🔍 关键验证点：
echo   • 保本状态列：从"已触发"变为"未触发"
echo   • 推仓进度：从"X/Y"变为"0/Y"
echo   • 执行历史：顶部出现清理记录
echo   • 触发记录数：变为0
echo.

echo ============================================
echo 🚀 启动测试程序
echo ============================================

Start-Process "bin\Release\net6.0-windows\BinanceFuturesTrader.exe" -WorkingDirectory "bin\Release\net6.0-windows"

echo.
echo 💡 程序已启动，请按照上述步骤测试清理功能
echo 💡 如果还有问题，请截图反馈具体现象
echo.
pause 