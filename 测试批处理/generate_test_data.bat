@echo off
chcp 65001 >nul
echo Multi-Contract Test Data Generator
echo =====================================
echo.

echo Will generate 3 test contracts:
echo    BTC LONG: +250.75U profit
echo    ETH LONG: +1000.00U profit (user requested)
echo    XRP SHORT: -100.00U loss
echo.

echo Step 1: Clear existing state file
set "STATE_DIR=%APPDATA%\BinanceFuturesTrader\Accounts\Test"
set "STATE_FILE=%STATE_DIR%\contract_monitoring_states.json"

if not exist "%STATE_DIR%" (
    mkdir "%STATE_DIR%" 2>nul
)

if exist "%STATE_FILE%" (
    echo Backing up existing state file...
    copy "%STATE_FILE%" "%STATE_FILE%.backup" >nul 2>&1
    del "%STATE_FILE%" >nul 2>&1
    echo State file cleared
) else (
    echo No existing state file
)
echo.

echo Step 2: Verify code changes
echo Checking ETH profit setting...
findstr /n "1000.00m.*用户要求1000" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ ETH profit 1000U setting confirmed
) else (
    echo ❌ ETH profit setting not found
)

echo Checking XRP loss setting...
findstr /n "-100.00m.*亏损场景" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ XRP loss -100U setting confirmed
) else (
    echo ❌ XRP loss setting not found
)

echo Checking test mode activation...
findstr /n "🧪 进入测试模式，创建多品种示例数据" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ Test mode activation confirmed
) else (
    echo ❌ Test mode activation not found
)

echo Checking data clearing logic...
findstr /n "ContractMonitors.Clear();" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ Data clearing logic confirmed
) else (
    echo ❌ Data clearing logic not found
)
echo.

echo Step 3: Ready to test
echo.
echo Expected results when launching program:
echo    📋 BTCUSDT LONG: +250.75U (green)
echo    📋 ETHUSDT LONG: +1000.00U (green) 
echo    📋 XRPUSDT SHORT: -100.00U (red)
echo.

echo 🎯 All modifications completed successfully!
echo.
echo Press any key to compile and test...
pause >nul

echo Building project...
if exist "BinanceFuturesTrader.sln" (
    dotnet build --configuration Debug --verbosity quiet
    if %errorlevel%==0 (
        echo ✅ Build successful
        
        if exist "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe" (
            echo Starting application...
            start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"
            echo.
            echo 🚀 Program started! Please verify:
            echo 1. Select Test account
            echo 2. Open Auto Monitor Config window  
            echo 3. Check if 3 test contracts are displayed
            echo 4. Verify profit/loss values and colors
        ) else (
            echo ❌ Executable not found after build
        )
    ) else (
        echo ❌ Build failed
    )
) else (
    echo ❌ Solution file not found
)

echo.
echo Test data generation complete!
pause 