@echo off
chcp 65001 > nul
echo.
echo 🔧 基础配置路径诊断脚本
echo ==========================================
echo.

echo 📋 问题描述：基础配置在启动的时候找不到
echo 💡 可能原因：装载基础配置的文件路径和保存路径有问题
echo.

echo 🔍 开始路径诊断...
echo ==========================================
echo.

set "BASE_DIR=%APPDATA%\BinanceFuturesTrader"
set "GLOBAL_DIR=%BASE_DIR%\Global"
set "CONFIG_FILE=%GLOBAL_DIR%\auto_monitor_configs.json"

echo 【步骤1】检查基础目录
echo ----------------------------------------
echo 基础目录: %BASE_DIR%
if exist "%BASE_DIR%" (
    echo ✅ 基础目录存在
    dir "%BASE_DIR%" | findstr /C:"<DIR>"
) else (
    echo ❌ 基础目录不存在！
    echo 💡 建议：手动创建目录或运行一次程序让它自动创建
)
echo.

echo 【步骤2】检查全局配置目录
echo ----------------------------------------
echo 全局配置目录: %GLOBAL_DIR%
if exist "%GLOBAL_DIR%" (
    echo ✅ 全局配置目录存在
    dir "%GLOBAL_DIR%" /B
) else (
    echo ❌ 全局配置目录不存在！
    echo 💡 正在创建全局配置目录...
    mkdir "%GLOBAL_DIR%" 2>nul
    if exist "%GLOBAL_DIR%" (
        echo ✅ 全局配置目录创建成功
    ) else (
        echo ❌ 全局配置目录创建失败，可能是权限问题
    )
)
echo.

echo 【步骤3】检查基础配置文件
echo ----------------------------------------
echo 配置文件路径: %CONFIG_FILE%
if exist "%CONFIG_FILE%" (
    echo ✅ 基础配置文件存在
    
    for %%A in ("%CONFIG_FILE%") do (
        echo 📄 文件大小: %%~zA 字节
        echo 📅 修改时间: %%~tA
        echo 📅 创建时间: %%~tA
    )
    
    echo.
    echo 📝 文件内容预览（前500字符）:
    echo ----------------------------------------
    powershell -Command "Get-Content '%CONFIG_FILE%' -Raw | ForEach-Object { if ($_.Length -gt 500) { $_.Substring(0, 500) + '...' } else { $_ } }"
    echo.
    
    echo 🔍 检查文件格式:
    echo ----------------------------------------
    findstr /C:"[" "%CONFIG_FILE%" >nul
    if %errorlevel% equ 0 (
        echo ✅ 检测到数组格式 "[" - 新格式
        findstr /C:"]" "%CONFIG_FILE%" >nul
        if %errorlevel% equ 0 (
            echo ✅ 检测到数组结束符 "]" - 格式完整
        ) else (
            echo ⚠️ 缺少数组结束符 "]" - 格式可能不完整
        )
    ) else (
        echo 🔄 未检测到数组格式，检查是否为旧格式...
        findstr /C:"accountConfigs" "%CONFIG_FILE%" >nul
        if %errorlevel% equ 0 (
            echo ⚠️ 检测到旧格式 "accountConfigs" - 需要格式转换
        ) else (
            echo ❌ 无法识别文件格式
        )
    )
    
) else (
    echo ❌ 基础配置文件不存在！
    echo.
    echo 🛠️ 可能的解决方案：
    echo ----------------------------------------
    echo 1. 文件从未被创建（用户还没有创建过基础配置）
    echo 2. 文件被意外删除
    echo 3. 保存到了不同的路径
    echo 4. 文件权限问题导致无法访问
    echo.
    echo 💡 建议操作：
    echo 1. 检查是否在其他位置有配置文件
    echo 2. 启动程序并尝试创建一个基础配置
    echo 3. 检查程序日志确认保存路径
)
echo.

echo 【步骤4】检查其他可能的配置文件位置
echo ----------------------------------------
echo 检查是否有其他位置的配置文件...

:: 检查账户目录
if exist "%BASE_DIR%\Accounts" (
    echo 📁 检查账户目录...
    for /d %%D in ("%BASE_DIR%\Accounts\*") do (
        echo   检查账户: %%~nxD
        if exist "%%D\auto_monitor_configs.json" (
            echo   ⚠️ 发现账户级配置文件: %%D\auto_monitor_configs.json
            echo   💡 这可能是错误保存到账户目录的基础配置
        )
        if exist "%%D\config.json" (
            echo   ℹ️ 发现其他配置文件: %%D\config.json
        )
    )
) else (
    echo ℹ️ 账户目录不存在
)

:: 检查根目录
if exist "%BASE_DIR%\config.json" (
    echo ⚠️ 发现根目录配置文件: %BASE_DIR%\config.json
)
if exist "%BASE_DIR%\auto_monitor_configs.json" (
    echo ⚠️ 发现根目录基础配置文件: %BASE_DIR%\auto_monitor_configs.json
    echo 💡 这个文件应该在Global子目录中
)
echo.

echo 【步骤5】检查文件权限
echo ----------------------------------------
echo 测试目录写权限...
set "TEST_FILE=%GLOBAL_DIR%\test_write.tmp"
echo test > "%TEST_FILE%" 2>nul
if exist "%TEST_FILE%" (
    echo ✅ 目录具有写权限
    del "%TEST_FILE%" 2>nul
) else (
    echo ❌ 目录缺少写权限！
    echo 💡 建议：以管理员身份运行程序，或检查目录权限
)
echo.

echo 【步骤6】路径诊断总结
echo ==========================================
echo.
echo 📋 诊断结果：
if exist "%CONFIG_FILE%" (
    echo ✅ 配置文件路径正确且文件存在
    echo 💡 如果程序仍然提示找不到配置，可能是：
    echo    1. 程序使用了不同的加载路径
    echo    2. 文件格式不兼容
    echo    3. 文件内容为空或损坏
    echo    4. 程序加载时机有问题
) else (
    echo ❌ 配置文件不存在于预期路径
    echo 💡 建议：
    echo    1. 启动程序并创建一个基础配置
    echo    2. 检查程序日志确认实际保存路径
    echo    3. 查看是否有权限问题
)
echo.

echo 🔧 修复建议
echo ==========================================
echo.
echo 如果配置文件不存在：
echo 1. 启动程序
echo 2. 打开基础配置管理窗口
echo 3. 创建一个新的基础配置
echo 4. 保存配置
echo 5. 重新运行此诊断脚本验证
echo.
echo 如果配置文件存在但程序找不到：
echo 1. 检查程序日志中的配置加载信息
echo 2. 确认BaseConfigManager使用的文件路径
echo 3. 检查文件格式是否正确
echo 4. 考虑删除文件让程序重新创建
echo.

echo 诊断完成。按任意键退出...
pause > nul 