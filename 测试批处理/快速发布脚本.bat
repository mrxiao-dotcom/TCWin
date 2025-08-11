@echo off
chcp 65001 >nul
echo.
echo ============================================
echo          TCWin 快速发布脚本 v1.0
echo ============================================
echo.

:: 获取当前日期作为版本标识
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%" & set "HH=%dt:~8,2%" & set "NN=%dt:~10,2%"
set "VERSION=%YY%%MM%%DD%_%HH%%NN%"

echo 📅 发布版本: v%VERSION%
echo.

:: 步骤1: 清理旧文件
echo 🧹 步骤1: 清理编译文件...
dotnet clean --configuration Release
if errorlevel 1 (
    echo ❌ 清理失败！
    pause
    exit /b 1
)
echo ✅ 清理完成
echo.

:: 步骤2: 编译项目
echo 🔨 步骤2: 编译项目...
dotnet build --configuration Release
if errorlevel 1 (
    echo ❌ 编译失败！请检查代码错误
    pause
    exit /b 1
)
echo ✅ 编译成功
echo.

:: 步骤3: 创建发布包文件夹
set "RELEASE_DIR=TCWin_Release_v%VERSION%"
echo 📁 步骤3: 创建发布包 %RELEASE_DIR%...

if exist "%RELEASE_DIR%" (
    echo 删除旧的发布包...
    rmdir /s /q "%RELEASE_DIR%"
)
mkdir "%RELEASE_DIR%"

:: 步骤4: 复制核心文件
echo 📋 步骤4: 复制程序文件...
copy "bin\Release\net6.0-windows\BinanceFuturesTrader.exe" "%RELEASE_DIR%\" >nul
copy "bin\Release\net6.0-windows\BinanceFuturesTrader.dll" "%RELEASE_DIR%\" >nul

:: 复制所有依赖DLL文件
echo 📚 复制依赖库文件...
for %%f in ("bin\Release\net6.0-windows\*.dll") do (
    if /i not "%%~nf"=="BinanceFuturesTrader" (
        copy "%%f" "%RELEASE_DIR%\" >nul
    )
)

:: 步骤5: 创建说明文档
echo 📝 步骤5: 创建说明文档...

:: 创建版本信息文件
(
echo Version: v%VERSION%
echo Build Date: %date% %time%
echo.
echo This is the latest release of TCWin Binance Futures Trading Bot.
echo.
echo Key Features:
echo - Fixed duplicate position opening issue
echo - Enhanced debugging with detailed logs
echo - State cleanup functionality
echo - 5-second cooldown optimization
echo - State recovery after restart
echo.
echo Verification Signs:
echo - Look for "🔧 === 开始超级详细调试 ===" in logs
echo - Check for "🧹 清理状态" button in monitor dashboard
echo - BinanceFuturesTrader.dll should be ~800KB
echo.
echo For deployment instructions, see UpdateGuide.txt
) > "%RELEASE_DIR%\VersionInfo.txt"

:: 创建更新指南
(
echo TCWin Update Guide
echo ==================
echo.
echo 1. Stop the current running TCWin application
echo 2. Backup your current installation folder
echo 3. Backup configuration files ^(appsettings.json^)
echo 4. Replace old files with new release files
echo 5. Restore your configuration files
echo 6. Run VerifyNewVersion.bat to verify installation
echo 7. Start the application and verify functionality
echo.
echo Verification Checklist:
echo [ ] Program starts without errors
echo [ ] Monitor dashboard displays correctly
echo [ ] "🧹 清理状态" button is visible
echo [ ] Debug logs show "🔧 === 开始超级详细调试 ==="
echo [ ] File sizes: .dll ~800KB, .exe ~166KB
echo.
echo If any verification fails, restore from backup and contact support.
) > "%RELEASE_DIR%\UpdateGuide.txt"

:: 创建中文说明文件（UTF-8编码）
powershell -Command "[System.IO.File]::WriteAllText('%RELEASE_DIR%\中文说明.txt', 'TCWin 自动盯盘交易机器人 v%VERSION%`r`n发布日期: '+[DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss')+' `r`n`r`n=== 核心功能 ===`r`n✅ 多阶梯推仓策略`r`n✅ 智能止损管理`r`n✅ 实时监控面板`r`n✅ 状态持久化存储`r`n`r`n=== 本版本修复 ===`r`n🔧 修复重启后重复推仓问题`r`n🔧 添加超详细调试日志功能`r`n🔧 优化冷却期为5秒`r`n🔧 添加一键清理状态功能`r`n🔧 增强状态恢复机制`r`n`r`n=== 验证标志 ===`r`n- 日志出现：🔧 === 开始超级详细调试 ===`r`n- 监控面板有：🧹 清理状态 按钮`r`n- 文件大小：BinanceFuturesTrader.dll 约800KB`r`n`r`n=== 部署说明 ===`r`n1. 停止旧版本程序`r`n2. 备份当前程序文件夹和配置文件`r`n3. 用新版本文件替换旧文件`r`n4. 恢复配置文件(appsettings.json)`r`n5. 运行VerifyNewVersion.bat验证`r`n6. 启动程序确认功能正常`r`n`r`n=== 重要提醒 ===`r`n- 部署前请先备份！`r`n- 确认验证标志后再投入使用`r`n- 如有问题请联系技术支持', [System.Text.Encoding]::UTF8)"

:: 创建验证脚本
(
echo @echo off
echo chcp 65001 ^>nul
echo echo.
echo echo ========================================
echo echo          版本验证检查
echo echo ========================================
echo echo.
echo.
echo echo 🔍 检查核心文件...
echo if not exist "BinanceFuturesTrader.exe" ^(
echo     echo ❌ 主程序文件缺失！
echo     pause
echo     exit /b 1
echo ^)
echo if not exist "BinanceFuturesTrader.dll" ^(
echo     echo ❌ 核心库文件缺失！
echo     pause  
echo     exit /b 1
echo ^)
echo echo ✅ 核心文件存在
echo.
echo echo 🔍 检查文件大小...
echo for %%%%f in ^("BinanceFuturesTrader.dll"^) do set size=%%%%~zf
echo set /a sizekb=%%size%%/1024
echo echo 📊 BinanceFuturesTrader.dll 大小: %%sizekb%%KB
echo if %%sizekb%% LSS 700 ^(
echo     echo ⚠️  文件大小异常！应该约800KB
echo ^) else ^(
echo     echo ✅ 文件大小正常
echo ^)
echo.
echo echo 🔍 尝试启动程序验证...
echo echo 注意：程序将启动5秒后自动关闭进行验证
echo start /wait timeout /t 5 /nobreak ^>nul
echo tasklist /fi "imagename eq BinanceFuturesTrader.exe" 2^>nul ^| find /i "BinanceFuturesTrader.exe" ^>nul
echo if errorlevel 1 ^(
echo     echo ℹ️  程序未运行或已正常退出
echo ^) else ^(
echo     echo ⚠️  程序仍在运行，请手动检查
echo ^)
echo.
echo echo ========================================
echo echo 验证完成！请手动启动程序进行最终确认
echo echo 确认要点：
echo echo - 监控面板有 🧹 清理状态 按钮
echo echo - 日志显示 🔧 === 开始超级详细调试 ===
echo echo ========================================
echo pause
) > "%RELEASE_DIR%\VerifyNewVersion.bat"

:: 步骤6: 显示发布包信息
echo.
echo ✅ 发布包创建完成！
echo.
echo 📊 发布包信息:
echo 📁 位置: %RELEASE_DIR%
echo 📋 包含文件:
for %%f in ("%RELEASE_DIR%\*.*") do (
    echo    - %%~nxf
)
echo.

:: 步骤7: 自动验证
echo 🔍 步骤7: 运行自动验证...
cd "%RELEASE_DIR%"
call VerifyNewVersion.bat
cd ..

echo.
echo ================================================
echo 🎉 发布完成！版本: v%VERSION%
echo ================================================
echo.
echo 📋 接下来的步骤:
echo 1. 检查发布包文件完整性
echo 2. 将 %RELEASE_DIR% 文件夹上传到服务器
echo 3. 在服务器上按照UpdateGuide.txt进行部署
echo 4. 确认验证标志正常显示
echo.
echo 📄 发布包位置: %cd%\%RELEASE_DIR%
echo.
pause 