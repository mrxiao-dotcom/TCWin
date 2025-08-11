@echo off
chcp 65001
echo 🔍 检查 contract_monitoring_states.json 文件

echo.
echo 📂 基础路径: %APPDATA%\BinanceFuturesTrader
echo 📂 账号目录: %APPDATA%\BinanceFuturesTrader\Accounts\Test
echo 📄 目标文件: %APPDATA%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json

set FILE_PATH=%APPDATA%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json

echo.
echo 🔍 检查文件是否存在...
if exist "%FILE_PATH%" (
    echo ✅ 文件存在
    echo.
    echo 📋 文件信息:
    dir "%FILE_PATH%" | findstr contract_monitoring_states.json
    echo.
    echo 📄 文件内容:
    type "%FILE_PATH%"
) else (
    echo ❌ 文件不存在！
    echo.
    echo 📂 检查目录是否存在:
    if exist "%APPDATA%\BinanceFuturesTrader\Accounts\Test\" (
        echo ✅ 账号目录存在
        echo 📋 目录内容:
        dir "%APPDATA%\BinanceFuturesTrader\Accounts\Test\"
    ) else (
        echo ❌ 账号目录也不存在！
        echo.
        echo 📂 检查基础目录:
        if exist "%APPDATA%\BinanceFuturesTrader\" (
            echo ✅ 基础目录存在
            echo 📋 基础目录内容:
            dir "%APPDATA%\BinanceFuturesTrader\"
        ) else (
            echo ❌ 基础目录也不存在！
        )
    )
)

echo.
echo 🔍 检查是否有其他账号目录:
if exist "%APPDATA%\BinanceFuturesTrader\Accounts\" (
    echo 📋 Accounts目录内容:
    dir "%APPDATA%\BinanceFuturesTrader\Accounts\"
) else (
    echo ❌ Accounts目录不存在
)

pause 