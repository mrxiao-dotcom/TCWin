@echo off
chcp 65001 >nul
echo ============================================
echo 🔍 验证源代码修复情况
echo ============================================
echo.

echo 📋 检查1: 源代码中的清理状态按钮
findstr /n "🧹 清理状态" "Views\AutoMonitorDashboard.xaml.cs"
if %errorlevel%==0 (
    echo ✅ 源代码中包含清理状态按钮
) else (
    echo ❌ 源代码中未找到清理状态按钮
)
echo.

echo 📋 检查2: 源代码中的ClearStatesButton_Click方法
findstr /n "ClearStatesButton_Click" "Views\AutoMonitorDashboard.xaml.cs"
if %errorlevel%==0 (
    echo ✅ 源代码中包含清理状态按钮事件处理方法
) else (
    echo ❌ 源代码中未找到清理状态按钮事件处理方法
)
echo.

echo 📋 检查3: 源代码中的超详细调试日志
findstr /n "🔧 === 开始超级详细调试 ===" "Services\AutoMonitorService.cs"
if %errorlevel%==0 (
    echo ✅ 源代码中包含超详细调试日志
) else (
    echo ❌ 源代码中未找到超详细调试日志
)
echo.

echo 📋 检查4: 程序文件编译时间
for %%f in ("bin\Release\net6.0-windows\BinanceFuturesTrader.exe") do (
    echo 程序文件: %%~tf - %%~zf 字节
)
for %%f in ("bin\Release\net6.0-windows\BinanceFuturesTrader.dll") do (
    echo 程序库文件: %%~tf - %%~zf 字节
)
echo.

echo 📋 检查5: UI初始化方式
findstr /n "InitializeSimpleWindow" "Views\AutoMonitorDashboard.xaml.cs"
if %errorlevel%==0 (
    echo ✅ 程序使用代码UI初始化（已修复）
) else (
    echo ❌ 程序UI初始化方式未确认
)
echo.

echo ============================================
echo 🎯 源代码修复验证完成
echo ============================================
echo.
echo 💡 如果所有检查都显示 ✅，说明源代码修复成功
echo 💡 现在应该能在监控面板看到 3个按钮：
echo    - 🧹 清理状态 (新功能)
echo    - 🔄 刷新
echo    - ❌ 关闭
echo.
pause 