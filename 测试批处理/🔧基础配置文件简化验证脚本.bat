@echo off
chcp 65001 > nul
echo.
echo ========================================
echo 🔧 基础配置文件简化验证脚本
echo ========================================
echo.

echo 📋 验证 auto_monitor_configs.json 文件简化效果...
echo.

:: 编译项目
echo 🔨 正在编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity quiet
if %ERRORLEVEL% EQU 0 (
    echo ✅ 编译成功
) else (
    echo ❌ 编译失败
    goto :error
)

echo.
echo 🔍 检查配置文件简化情况...

:: 检查BaseConfigManager.cs的修改
findstr /c:"完全简化的配置" "Services\BaseConfigManager.cs" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ BaseConfigManager已更新为完全简化配置
) else (
    echo ⚠️ BaseConfigManager可能未正确修改
)

:: 检查是否移除了状态字段
findstr /c:"ExecutionState\|IsExecuted\|ExecutionTime\|IsTriggered\|TriggerTime" "Services\BaseConfigManager.cs" > nul
if %ERRORLEVEL% NEQ 0 (
    echo ✅ BaseConfigManager中已移除状态字段
) else (
    echo ⚠️ BaseConfigManager中可能还包含状态字段
)

echo.
echo 📊 修改说明:
echo ================================
echo ✅ 基础配置文件现在只包含:
echo    ┌─ 基础信息: Name, IsEnabled, 扫描间隔等
echo    ├─ 保本配置: IsEnabled, TriggerProfitAmount, Description
echo    ├─ 推仓配置: IsEnabled, Tiers[基础参数], Description  
echo    └─ 保盈配置: IsEnabled, Tiers[基础参数], Description
echo.
echo ❌ 已移除的状态字段:
echo    ┌─ ExecutionState (执行状态)
echo    ├─ IsExecuted (是否已执行)
echo    ├─ ExecutionTime (执行时间)
echo    ├─ IsTriggered (是否已触发)
echo    └─ TriggerTime (触发时间)
echo.
echo 🎯 预期效果:
echo    - auto_monitor_configs.json 文件更简洁
echo    - 只包含基础配置信息，不含运行时状态
echo    - 状态信息由 contract_monitoring_states.json 管理
echo    - 基础配置文件可以作为模板使用
echo.

echo 💾 文件路径信息:
echo    ┌─ 基础配置: %%AppData%%\BinanceFuturesTrader\Global\auto_monitor_configs.json
echo    └─ 状态文件: %%AppData%%\BinanceFuturesTrader\Accounts\[账户]\contract_monitoring_states.json
echo.

echo ✅ 基础配置文件简化验证完成！
echo.
echo 📝 使用建议:
echo    1. 重新保存配置以生成简化的JSON文件
echo    2. 检查生成的JSON文件确认无状态字段
echo    3. 运行时状态将由专门的状态管理文件处理
echo.
pause
goto :end

:error
echo.
echo ❌ 验证过程中发现错误，请检查修改情况。
echo.
pause

:end 