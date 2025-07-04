@echo off
chcp 65001 >nul
echo ====================================
echo     TCWin Server Release Package
echo ====================================
echo.

echo Cleaning old version...
dotnet clean BinanceFuturesTrader.csproj --configuration Release
echo Clean completed
echo.

echo Building Release version...
dotnet build BinanceFuturesTrader.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)
echo Build completed
echo.

echo Creating release package...
for /f "tokens=1-3 delims=/ " %%a in ('date /t') do (
    set year=%%c
    set month=%%a
    set day=%%b
)
for /f "tokens=1-2 delims=: " %%a in ('time /t') do (
    set hour=%%a
    set minute=%%b
)
set TIMESTAMP=%year%%month%%day%_%hour%%minute%
set TIMESTAMP=%TIMESTAMP: =0%
set RELEASE_DIR=TCWin_Release_%TIMESTAMP%

if not exist "ServerRelease" mkdir "ServerRelease"
if exist "ServerRelease\%RELEASE_DIR%" rmdir /s /q "ServerRelease\%RELEASE_DIR%"
mkdir "ServerRelease\%RELEASE_DIR%"

echo Copying program files...
xcopy "bin\Release\net6.0-windows\*" "ServerRelease\%RELEASE_DIR%\" /E /I /Y

echo Creating version info file...
echo TCWin Server Release Package > "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo ============================================= >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo Release Time: %date% %time% >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo Version ID: %TIMESTAMP% >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo. >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo Key Fixes in This Version: >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo - Duplicate position problem fixed >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo - Super detailed debug log function >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo - Monitor panel clear state function >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo - 5-second cooldown optimization >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo - State recovery after restart >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo. >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo Feature Verification: >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo 1. After starting auto-monitor, look for: >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo    "=== Super Detailed Debug ===" in log >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo 2. Check monitor panel for "Clear State" button >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo 3. If both exist, version update is successful >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo ============================================= >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"

echo Creating update guide...
echo Server Update Guide > "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo =================== >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo. >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo Update Steps: >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 1. Stop the old program running on server >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 2. Backup important config files if needed >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 3. Delete old program files on server >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 4. Copy all files from this package to server >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 5. Run BinanceFuturesTrader.exe >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo 6. Verify new features work properly >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo. >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo Important Notes: >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo - This version fixes duplicate position issue >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo - Use "Clear State" function if problem persists >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo - Cooldown period optimized to 5 seconds >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo - Position states recover properly after restart >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"
echo =================== >> "ServerRelease\%RELEASE_DIR%\UpdateGuide.txt"

echo Creating verification script...
echo @echo off > "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo chcp 65001 ^>nul >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo ==================================== >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo       New Version Verification >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo ==================================== >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo. >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo Starting program and verifying new features... >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo. >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo Please check after startup: >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo 1. Look for "=== Super Detailed Debug ===" in log >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo 2. Check monitor panel for "Clear State" button >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo 3. If both exist, version update is successful! >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo echo. >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo pause >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"
echo start "" "BinanceFuturesTrader.exe" >> "ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat"

echo.
echo Release package created successfully!
echo Location: ServerRelease\%RELEASE_DIR%
echo.
echo Package contents:
dir "ServerRelease\%RELEASE_DIR%" /B
echo.
echo You can now copy ServerRelease\%RELEASE_DIR% to your server!
echo All files use proper encoding without Chinese character issues.
echo.
pause

explorer "ServerRelease\%RELEASE_DIR%" 