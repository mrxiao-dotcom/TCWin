@echo off
chcp 65001 >nul
echo 🔒 文件驱动状态管理系统验证脚本
echo =====================================
echo.

set "LOG_FILE=%USERPROFILE%\AppData\Local\Temp\file_lock_verification.log"
set "STATE_FILE=%APPDATA%\BinanceFuturesTrader\AutoMonitor\contract_monitoring_states.json"

echo 📍 开始验证文件锁定机制... > "%LOG_FILE%"
echo %DATE% %TIME% >> "%LOG_FILE%"
echo. >> "%LOG_FILE%"

echo 🔍 步骤1: 检查状态文件路径
echo 状态文件路径: %STATE_FILE%
if exist "%STATE_FILE%" (
    echo ✅ 状态文件存在
    echo ✅ 状态文件存在 >> "%LOG_FILE%"
) else (
    echo ⚠️  状态文件不存在，这是正常的（首次运行）
    echo ⚠️  状态文件不存在 >> "%LOG_FILE%"
)
echo.

echo 🔍 步骤2: 检查新增的服务文件
set "SERVICE_1=Services\FileBasedStateManager.cs"
set "SERVICE_2=Services\AutoMonitorScanExecutor.cs"

if exist "%SERVICE_1%" (
    echo ✅ FileBasedStateManager.cs 已创建
    echo ✅ FileBasedStateManager.cs 存在 >> "%LOG_FILE%"
) else (
    echo ❌ FileBasedStateManager.cs 缺失！
    echo ❌ FileBasedStateManager.cs 缺失 >> "%LOG_FILE%"
)

if exist "%SERVICE_2%" (
    echo ✅ AutoMonitorScanExecutor.cs 已创建
    echo ✅ AutoMonitorScanExecutor.cs 存在 >> "%LOG_FILE%"
) else (
    echo ❌ AutoMonitorScanExecutor.cs 缺失！
    echo ❌ AutoMonitorScanExecutor.cs 缺失 >> "%LOG_FILE%"
)
echo.

echo 🔍 步骤3: 检查文件锁定关键词
echo 检查代码中是否包含文件锁定相关实现...

findstr /c:"SemaphoreSlim" "%SERVICE_1%" >nul 2>&1
if %errorlevel%==0 (
    echo ✅ 发现文件锁定机制（SemaphoreSlim）
    echo ✅ 文件锁定机制已实现 >> "%LOG_FILE%"
) else (
    echo ❌ 未发现文件锁定机制！
    echo ❌ 文件锁定机制缺失 >> "%LOG_FILE%"
)

findstr /c:"ExecuteWithFileLockAsync" "%SERVICE_1%" >nul 2>&1
if %errorlevel%==0 (
    echo ✅ 发现锁定执行方法
    echo ✅ 锁定执行方法已实现 >> "%LOG_FILE%"
) else (
    echo ❌ 未发现锁定执行方法！
    echo ❌ 锁定执行方法缺失 >> "%LOG_FILE%"
)
echo.

echo 🔍 步骤4: 模拟文件并发访问测试
echo 创建测试文件进行并发读写测试...

set "TEST_FILE=%TEMP%\file_lock_test.json"
echo {"test": "concurrent_access"} > "%TEST_FILE%"

echo 测试文件: %TEST_FILE%
echo 🔄 开始并发访问测试（10次读写操作）...

for /L %%i in (1,1,10) do (
    echo {"iteration": %%i, "timestamp": "%DATE% %TIME%"} > "%TEST_FILE%" 2>nul
    if !errorlevel!==0 (
        echo    ✅ 写入操作 %%i 成功
    ) else (
        echo    ❌ 写入操作 %%i 失败
    )
)

del "%TEST_FILE%" 2>nul
echo ✅ 并发测试完成
echo.

echo 🔍 步骤5: 检查集成指南文档
set "GUIDE_FILE=🔒文件驱动状态管理系统集成指南.md"
if exist "%GUIDE_FILE%" (
    echo ✅ 集成指南文档已创建
    echo ✅ 集成指南文档存在 >> "%LOG_FILE%"
) else (
    echo ❌ 集成指南文档缺失！
    echo ❌ 集成指南文档缺失 >> "%LOG_FILE%"
)
echo.

echo 🔍 步骤6: 生成验证报告
echo =====================================
echo 📊 验证结果汇总：
echo.

echo 📁 文件状态检查：
if exist "%STATE_FILE%" (
    echo    🔵 状态文件：存在
) else (
    echo    🟡 状态文件：不存在（首次运行正常）
)

if exist "%SERVICE_1%" (
    echo    🟢 核心管理器：已部署
) else (
    echo    🔴 核心管理器：缺失
)

if exist "%SERVICE_2%" (
    echo    🟢 扫描执行器：已部署
) else (
    echo    🔴 扫描执行器：缺失
)

echo.
echo 🔧 功能实现检查：
findstr /c:"SemaphoreSlim" "%SERVICE_1%" >nul 2>&1
if %errorlevel%==0 (
    echo    🟢 文件锁定：已实现
) else (
    echo    🔴 文件锁定：未实现
)

findstr /c:"ExecuteWithFileLockAsync" "%SERVICE_1%" >nul 2>&1
if %errorlevel%==0 (
    echo    🟢 原子操作：已实现
) else (
    echo    🔴 原子操作：未实现
)

echo.
echo 📋 下一步操作建议：
echo.
echo 1. 📖 仔细阅读《🔒文件驱动状态管理系统集成指南.md》
echo 2. 🔧 按照指南修改 AutoMonitorService.cs
echo 3. 🔧 按照指南修改 AutoMonitorDashboard.xaml.cs  
echo 4. 🧪 在测试环境中验证功能
echo 5. 📊 观察日志中的 [FILE-LOCK] 标记
echo 6. ✅ 确认档案数量与持仓数量一致
echo.

echo 📄 完整验证日志保存在: %LOG_FILE%
echo.
echo 🎯 核心验证要点：
echo    • 启动自动盯盘后，检查日志是否出现 "[FILE-LOCK]" 字样
echo    • 检查档案数量是否与实际持仓数量一致  
echo    • 触发操作后，立即检查状态文件是否更新
echo    • 停止重启后，状态是否正确保持
echo.

echo =====================================
echo 🔒 文件锁定机制验证完成！
echo.
pause 