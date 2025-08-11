@echo off
chcp 65001 >nul
echo ============================================
echo 🧹 测试状态清理功能
echo ============================================
echo.

echo 📋 测试步骤：
echo 1. 启动程序
echo 2. 进入自动盯盘设置，添加一些合约
echo 3. 启动自动盯盘
echo 4. 打开监控面板
echo 5. 点击"🧹 清理状态"按钮
echo 6. 检查以下内容：
echo.

echo ✅ 应该看到的效果：
echo   • 监控面板右上角有3个按钮：🧹清理状态、🔄刷新、❌关闭
echo   • 点击清理状态后，弹出确认对话框
echo   • 确认清理后，所有保本状态变为"未触发"
echo   • 执行历史中出现"🧹 状态清理"记录
echo   • 显示清理了几个合约的详细信息
echo.

echo 🔧 修复内容：
echo   • 修复了保本状态不被清理的问题
echo   • 改进了清理逻辑，确保逐个清理每个合约
echo   • 在执行历史中添加详细的清理记录
echo   • 修复了全局清理时的空指针问题
echo.

echo ============================================
echo 🚀 开始测试
echo ============================================
echo.

echo 启动程序...
Start-Process "bin\Release\net6.0-windows\BinanceFuturesTrader.exe" -WorkingDirectory "bin\Release\net6.0-windows"

echo.
echo 💡 程序已启动，请按照上面的测试步骤进行验证
echo.
pause 