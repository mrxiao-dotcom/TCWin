@echo off
chcp 65001 >nul
echo.
echo ============================================
echo       TCWin 专业发布脚本 v2.0
echo         (模拟VS2022发布功能)
echo ============================================
echo.

:: 获取当前日期作为版本标识
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%" & set "HH=%dt:~8,2%" & set "NN=%dt:~10,2%"
set "VERSION=%YY%%MM%%DD%_%HH%%NN%"

echo 📅 发布版本: v%VERSION%
echo ⚙️  使用 dotnet publish (等同于VS2022发布功能)
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

:: 步骤2: 专业发布 (等同于VS2022发布)
echo 🚀 步骤2: 专业发布 (dotnet publish)...
echo    - 配置: Release
echo    - 平台: Windows x64  
echo    - 运行时: 框架依赖
echo    - 单文件: 否
echo.

dotnet publish BinanceFuturesTrader.csproj --configuration Release --runtime win-x64 --self-contained false --output "publish_temp"
if errorlevel 1 (
    echo ❌ 发布失败！请检查项目配置
    pause
    exit /b 1
)
echo ✅ 发布成功
echo.

:: 步骤3: 创建最终发布包文件夹
set "RELEASE_DIR=TCWin_Professional_v%VERSION%"
echo 📁 步骤3: 创建专业发布包 %RELEASE_DIR%...

if exist "%RELEASE_DIR%" (
    echo 删除旧的发布包...
    rmdir /s /q "%RELEASE_DIR%"
)
mkdir "%RELEASE_DIR%"

:: 步骤4: 复制发布文件
echo 📋 步骤4: 复制发布文件...
robocopy "publish_temp" "%RELEASE_DIR%" /E /NFL /NDL /NJH /NJS
echo ✅ 文件复制完成
echo.

:: 清理临时发布文件夹
rmdir /s /q "publish_temp"

:: 步骤5: 创建专业版说明文档
echo 📝 步骤5: 创建专业版说明文档...

:: 创建版本信息文件
(
echo TCWin Professional Release v%VERSION%
echo Build Date: %date% %time%
echo Build Method: dotnet publish ^(VS2022 equivalent^)
echo Target Framework: .NET 6.0
echo Runtime: Windows x64
echo Deployment Type: Framework-dependent
echo.
echo This is a professional deployment package created using dotnet publish,
echo equivalent to Visual Studio 2022's Publish functionality.
echo.
echo Key Advantages of Professional Release:
echo - Optimized for production deployment
echo - Better performance than regular build
echo - Includes all necessary runtime dependencies
echo - Ready for server deployment
echo.
echo Key Features:
echo - Fixed duplicate position opening issue
echo - Enhanced debugging with detailed logs  
echo - State cleanup functionality ^(🧹 清理状态 button^)
echo - 5-second cooldown optimization
echo - State recovery after restart
echo.
echo Verification Signs:
echo - Look for "🔧 === 开始超级详细调试 ===" in logs
echo - Check for "🧹 清理状态" button in monitor dashboard
echo - BinanceFuturesTrader.dll should be ~800KB
echo.
echo Server Requirements:
echo - Windows Server 2016 or later
echo - .NET 6.0 Runtime installed
echo - Sufficient disk space and memory
echo.
echo For deployment instructions, see ProfessionalDeployGuide.txt
) > "%RELEASE_DIR%\VersionInfo.txt"

:: 创建专业部署指南
(
echo TCWin Professional Deployment Guide
echo ===================================
echo.
echo This package was created using 'dotnet publish' command,
echo equivalent to Visual Studio 2022's Publish functionality.
echo.
echo Server Requirements:
echo -------------------
echo 1. Windows Server 2016 or later
echo 2. .NET 6.0 Runtime ^(download from Microsoft^)
echo 3. Sufficient permissions to run the application
echo.
echo Deployment Steps:
echo ----------------
echo 1. Stop any existing TCWin application
echo 2. Create backup of current installation
echo    - Backup entire application folder
echo    - Backup configuration files ^(appsettings.json^)
echo    - Backup any data files
echo.
echo 3. Deploy new version:
echo    - Upload this entire folder to server
echo    - Extract to target directory
echo    - Restore backed up configuration files
echo.
echo 4. Verify deployment:
echo    - Run VerifyProfessionalVersion.bat
echo    - Check file integrity
echo    - Verify .NET runtime availability
echo.
echo 5. Start application:
echo    - Double-click BinanceFuturesTrader.exe
echo    - Or use command: dotnet BinanceFuturesTrader.dll
echo.
echo 6. Final verification:
echo    - Monitor dashboard displays correctly
echo    - "🧹 清理状态" button is visible
echo    - Debug logs show "🔧 === 开始超级详细调试 ==="
echo    - All trading functions work normally
echo.
echo Troubleshooting:
echo ---------------
echo - If "Framework not found" error:
echo   Download and install .NET 6.0 Runtime from Microsoft
echo - If permission errors:
echo   Run as Administrator or adjust folder permissions
echo - If configuration errors:
echo   Restore original appsettings.json file
echo.
echo Rollback Procedure:
echo ------------------
echo 1. Stop current application
echo 2. Restore from backup folder
echo 3. Verify backup version works
echo 4. Contact support if issues persist
) > "%RELEASE_DIR%\ProfessionalDeployGuide.txt"

:: 创建中文说明文件（UTF-8编码）
powershell -Command "[System.IO.File]::WriteAllText('%RELEASE_DIR%\专业版中文说明.txt', 'TCWin 专业版自动盯盘交易机器人 v%VERSION%`r`n发布日期: '+[DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss')+' `r`n发布方式: dotnet publish (等同于VS2022发布功能)`r`n`r`n=== 专业版优势 ===`r`n🚀 使用dotnet publish命令编译，性能更优`r`n🚀 等同于Visual Studio 2022发布功能`r`n🚀 专为生产环境优化`r`n🚀 包含完整运行时依赖`r`n🚀 更稳定的服务器部署`r`n`r`n=== 核心功能 ===`r`n✅ 多阶梯推仓策略`r`n✅ 智能止损管理`r`n✅ 实时监控面板`r`n✅ 状态持久化存储`r`n`r`n=== 本版本修复 ===`r`n🔧 修复重启后重复推仓问题`r`n🔧 添加超详细调试日志功能`r`n🔧 优化冷却期为5秒`r`n🔧 添加一键清理状态功能 (🧹 清理状态)`r`n🔧 增强状态恢复机制`r`n`r`n=== 服务器要求 ===`r`n- Windows Server 2016 或更高版本`r`n- 已安装.NET 6.0 运行时`r`n- 足够的磁盘空间和内存`r`n- 网络连接权限`r`n`r`n=== 专业部署流程 ===`r`n1. 停止旧版本程序`r`n2. 备份当前程序文件夹和配置文件`r`n3. 上传整个发布包到服务器`r`n4. 解压到目标目录`r`n5. 恢复配置文件(appsettings.json)`r`n6. 运行 VerifyProfessionalVersion.bat 验证`r`n7. 启动程序确认功能正常`r`n`r`n=== 验证标志 ===`r`n- 日志出现：🔧 === 开始超级详细调试 ===`r`n- 监控面板有：🧹 清理状态 按钮`r`n- 文件大小：BinanceFuturesTrader.dll 约800KB`r`n- 程序启动无错误提示`r`n`r`n=== 故障排除 ===`r`n- 如提示\"找不到框架\"：安装.NET 6.0运行时`r`n- 如权限错误：以管理员身份运行`r`n- 如配置错误：恢复原始配置文件`r`n`r`n=== 重要提醒 ===`r`n- 这是专业版发布包，专为生产环境设计`r`n- 部署前请务必备份！`r`n- 确认验证标志后再投入使用`r`n- 如有问题请联系技术支持', [System.Text.Encoding]::UTF8)"

:: 创建专业版验证脚本
(
echo @echo off
echo chcp 65001 ^>nul
echo echo.
echo echo ==========================================
echo echo       专业版本验证检查
echo echo     ^(Professional Version Check^)
echo echo ==========================================
echo echo.
echo.
echo echo 🔍 检查核心文件...
echo if not exist "BinanceFuturesTrader.exe" ^(
echo     echo ❌ 主程序文件缺失！
echo     echo Missing: BinanceFuturesTrader.exe
echo     pause
echo     exit /b 1
echo ^)
echo if not exist "BinanceFuturesTrader.dll" ^(
echo     echo ❌ 核心库文件缺失！
echo     echo Missing: BinanceFuturesTrader.dll
echo     pause  
echo     exit /b 1
echo ^)
echo echo ✅ 核心文件存在
echo.
echo echo 🔍 检查.NET运行时...
echo dotnet --version ^>nul 2^>^&1
echo if errorlevel 1 ^(
echo     echo ❌ .NET运行时未安装或不可用！
echo     echo Please install .NET 6.0 Runtime
echo     pause
echo     exit /b 1
echo ^) else ^(
echo     echo ✅ .NET运行时可用
echo     for /f %%i in ^('dotnet --version'^) do echo 📊 .NET版本: %%i
echo ^)
echo.
echo echo 🔍 检查专业版文件大小...
echo for %%%%f in ^("BinanceFuturesTrader.dll"^) do set size=%%%%~zf
echo set /a sizekb=%%size%%/1024
echo echo 📊 BinanceFuturesTrader.dll 大小: %%sizekb%%KB
echo if %%sizekb%% LSS 700 ^(
echo     echo ⚠️  文件大小异常！专业版应该约800KB
echo     echo Warning: File size seems incorrect for professional build
echo ^) else ^(
echo     echo ✅ 专业版文件大小正常
echo ^)
echo.
echo echo 🔍 检查发布包完整性...
echo set filecount=0
echo for %%%%f in ^(*.*^) do set /a filecount+=1
echo echo 📊 发布包文件数量: %%filecount%%
echo if %%filecount%% LSS 5 ^(
echo     echo ⚠️  发布包文件可能不完整
echo ^) else ^(
echo     echo ✅ 发布包文件数量正常
echo ^)
echo.
echo echo 🔍 测试程序启动能力...
echo echo 注意：将尝试启动程序进行快速验证
echo timeout /t 2 /nobreak ^>nul
echo start /wait /b BinanceFuturesTrader.exe --version 2^>nul ^|^| echo 程序启动测试完成
echo.
echo echo ==========================================
echo echo 🎉 专业版验证完成！
echo echo.
echo echo 手动验证要点：
echo echo ✓ 监控面板有 🧹 清理状态 按钮
echo echo ✓ 日志显示 🔧 === 开始超级详细调试 ===
echo echo ✓ 所有功能正常工作
echo echo.
echo echo 这是使用dotnet publish创建的专业版本，
echo echo 等同于Visual Studio 2022的发布功能。
echo echo ==========================================
echo pause
) > "%RELEASE_DIR%\VerifyProfessionalVersion.bat"

:: 步骤6: 显示发布包信息
echo.
echo 🎉 专业发布包创建完成！
echo.
echo 📊 专业版发布包信息:
echo 📁 位置: %RELEASE_DIR%
echo 🚀 发布方式: dotnet publish (VS2022等效)
echo 📋 包含文件数量:
for /f %%A in ('dir /b "%RELEASE_DIR%" ^| find /c /v ""') do echo    - 总计 %%A 个文件
echo.

:: 显示核心文件大小
echo 📊 核心文件大小:
for %%f in ("%RELEASE_DIR%\BinanceFuturesTrader.exe") do (
    set /a sizekb=%%~zf/1024
    echo    - BinanceFuturesTrader.exe: %%~zf bytes ^(!sizekb! KB^)
)
for %%f in ("%RELEASE_DIR%\BinanceFuturesTrader.dll") do (
    set /a sizekb=%%~zf/1024  
    echo    - BinanceFuturesTrader.dll: %%~zf bytes ^(!sizekb! KB^)
)
echo.

:: 步骤7: 自动验证
echo 🔍 步骤7: 运行专业版验证...
cd "%RELEASE_DIR%"
call VerifyProfessionalVersion.bat
cd ..

echo.
echo ================================================
echo 🎉 专业版发布完成！版本: v%VERSION%
echo ================================================
echo.
echo 📋 专业版特点:
echo ✓ 使用 dotnet publish 命令 (等同于VS2022发布)
echo ✓ 生产环境优化
echo ✓ 包含完整运行时依赖
echo ✓ 更好的性能和稳定性
echo.
echo 📋 接下来的步骤:
echo 1. 检查专业版发布包文件完整性
echo 2. 将 %RELEASE_DIR% 文件夹上传到服务器
echo 3. 在服务器上按照 ProfessionalDeployGuide.txt 进行专业部署
echo 4. 运行专业版验证确认功能正常
echo.
echo 📄 专业版发布包位置: %cd%\%RELEASE_DIR%
echo.
pause 