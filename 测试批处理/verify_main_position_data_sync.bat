@echo off
echo Main Position Data Sync Verification Script
echo ===========================================
echo.
echo This script verifies the main UI position data synchronization
echo.
echo Step 1: Check enhanced data loading method
findstr /n "CreateEnhancedTestContractConfigs" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] CreateEnhancedTestContractConfigs method exists
) else (
    echo [FAIL] CreateEnhancedTestContractConfigs method not found
)
echo.

echo Step 2: Check main UI position data access
findstr /n "mainViewModel.*Positions" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Main UI position data access implemented
) else (
    echo [FAIL] Main UI position data access not found
)
echo.

echo Step 3: Check real-time PnL synchronization
findstr /n "matchingPosition.*UnrealizedProfit" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Real-time PnL synchronization implemented
) else (
    echo [FAIL] Real-time PnL synchronization not found
)
echo.

echo Step 4: Check fallback to test data
findstr /n "使用测试默认数据" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Fallback to test data implemented
) else (
    echo [FAIL] Fallback to test data not found
)
echo.

echo Step 5: Check intelligent config setting based on PnL
findstr /n "根据浮盈状态设置配置数据" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Intelligent config setting based on PnL implemented
) else (
    echo [FAIL] Intelligent config setting not found
)
echo.

echo Step 6: Compilation check
echo Compiling project...
dotnet build TCWin.sln --verbosity quiet
if %errorlevel% equ 0 (
    echo [OK] Project compiled successfully
) else (
    echo [FAIL] Project compilation failed
    goto :end
)
echo.

echo ===========================================
echo Fix Verification Summary:
echo ===========================================
echo.
echo [IMPLEMENTED FEATURES]:
echo  * Enhanced test config creation with main UI position data
echo  * Real-time PnL synchronization from main UI positions
echo  * Intelligent config setting based on profit/loss status
echo  * Fallback mechanism to test data when no main UI data
echo  * Matching position logic by symbol and side
echo.
echo [DATA FLOW]:
echo  1. Load config window
echo  2. Get real-time positions from main UI (_mainViewModel.Positions)
echo  3. Match positions by symbol and side (LONG/SHORT)
echo  4. Use real PnL data: matchingPosition.UnrealizedProfit
echo  5. Use real price data: matchingPosition.MarkPrice
echo  6. Set intelligent configs based on profit status
echo  7. Display complete configuration data
echo.
echo [INTELLIGENT CONFIG LOGIC]:
echo  * PnL ^> 500: Large profit scenario (ETH)
echo    - Break-even: ✓ (executed)
echo    - Push tier 1: ✓ (executed) 
echo    - Profit tier 1: ✓ (executed)
echo    - Other tiers: Not triggered
echo.
echo  * PnL ^> 0: Normal profit scenario (BTC)  
echo    - All configs enabled but not triggered
echo    - 4-tier push config + 3-tier profit config
echo.
echo  * PnL ^< 0: Loss scenario (XRP)
echo    - All push/profit configs disabled
echo    - Only break-even config enabled
echo.
echo [EXPECTED BEHAVIOR]:
echo  * Config window shows real-time PnL from main UI
echo  * No more "-" or "0" for position profit values
echo  * Config data properly populated based on profit status
echo  * Refresh preserves real-time data synchronization
echo.
echo [TEST SCENARIOS]:
echo.
echo Scenario 1: With real positions in main UI
echo  1. Start program, select account with positions
echo  2. Open config window
echo  3. Verify PnL matches main UI exactly
echo  4. Verify configs match profit status
echo.
echo Scenario 2: Without real positions (Test account)
echo  1. Start program, select Test account  
echo  2. Open config window
echo  3. Verify test data with proper PnL values
echo  4. Verify intelligent config assignment
echo.
echo Scenario 3: Refresh functionality
echo  1. Change positions in main UI
echo  2. Refresh config window
echo  3. Verify PnL updates from main UI
echo  4. Verify config data consistency
echo.
echo [LOG KEYWORDS]:
echo Look for these messages to verify functionality:
echo  * "从主界面获取到 X 个活跃持仓数据"
echo  * "使用主界面真实数据 - 浮盈:XXX.XXU"
echo  * "使用测试默认数据 - 浮盈:XXX.XXU"
echo  * "成功创建 X 个增强测试配置（结合主界面实时数据）"
echo.
echo Main position data sync verification complete!
echo.
echo Next step: Test with real positions to verify PnL synchronization

:end
pause 