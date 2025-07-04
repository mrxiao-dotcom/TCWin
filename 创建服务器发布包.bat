@echo off
echo ====================================
echo     创建服务器发布包脚本
echo ====================================
echo.

echo 🔄 正在清理旧版本...
dotnet clean BinanceFuturesTrader.csproj
echo ✅ 清理完成
echo.

echo 🏗️ 正在编译Release版本...
dotnet build BinanceFuturesTrader.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo ❌ 编译失败！
    pause
    exit /b 1
)
echo ✅ 编译完成
echo.

echo 📦 正在创建发布包...
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set PUBLISH_DIR=TCWin_Release_%TIMESTAMP%

if not exist "发布包" mkdir "发布包"
if exist "发布包\%PUBLISH_DIR%" rmdir /s /q "发布包\%PUBLISH_DIR%"
mkdir "发布包\%PUBLISH_DIR%"

echo 📁 复制程序文件...
xcopy "bin\Release\net6.0-windows\*" "发布包\%PUBLISH_DIR%\" /E /I /Y

echo 📝 创建版本信息文件...
echo 版本发布信息 > "发布包\%PUBLISH_DIR%\版本信息.txt"
echo ================ >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo 发布时间: %date% %time% >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo 版本标识: %TIMESTAMP% >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo 编译配置: Release >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo 项目路径: %CD% >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo. >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo 新功能标识: >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo - 超详细调试日志 >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo - 监控面板清理状态功能 >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo - 5秒冷却期优化 >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo - 重复推仓问题修复 >> "发布包\%PUBLISH_DIR%\版本信息.txt"
echo ================ >> "发布包\%PUBLISH_DIR%\版本信息.txt"

echo 📋 创建启动验证脚本...
echo @echo off > "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo ==================================== >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo       新版本功能验证脚本 >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo ==================================== >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo. >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo 🚀 启动程序并验证新功能... >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo. >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo ⚠️ 启动后请检查: >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo 1. 日志中是否出现: "🔧 === 开始超级详细调试 ===" >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo 2. 监控面板是否有: "🧹 清理状态" 按钮 >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo 3. 如果都有，说明版本更新成功！ >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo echo. >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo pause >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"
echo start "" "BinanceFuturesTrader.exe" >> "发布包\%PUBLISH_DIR%\验证新版本功能.bat"

echo 📄 创建更新说明...
echo 服务器更新指南 > "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo ================ >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo. >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 1. 停止服务器上的旧程序 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 2. 备份服务器上的配置文件（如果有） >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 3. 删除服务器上的旧程序文件 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 4. 复制此发布包中的所有文件到服务器 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 5. 运行 "验证新版本功能.bat" 验证更新 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 6. 如果验证成功，重新配置自动盯盘 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 7. 使用 "🧹 清理状态" 功能解决重复推仓 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo. >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo 验证新功能的标志: >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo - 日志出现: 🔧 === 开始超级详细调试 === >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo - 监控面板有: 🧹 清理状态 按钮 >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"
echo ================ >> "发布包\%PUBLISH_DIR%\服务器更新指南.txt"

echo.
echo ✅ 发布包创建完成！
echo 📁 位置: 发布包\%PUBLISH_DIR%
echo.
echo 📋 发布包内容:
dir "发布包\%PUBLISH_DIR%" /B
echo.
echo 🚀 下一步操作:
echo 1. 将 "发布包\%PUBLISH_DIR%" 文件夹复制到服务器
echo 2. 在服务器上运行 "验证新版本功能.bat"
echo 3. 验证新功能是否正常工作
echo.
pause

explorer "发布包\%PUBLISH_DIR%" 