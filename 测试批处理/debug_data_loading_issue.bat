@echo off
echo Debug Data Loading Issue Verification Script
echo ============================================
echo.
echo This script verifies the enhanced debugging for data loading issues
echo.

echo Step 1: Check window Loaded event debugging
findstr /n "🔥.*窗口加载事件.*AutoMonitorConfigWindow_Loaded" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Window Loaded event debugging enhanced
) else (
    echo [FAIL] Window Loaded event debugging not found
)
echo.

echo Step 2: Check LoadExistingContractConfigsAsync debugging
findstr /n "🔥.*LoadExistingContractConfigsAsync.*方法开始执行" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] LoadExistingContractConfigsAsync debugging enhanced
) else (
    echo [FAIL] LoadExistingContractConfigsAsync debugging not found
)
echo.

echo Step 3: Check LoadInitialDataAsync debugging
findstr /n "🔥.*LoadInitialDataAsync.*开始执行" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] LoadInitialDataAsync debugging enhanced
) else (
    echo [FAIL] LoadInitialDataAsync debugging not found
)
echo.

echo Step 4: Check LoadContractConfigsFromStateFile debugging
findstr /n "🔥.*LoadContractConfigsFromStateFile.*方法开始执行" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] LoadContractConfigsFromStateFile debugging enhanced
) else (
    echo [FAIL] LoadContractConfigsFromStateFile debugging not found
)
echo.

echo Step 5: Check Critical logging integration
findstr /n "_logger.LogCritical" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [OK] Critical logging integrated for better tracking
) else (
    echo [FAIL] Critical logging not integrated
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
echo Debug Enhancement Summary:
echo ===========================================
echo.
echo [PROBLEM ANALYSIS]:
echo User reported: "AutoMonitor page contract configs not displaying data"
echo But logs show: "Unified state file exists and contains 3 contract states"
echo.
echo [ROOT CAUSE HYPOTHESIS]:
echo 1. Window Loaded event not triggering
echo 2. LoadExistingContractConfigsAsync not being called
echo 3. Data loading methods completing but UI not updating
echo 4. Exception occurring silently in the loading process
echo.
echo [DEBUGGING ENHANCEMENTS IMPLEMENTED]:
echo.
echo 1. WINDOW LOADED EVENT TRACKING:
echo    * Added 🔥 markers to track event triggering
echo    * Added Critical logging for console output
echo    * Service status checks and UI state tracking
echo.
echo 2. LOADEXISTINGCONTRACTCONFIGSASYNC TRACKING:
echo    * Method entry/exit logging
echo    * File existence checking with detailed output
echo    * Strategy 1 vs Strategy 2 path tracking
echo    * UI clear and update operations logging
echo.
echo 3. LOADINITIALDATAASYNC TRACKING:
echo    * Method entry/exit logging 
echo    * Calls to LoadExistingContractConfigsAsync tracking
echo    * Integration with window construction process
echo.
echo 4. LOADCONTRACTCONFIGSFROMSTATEFILE TRACKING:
echo    * Method entry point logging
echo    * Integration with all the enhanced debugging we added before
echo.
echo 5. CRITICAL LOGGING INTEGRATION:
echo    * All key debug points now use _logger.LogCritical
echo    * Ensures debug info appears in console output
echo    * Makes debugging visible even without AddLog UI updates
echo.
echo [EXPECTED DEBUG OUTPUT]:
echo.
echo When user clicks Auto Monitor button, they should see:
echo.
echo Console Output (Critical logs):
echo  * "🔥 【LoadInitialDataAsync】开始执行初始数据加载"
echo  * "🔥 【LoadInitialDataAsync】准备调用LoadExistingContractConfigsAsync"
echo  * "🔥 【LoadExistingContractConfigsAsync】方法开始执行"
echo  * "🔥 【LoadExistingContractConfigsAsync】文件是否存在: True"
echo  * "🔥 【LoadExistingContractConfigsAsync】文件存在，执行策略2"
echo  * "🔥 【LoadExistingContractConfigsAsync】准备调用LoadContractConfigsFromStateFile"
echo  * "🔥 【LoadContractConfigsFromStateFile】方法开始执行"
echo.
echo When window loads, additional output:
echo  * "🔥 【窗口加载事件】AutoMonitorConfigWindow_Loaded 事件被触发"
echo  * "🔥 【窗口加载事件】准备调用LoadExistingContractConfigsAsync"
echo  * (Same LoadExistingContractConfigsAsync sequence again)
echo.
echo Final State Check:
echo  * "🔥 【LoadExistingContractConfigsAsync】最终UI显示 X 个配置"
echo.
echo [DIAGNOSIS STRATEGY]:
echo.
echo 1. START PROGRAM: Select Test account
echo 2. OPEN CONFIG WINDOW: Click Auto Monitor button
echo 3. CHECK CONSOLE: Look for 🔥 marked debug messages
echo 4. IDENTIFY BREAK POINT: Find where the call chain stops
echo.
echo Possible scenarios:
echo  A) No "🔥 【LoadInitialDataAsync】开始执行" → Constructor issue
echo  B) "LoadInitialDataAsync" runs but no "窗口加载事件" → Event not firing
echo  C) Methods run but "最终UI显示 0 个配置" → Data loading issue
echo  D) All methods run but UI still blank → UI binding issue
echo.
echo [NEXT STEPS]:
echo 1. Test the enhanced debugging
echo 2. Identify exactly where the call chain breaks
echo 3. Focus debugging on the specific failure point
echo 4. Fix the root cause once identified
echo.
echo Debug enhancement verification complete!
echo.
echo Next step: Test with enhanced debugging to identify the exact failure point

:end
pause 