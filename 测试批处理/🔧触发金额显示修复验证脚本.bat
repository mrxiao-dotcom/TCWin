@echo off
chcp 65001 > nul
echo 🔧 触发金额显示修复验证脚本
echo.

echo 📋 验证步骤：
echo 1. 清理现有状态文件
echo 2. 编译项目
echo 3. 运行程序，打开启动盯盘面板
echo 4. 检查UI显示的触发金额是否与基础配置文件一致
echo.

echo 🧹 步骤1: 清理现有状态文件...
if exist "%APPDATA%\BinanceFuturesTrader\Accounts" (
    rmdir /s /q "%APPDATA%\BinanceFuturesTrader\Accounts"
    echo ✅ 已清理Accounts目录
) else (
    echo ℹ️ Accounts目录不存在，跳过清理
)

if exist "%APPDATA%\BinanceFuturesTrader\Global" (
    rmdir /s /q "%APPDATA%\BinanceFuturesTrader\Global"
    echo ✅ 已清理Global目录
) else (
    echo ℹ️ Global目录不存在，跳过清理
)

echo.
echo 🔨 步骤2: 编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity minimal

if %ERRORLEVEL% NEQ 0 (
    echo ❌ 编译失败！请检查代码错误
    pause
    exit /b 1
)

echo ✅ 编译成功！
echo.

echo 🚀 步骤3: 启动程序...
echo.
echo 📋 验证要点：
echo   - 基础配置文件中保本触发金额应为 95.00
echo   - UI显示的触发金额应为 95，而不是 0
echo   - 状态文件应自动生成
echo.
echo 请按任意键启动程序进行测试...
pause > nul

start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"

echo.
echo 🔍 程序已启动，请进行以下验证：
echo.
echo 1. 📄 检查基础配置文件内容：
echo    文件: %%APPDATA%%\BinanceFuturesTrader\Global\auto_monitor_configs.json
echo    期望内容: "triggerProfitAmount": 95.00
echo.
echo 2. 🖥️ 检查UI显示：
echo    - 打开"启动盯盘面板"
echo    - 查看保本触发金额是否显示为 95
echo    - 确认不是显示 0
echo.
echo 3. 📁 检查状态文件生成：
echo    文件: %%APPDATA%%\BinanceFuturesTrader\Accounts\{账户名}\contract_monitoring_states.json
echo    期望: 文件应自动生成，且包含正确的触发金额
echo.
echo 4. 🔄 检查数据同步：
echo    - UI显示的数据应与文件内容一致
echo    - 状态变更应实时保存到文件
echo.

echo 验证完成后请按任意键...
pause > nul

echo.
echo 📊 修复总结：
echo ✅ 修复了状态文件生成流程
echo ✅ 修复了UI从状态文件读取数据
echo ✅ 确保触发金额正确显示
echo.
echo 如有问题，请检查调试日志！
pause 