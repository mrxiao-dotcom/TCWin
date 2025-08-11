@echo off
chcp 65001 >nul
echo ====================================
echo     TCWin Server Release Package
echo ====================================
echo.

echo 清理旧版本...
dotnet clean BinanceFuturesTrader.csproj --configuration Release
echo 清理完成
echo.

echo 编译Release版本...
dotnet build BinanceFuturesTrader.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo 编译失败！
    pause
    exit /b 1
)
echo 编译完成
echo.

echo 创建发布包...
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%
set TIMESTAMP=%TIMESTAMP: =0%
set RELEASE_DIR=TCWin_Release_%TIMESTAMP%

if not exist "ServerRelease" mkdir "ServerRelease"
if exist "ServerRelease\%RELEASE_DIR%" rmdir /s /q "ServerRelease\%RELEASE_DIR%"
mkdir "ServerRelease\%RELEASE_DIR%"

echo 复制程序文件...
xcopy "bin\Release\net6.0-windows\*" "ServerRelease\%RELEASE_DIR%\" /E /I /Y

echo 创建版本说明文件...
powershell -Command "
$content = @'
TCWin Server Release Package
============================

发布时间: %date% %time%
版本标识: %TIMESTAMP%

本版本包含的重要修复:
- 重复推仓问题修复
- 超详细调试日志功能
- 监控面板清理状态功能
- 5秒冷却期优化
- 重启后状态恢复功能

新功能验证标志:
1. 启动自动盯盘后，日志中会出现: 🔧 === 开始超级详细调试 ===
2. 监控面板中会有: 🧹 清理状态 按钮
3. 如果遇到重复推仓问题，使用清理状态功能即可解决

服务器更新步骤:
1. 停止服务器上运行的旧程序
2. 备份重要配置文件（如有需要）
3. 删除服务器上的旧程序文件
4. 将此发布包的所有文件复制到服务器
5. 启动 BinanceFuturesTrader.exe
6. 验证新功能是否正常工作

重要提醒:
这个版本专门解决了重复推仓问题。如果问题仍然存在，
请使用新增的清理状态功能进行处理。
============================
'@
[System.IO.File]::WriteAllText('ServerRelease\%RELEASE_DIR%\VersionInfo.txt', $content, [System.Text.Encoding]::UTF8)
"

echo 创建服务器更新指南...
powershell -Command "
$content = @'
Server Update Guide
===================

How to Update:
1. Stop the old program running on the server
2. Backup important configuration files (if needed)
3. Delete the old program files on the server
4. Copy all files from this release package to the server
5. Run BinanceFuturesTrader.exe
6. Verify that the new features work properly

How to Verify New Features:
- After starting auto-monitoring, check the log for: 🔧 === 开始超级详细调试 ===
- Open the monitoring panel and check for: 🧹 清理状态 button
- If you encounter duplicate position issues, use the clear state function

Important Notes:
- This version specifically fixes the duplicate position issue
- If the problem still exists, use the new clear state function
- The cooldown period has been optimized to 5 seconds
- Position states will be properly restored after restart

Expected Results:
✅ No more duplicate position issues
✅ Detailed debug logs
✅ Monitor panel with clear state function
✅ Faster position response (5-second cooldown)
✅ Proper state recovery after restart
===================
'@
[System.IO.File]::WriteAllText('ServerRelease\%RELEASE_DIR%\UpdateGuide.txt', $content, [System.Text.Encoding]::UTF8)
"

echo 创建验证脚本...
powershell -Command "
$content = @'
@echo off
chcp 65001 >nul
echo ====================================
echo       New Version Verification
echo ====================================
echo.
echo Starting program and verifying new features...
echo.
echo Please check after startup:
echo 1. Look for in log: 🔧 === 开始超级详细调试 ===
echo 2. Check monitoring panel for: 🧹 清理状态 button
echo 3. If both exist, version update is successful!
echo.
pause
start "TCWin" "BinanceFuturesTrader.exe"
'@
[System.IO.File]::WriteAllText('ServerRelease\%RELEASE_DIR%\VerifyNewVersion.bat', $content, [System.Text.Encoding]::UTF8)
"

echo.
echo 发布包创建成功！
echo 位置: ServerRelease\%RELEASE_DIR%
echo.
echo 发布包内容:
dir "ServerRelease\%RELEASE_DIR%" /B
echo.
echo 现在可以将 ServerRelease\%RELEASE_DIR% 复制到服务器使用了！
echo 所有文件都使用UTF-8编码，不会出现中文乱码问题。
echo.
pause

explorer "ServerRelease\%RELEASE_DIR%" 