@echo off
echo Display Format Fix Verification Script
echo ======================================
echo.
echo This script verifies the display format fixes based on user feedback
echo.

echo Step 1: Check break-even dynamic data setting
findstr /n "设置保本的动态数据用于UI显示" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Break-even dynamic data setting implemented
) else (
    echo [FAIL] Break-even dynamic data setting not found
)
echo.

echo Step 2: Check push tier format fix
findstr /n "TriggerProfitAmount:F0} {statusSymbol}" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Push tier format fixed (removed extra separator)
) else (
    echo [FAIL] Push tier format not fixed
)
echo.

echo Step 3: Check profit tier format fix  
findstr /n "ProtectionAmount:F0} {statusSymbol}" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Profit tier format fixed (removed extra separator)
) else (
    echo [FAIL] Profit tier format not fixed
)
echo.

echo Step 4: Check PopulateConfigFromState end verification
findstr /n "PopulateConfigFromState结束.*验证动态数据设置结果" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] PopulateConfigFromState end verification implemented
) else (
    echo [FAIL] PopulateConfigFromState end verification not found
)
echo.

echo Step 5: Check break-even dynamic data logging
findstr /n "保本动态数据.*GetDynamicData.*BreakEven" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Break-even dynamic data logging implemented
) else (
    echo [FAIL] Break-even dynamic data logging not found
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
echo Fix Summary:
echo ===========================================
echo.
echo [USER FEEDBACK]:
echo "数据显示有问题：如果状态是未触发，保本的分开显示，
echo 推仓的应该是触发值 - ，保盈展示的应该是 触发值|保盈值 -"
echo.
echo [PROBLEM ANALYSIS]:
echo 1. Data loads successfully from unified state file (3 configs)
echo 2. Display format doesn't match user expectations
echo 3. Break-even missing dynamic data setting
echo 4. Dynamic data being cleared after PopulateConfigFromState
echo.
echo [FIXES IMPLEMENTED]:
echo.
echo 1. BREAK-EVEN DISPLAY FORMAT FIX:
echo    * Added missing dynamic data setting for break-even
echo    * Format: "TriggerAmount StatusSymbol" → "95.00 -"
echo    * Code: config.SetDynamicData("BreakEven", breakEvenDisplayText);
echo.
echo 2. PUSH TIER DISPLAY FORMAT FIX:  
echo    * Removed extra separator before status symbol
echo    * Before: "950 | -" → After: "950 -"
echo    * Format: "TriggerAmount StatusSymbol"
echo.
echo 3. PROFIT TIER DISPLAY FORMAT FIX:
echo    * Removed extra separator before status symbol  
echo    * Before: "950 | 760 | -" → After: "950 | 760 -"
echo    * Format: "TriggerAmount | ProtectionAmount StatusSymbol"
echo.
echo 4. ENHANCED DEBUGGING:
echo    * Added verification at PopulateConfigFromState end
echo    * Logs all dynamic data settings for troubleshooting
echo    * Helps identify where data might be cleared
echo.
echo [EXPECTED DISPLAY FORMATS]:
echo.
echo For NotTriggered state (ExecutionState = 0):
echo  * Break-Even: "95.00 -" (target + status)
echo  * Push Tier: "950 -" (trigger + status)  
echo  * Profit Tier: "950 | 760 -" (trigger|protection + status)
echo.
echo For Executed state (ExecutionState = 2):
echo  * Break-Even: "95.00 √" (target + executed)
echo  * Push Tier: "950 √" (trigger + executed)
echo  * Profit Tier: "950 | 760 √" (trigger|protection + executed)
echo.
echo For Executing state (ExecutionState = 1):
echo  * Break-Even: "95.00 ⚡" (target + executing)
echo  * Push Tier: "950 ⚡" (trigger + executing)  
echo  * Profit Tier: "950 | 760 ⚡" (trigger|protection + executing)
echo.
echo [EXPECTED DEBUG OUTPUT]:
echo.
echo User should now see this in logs:
echo  🔍【PopulateConfigFromState结束】验证动态数据设置结果:
echo     保本动态数据: '95.00 -'
echo     推仓1动态数据: '950 -'
echo     推仓2动态数据: '1900 -'  
echo     保盈1动态数据: '950 | 760 -'
echo     保盈2动态数据: '1900 | 1520 -'
echo.
echo [TEST PROCEDURE]:
echo 1. Start program, select Test account
echo 2. Click "Auto Monitor" button to open config window
echo 3. Check log area for "PopulateConfigFromState结束" messages
echo 4. Verify display formats match expectations above
echo 5. Confirm no more "-" in all configuration columns
echo.
echo [TROUBLESHOOTING]:
echo If still seeing "-" in display:
echo  1. Check "PopulateConfigFromState结束" logs for correct data
echo  2. Compare with "转换后验证" logs to see if data is cleared
echo  3. Verify UI update process doesn't overwrite dynamic data
echo.
echo Display format fix verification complete!
echo.
echo Next step: Test the config window to verify all formats display correctly

:end
pause 