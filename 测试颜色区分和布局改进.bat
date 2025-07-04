@echo off
chcp 65001 >nul
echo ============================
echo 🎨 测试颜色区分和布局改进
echo ============================
echo.
echo 🎯 本次改进内容：
echo   1. 界面布局改为上下结构 ✅
echo      • 上方：合约表格（75%空间）
echo      • 下方：配置信息（左） + 历史记录（右）
echo.
echo   2. 中文化类型和状态显示 ✅
echo      • 类型：保本条件、加仓条件、止盈条件
echo      • 状态：未触发、已执行（移除执行中）
echo.
echo   3. 颜色区分功能 🎨✨
echo      • 保本条件：蓝色边框 + 浅蓝背景
echo      • 加仓条件：橙色边框 + 浅橙背景  
echo      • 止盈条件：绿色边框 + 浅绿背景
echo      • 状态颜色：
echo        - 未触发：浅灰色文字
echo        - 已执行：深绿色文字
echo.
echo   4. 止盈条件双值支持 ✅
echo      • 触发值：达到多少盈利时触发
echo      • 保留值：触发时保留的盈利金额
echo.
echo 📝 测试步骤：
echo   1. 启动程序，设置API账号
echo   2. 进入自动盯盘配置，添加合约并配置参数
echo   3. 点击"自动盯盘"打开新界面
echo   4. 验证上下布局结构 
echo   5. 检查颜色区分效果：
echo      - 不同类型的条件有不同颜色
echo      - 不同状态的文字有不同颜色  
echo      - 编辑对话框中的颜色标识
echo   6. 测试编辑功能（包括止盈双值）
echo.
echo ✅ 预期颜色效果：
echo   • 保本条件：🔵 蓝色系（DodgerBlue边框）
echo   • 加仓条件：🟠 橙色系（Orange边框）
echo   • 止盈条件：🟢 绿色系（Green边框）
echo   • 未触发状态：⚪ 浅灰色文字
echo   • 已执行状态：✅ 深绿色文字
echo   • 已执行背景：浅绿色统一背景
echo.
echo ============================
echo 🚀 启动测试程序
echo ============================

echo 正在编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity quiet

if %ERRORLEVEL% EQU 0 (
    echo ✅ 编译成功，启动程序...
    start "" "bin/Debug/net6.0-windows/BinanceFuturesTrader.exe"
) else (
    echo ❌ 编译失败，请检查代码错误
    pause
    exit /b 1
)

echo.
echo 💡 程序已启动，请特别注意颜色区分效果：
echo    1. 合约表格中不同类型条件的颜色标识
echo    2. 编辑对话框中的彩色边框和背景
echo    3. 状态文字的颜色变化
echo    4. 止盈条件的双值编辑功能
echo.
echo 🎨 如果颜色显示正常，说明新功能工作正常
echo    如有问题，请截图反馈具体现象
echo.
pause 