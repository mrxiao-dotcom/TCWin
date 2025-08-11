@echo off
chcp 65001 > nul
echo.
echo ==========================================
echo 🔧 清理废弃的ContractConfigs.json文件
echo ==========================================
echo.

echo 📋 说明：
echo   ContractConfigs.json 文件已废弃，现在使用统一状态管理
echo   新的数据保存在 contract_monitoring_states.json 文件中
echo.

echo 🔍 查找需要清理的文件...

set "APPDATA_PATH=%APPDATA%\BinanceFuturesTrader"
set "FOUND_FILES=0"

echo.
echo 📂 搜索路径: %APPDATA_PATH%
echo.

if exist "%APPDATA_PATH%" (
    echo ✅ 找到BinanceFuturesTrader数据目录
    
    REM 查找所有ContractConfigs.json文件
    for /r "%APPDATA_PATH%" %%f in (ContractConfigs.json) do (
        if exist "%%f" (
            set /a FOUND_FILES+=1
            echo 📄 发现文件: %%f
            
            REM 显示文件大小和修改时间
            for %%A in ("%%f") do (
                echo   📊 大小: %%~zA 字节
                echo   📅 修改时间: %%~tA
            )
            echo.
        )
    )
    
    if %FOUND_FILES% GTR 0 (
        echo.
        echo ⚠️ 发现 %FOUND_FILES% 个废弃的ContractConfigs.json文件
        echo.
        choice /C YN /M "是否删除这些废弃文件? (Y=是, N=否)"
        
        if errorlevel 2 (
            echo.
            echo 💡 用户选择保留文件，脚本退出
            goto :end
        )
        
        if errorlevel 1 (
            echo.
            echo 🗑️ 开始删除废弃文件...
            
            for /r "%APPDATA_PATH%" %%f in (ContractConfigs.json) do (
                if exist "%%f" (
                    del "%%f" 2>nul
                    if exist "%%f" (
                        echo ❌ 删除失败: %%f
                    ) else (
                        echo ✅ 已删除: %%f
                    )
                )
            )
            
            echo.
            echo 🎉 清理完成！
        )
    ) else (
        echo ✅ 未发现废弃的ContractConfigs.json文件
    )
) else (
    echo ❌ 未找到BinanceFuturesTrader数据目录
    echo 💡 这表明程序还没有创建过数据文件，无需清理
)

echo.
echo ==========================================
echo 📋 当前使用的文件结构：
echo ==========================================
echo.
echo 📁 基础配置文件：
echo   %APPDATA%\BinanceFuturesTrader\Global\BaseConfigs.json
echo.
echo 📁 账户专属监控状态文件：
echo   %APPDATA%\BinanceFuturesTrader\[账户名]\contract_monitoring_states.json
echo.
echo 💡 说明：
echo   • BaseConfigs.json - 存储基础配置模板（保本、推仓、保盈参数）
echo   • contract_monitoring_states.json - 存储每个合约的实时状态和执行记录
echo   • 不再使用 ContractConfigs.json 文件

:end
echo.
pause 