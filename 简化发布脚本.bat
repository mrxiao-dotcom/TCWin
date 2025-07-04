@echo off
echo ====================================
echo     服务器发布包生成脚本
echo ====================================
echo.

echo 🔄 清理旧版本...
dotnet clean BinanceFuturesTrader.csproj --configuration Release
echo ✅ 清理完成
echo.

echo 🏗️ 编译Release版本...
dotnet build BinanceFuturesTrader.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo ❌ 编译失败！
    pause
    exit /b 1
)
echo ✅ 编译完成
echo.

echo 📦 创建发布包...
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%
set TIMESTAMP=%TIMESTAMP: =0%
set RELEASE_DIR=TCWin_Release_%TIMESTAMP%

if not exist "服务器发布包" mkdir "服务器发布包"
if exist "服务器发布包\%RELEASE_DIR%" rmdir /s /q "服务器发布包\%RELEASE_DIR%"
mkdir "服务器发布包\%RELEASE_DIR%"

echo 📁 复制程序文件...
xcopy "bin\Release\net6.0-windows\*" "服务器发布包\%RELEASE_DIR%\" /E /I /Y

echo 📝 创建版本说明文件...
echo 🚀 TCWin 服务器发布包 > "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo ======================= >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo 发布时间: %date% %time% >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo 版本标识: %TIMESTAMP% >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo. >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo 🔧 本版本包含的重要修复: >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo - 重复推仓问题修复 >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo - 超详细调试日志功能 >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo - 监控面板清理状态功能 >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo - 5秒冷却期优化 >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo - 重启后状态恢复功能 >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"
echo ======================= >> "服务器发布包\%RELEASE_DIR%\版本说明.txt"

echo 📋 创建使用说明...
echo 🔄 服务器更新步骤 > "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo ================== >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo. >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 1. 停止服务器上运行的旧程序 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 2. 备份重要配置文件（如有需要） >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 3. 删除服务器上的旧程序文件 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 4. 将此发布包的所有文件复制到服务器 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 5. 启动 BinanceFuturesTrader.exe >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 6. 检查程序正常启动和运行 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo. >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo 🔍 如何验证新版本功能: >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo - 启动自动盯盘后，在日志中查看是否有 "🔧 === 开始超级详细调试 ===" >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo - 打开监控面板，检查是否有 "🧹 清理状态" 按钮 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo - 如果遇到重复推仓问题，使用 "🧹 清理状态" 功能清理 >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"
echo ================== >> "服务器发布包\%RELEASE_DIR%\服务器更新指南.txt"

echo.
echo ✅ 发布包创建成功！
echo 📁 位置: 服务器发布包\%RELEASE_DIR%
echo.
echo 📋 发布包内容:
dir "服务器发布包\%RELEASE_DIR%" /B
echo.
echo 🚀 现在可以将 "服务器发布包\%RELEASE_DIR%" 复制到服务器使用了！
echo.
pause

explorer "服务器发布包\%RELEASE_DIR%" 