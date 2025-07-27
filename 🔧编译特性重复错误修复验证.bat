@echo off
chcp 65001 > nul
echo.
echo ==========================================
echo 🔧 编译特性重复错误修复验证脚本
echo ==========================================
echo.

echo 📁 切换到项目目录...
cd /d "D:\CSharpProjects\TCWin"

echo.
echo 🧹 清理编译缓存...
if exist "obj" rmdir /s /q "obj"
if exist "bin" rmdir /s /q "bin"
if exist "Tests\obj" rmdir /s /q "Tests\obj"
if exist "Tests\bin" rmdir /s /q "Tests\bin"

echo.
echo 🔨 编译主项目 (Release)...
echo ==========================================
dotnet build BinanceFuturesTrader.csproj --configuration Release --no-restore --verbosity minimal
if %ERRORLEVEL% EQU 0 (
    echo ✅ 主项目编译成功！
) else (
    echo ❌ 主项目编译失败！
    echo.
    pause
    exit /b 1
)

echo.
echo 🔨 编译测试项目 (Release)...
echo ==========================================
dotnet build Tests\BinanceFuturesTrader.Tests.csproj --configuration Release --no-restore --verbosity minimal
if %ERRORLEVEL% EQU 0 (
    echo ✅ 测试项目编译成功！
) else (
    echo ❌ 测试项目编译失败！
    echo.
    pause
    exit /b 1
)

echo.
echo 🔨 编译主项目 (Debug)...
echo ==========================================
dotnet build BinanceFuturesTrader.csproj --configuration Debug --no-restore --verbosity minimal
if %ERRORLEVEL% EQU 0 (
    echo ✅ 主项目 Debug 编译成功！
) else (
    echo ❌ 主项目 Debug 编译失败！
    echo.
    pause
    exit /b 1
)

echo.
echo 🔨 编译测试项目 (Debug)...
echo ==========================================
dotnet build Tests\BinanceFuturesTrader.Tests.csproj --configuration Debug --no-restore --verbosity minimal
if %ERRORLEVEL% EQU 0 (
    echo ✅ 测试项目 Debug 编译成功！
) else (
    echo ❌ 测试项目 Debug 编译失败！
    echo.
    pause
    exit /b 1
)

echo.
echo ==========================================
echo 🎉 所有编译测试通过！
echo ==========================================
echo.
echo 📋 修复内容总结：
echo   • 在项目文件中添加了 GenerateAssemblyInfo=false
echo   • 创建了手动的 Properties\AssemblyInfo.cs 文件
echo   • 删除了测试项目的重复特性文件
echo   • 移除了可能冲突的框架特性
echo.
echo 💡 现在可以正常编译项目了！
echo.
pause 