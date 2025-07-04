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
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set dt=%%a
set YYYY=%dt:~0,4%
set MM=%dt:~4,2%
set DD=%dt:~6,2%
set HH=%dt:~8,2%
set Min=%dt:~10,2%
set TIMESTAMP=%YYYY%%MM%%DD%_%HH%%Min%
set RELEASE_DIR=TCWin_Release_%TIMESTAMP%

if not exist "ServerRelease" mkdir "ServerRelease"
if exist "ServerRelease\%RELEASE_DIR%" rmdir /s /q "ServerRelease\%RELEASE_DIR%"
mkdir "ServerRelease\%RELEASE_DIR%"

echo Copying program files...
xcopy "bin\Release\net6.0-windows\*" "ServerRelease\%RELEASE_DIR%\" /E /I /Y

echo Creating version info file...
echo TCWin Server Release Package > "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo ============================================= >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
echo Release Time: %YYYY%-%MM%-%DD% %HH%:%Min% >> "ServerRelease\%RELEASE_DIR%\VersionInfo.txt"
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

echo Creating Chinese version info for convenience...
powershell -Command "[System.IO.File]::WriteAllText('ServerRelease\%RELEASE_DIR%\中文说明.txt', @'
TCWin 服务器发布包
==================

发布时间: %YYYY%-%MM%-%DD% %HH%:%Min%
版本编号: %TIMESTAMP%

本版本包含的重要修复:
- 重复推仓问题修复
- 超详细调试日志功能
- 监控面板清理状态功能
- 5秒冷却期优化
- 重启后状态恢复功能

新功能验证方法:
1. 启动自动盯盘后，在日志中查找: "=== 开始超级详细调试 ==="
2. 检查监控面板是否有: "清理状态" 按钮
3. 如果两个都有，说明版本更新成功

服务器更新步骤:
1. 停止服务器上运行的旧程序
2. 备份重要配置文件（如需要）
3. 删除服务器上的旧程序文件
4. 将此发布包的所有文件复制到服务器
5. 运行 BinanceFuturesTrader.exe
6. 验证新功能是否正常工作

重要提醒:
- 此版本专门修复重复推仓问题
- 如果问题仍存在，使用"清理状态"功能
- 冷却期已优化为5秒
- 重启后状态会正确恢复
==================
'@, [System.Text.Encoding]::UTF8)"

echo.
echo Release package created successfully!
echo Location: ServerRelease\%RELEASE_DIR%
echo.
echo Package contents:
dir "ServerRelease\%RELEASE_DIR%" /B
echo.
echo Perfect! All files created with proper encoding.
echo You can now copy ServerRelease\%RELEASE_DIR% to your server!
echo.
pause

explorer "ServerRelease\%RELEASE_DIR%" 