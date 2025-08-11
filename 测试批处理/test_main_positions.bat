@echo off
chcp 65001 >nul
echo 🎯 主界面3个测试持仓验证脚本
echo =====================================
echo.

echo 📋 验证目标：
echo    主界面持仓列表应显示3个测试合约：
echo    📊 BTCUSDT LONG: 浮盈 +250.75U
echo    📊 ETHUSDT LONG: 浮盈 +1000.00U  
echo    📊 XRPUSDT SHORT: 浮盈 -100.00U
echo.

echo 🔧 步骤1: 检查代码修改
echo 验证测试数据逻辑是否添加...
findstr /n "【测试模式】为Test账户添加多品种测试数据" "ViewModels\MainViewModel.Data.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 测试模式逻辑已添加到MainViewModel.Data.cs
) else (
    echo ❌ 测试模式逻辑未找到
)

findstr /n "UnrealizedProfit = 1000.00m" "ViewModels\MainViewModel.Data.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ ETH浮盈1000U设置正确
) else (
    echo ❌ ETH浮盈设置未找到
)

findstr /n "UnrealizedProfit = -100.00m" "ViewModels\MainViewModel.Data.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ XRP浮盈-100U设置正确
) else (
    echo ❌ XRP浮盈设置未找到
)
echo.

echo 🔧 步骤2: 编译项目
echo 正在编译项目...
dotnet build TCWin.sln --verbosity quiet >nul 2>&1
if %errorlevel%==0 (
    echo ✅ 项目编译成功 - 属性错误已修复
    echo ✅ Percentage和InitialMargin编译错误已解决
) else (
    echo ❌ 项目编译失败
    echo 请检查编译错误并修复
    pause
    exit /b 1
)
echo.

echo 🔧 步骤3: 启动验证
echo 准备启动程序进行验证...
echo.
echo 📝 测试步骤：
echo 1. 程序启动后，确保选择"Test"账户
echo 2. 查看主界面持仓列表
echo 3. 应该看到3个测试合约（而不是1个）
echo 4. 验证浮盈数值：BTC +250.75U, ETH +1000.00U, XRP -100.00U
echo 5. 验证颜色显示：BTC和ETH绿色，XRP红色
echo.

echo 💡 触发条件：
echo    • 选择Test账户
echo    • 当前持仓数量 ≤ 1个时自动添加测试数据
echo    • 系统会在数据刷新时自动检测并添加
echo.

echo 🚀 启动程序...
if exist "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe" (
    start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"
    echo ✅ 程序已启动
    echo.
    echo 🔍 请按照上述步骤验证3个测试持仓是否正确显示
    echo.
    echo 📊 预期显示效果：
    echo ┌─────────────────────────────────────────┐
    echo │ 序号  合约      方向   浮盈      颜色   │
    echo ├─────────────────────────────────────────┤
    echo │  1   BTCUSDT   LONG   +250.75U   绿色  │
    echo │  2   ETHUSDT   LONG   +1000.00U  绿色  │
    echo │  3   XRPUSDT   SHORT  -100.00U   红色  │
    echo └─────────────────────────────────────────┘
) else (
    echo ❌ 未找到编译后的程序文件
    echo 请先编译项目
)

echo.
echo 🎯 主界面测试持仓验证脚本执行完成！
pause 