@echo off
chcp 65001 >nul
echo ============================
echo 🔍 诊断界面卡死和日志问题
echo ============================

echo.
echo 📋 问题现象：
echo   • 界面点击启动后卡死
echo   • 日志也无法看到
echo   • 按钮显示"正在启动"就停止响应
echo.

echo 🔍 开始诊断...
echo.

echo ⏳ 1. 检查日志文件位置...
set "log_file=%~dp0trading_log.txt"
echo 📁 预期日志文件位置：%log_file%

if exist "%log_file%" (
    echo ✅ 日志文件存在
    echo 📊 文件大小：
    for %%A in ("%log_file%") do echo    %%~zA 字节
    echo.
    echo 📄 最后5行日志内容：
    echo ----------------------------------------
    powershell -Command "Get-Content '%log_file%' -Tail 5"
    echo ----------------------------------------
) else (
    echo ❌ 日志文件不存在
    echo 💡 可能原因：
    echo   • 程序还没有开始写入日志
    echo   • 日志写入权限问题
    echo   • 程序在日志初始化之前就卡死了
)

echo.
echo ⏳ 2. 检查程序进程状态...
tasklist /fi "imagename eq TCWin.exe" /fo table 2>nul | find "TCWin.exe" >nul
if %errorlevel% == 0 (
    echo ✅ TCWin.exe 进程正在运行
    echo 📊 进程详情：
    tasklist /fi "imagename eq TCWin.exe" /fo table
) else (
    echo ❌ 未找到TCWin.exe进程
    echo 💡 如果程序界面还在，但进程不存在，说明可能是"僵尸"窗口
)

echo.
echo ⏳ 3. 检查系统资源...
echo 💾 内存使用情况：
wmic OS get TotalVisibleMemorySize,FreePhysicalMemory /value | find "="

echo.
echo 💿 CPU使用情况：
wmic cpu get loadpercentage /value | find "="

echo.
echo ⏳ 4. 检查网络连接...
echo 🌐 测试网络连接（ping baidu.com）：
ping baidu.com -n 1 -w 1000 >nul 2>&1
if %errorlevel% == 0 (
    echo ✅ 网络连接正常
) else (
    echo ❌ 网络连接异常
    echo 💡 网络问题可能导致API调用卡死
)

echo.
echo ⏳ 5. 检查币安API连接...
echo 🔗 测试币安API连接：
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://fapi.binance.com/fapi/v1/ping' -TimeoutSec 5; if ($response.StatusCode -eq 200) { Write-Host '✅ 币安API连接正常' } else { Write-Host '❌ 币安API连接异常' } } catch { Write-Host '❌ 币安API连接失败:', $_.Exception.Message }"

echo.
echo ⏳ 6. 检查应用程序配置目录...
set "app_data=%APPDATA%\BinanceFuturesTrader"
echo 📁 应用数据目录：%app_data%

if exist "%app_data%" (
    echo ✅ 应用数据目录存在
    echo 📊 目录内容：
    dir "%app_data%" /b
    
    echo.
    echo 📁 检查自动监控数据目录：
    set "monitor_data=%app_data%\AutoMonitor"
    if exist "%monitor_data%" (
        echo ✅ 自动监控数据目录存在
        echo 📊 目录内容：
        dir "%monitor_data%" /b
    ) else (
        echo ❌ 自动监控数据目录不存在
    )
) else (
    echo ❌ 应用数据目录不存在
    echo 💡 可能是首次运行或权限问题
)

echo.
echo ⏳ 7. 检查系统事件日志...
echo 📋 检查最近的应用程序错误：
powershell -Command "Get-EventLog -LogName Application -EntryType Error -Newest 3 -Source *TCWin* -ErrorAction SilentlyContinue | Format-Table TimeGenerated, Source, Message -Wrap"

echo.
echo ============================
echo 🔧 诊断完成
echo ============================
echo.
echo 📝 诊断建议：
echo.
echo 💡 如果日志文件不存在或很小：
echo   • 程序可能在日志初始化之前就卡死
echo   • 检查是否有防火墙或杀毒软件阻止
echo   • 尝试以管理员身份运行程序
echo.
echo 💡 如果网络连接有问题：
echo   • 检查网络连接
echo   • 检查防火墙设置
echo   • 确认币安API地址可访问
echo.
echo 💡 如果进程存在但界面卡死：
echo   • 可能是UI线程死锁
echo   • 尝试强制结束进程后重启
echo   • 检查最近的系统事件日志
echo.
echo 💡 如果应用数据目录有问题：
echo   • 检查磁盘空间
echo   • 检查文件权限
echo   • 尝试删除配置文件重新开始
echo.
echo 🚨 紧急处理方案：
echo   1. 强制结束TCWin.exe进程
echo   2. 删除配置目录 %APPDATA%\BinanceFuturesTrader
echo   3. 重新启动程序
echo   4. 重新配置账户信息
echo.
echo 请将此诊断结果提供给技术支持以便进一步分析。
echo.
pause 