@echo off
echo Checking config file format...
set "CONFIG_FILE=%APPDATA%\BinanceFuturesTrader\Global\auto_monitor_configs.json"

if exist "%CONFIG_FILE%" (
    echo File exists
    findstr "accountConfigs" "%CONFIG_FILE%" >nul
    if %errorlevel% equ 0 (
        echo OLD FORMAT detected - contains accountConfigs
        echo Need to convert to new format
    ) else (
        echo NEW FORMAT detected - array format
        echo No conversion needed
    )
) else (
    echo File does not exist
) 