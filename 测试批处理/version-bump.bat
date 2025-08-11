@echo off
REM ===================================================================
REM 版本升级快捷批处理 - version-bump.bat
REM 功能：快速调用PowerShell版本管理脚本
REM ===================================================================

chcp 65001 >nul
echo.
echo 🚀 币安期货交易管理器 - 版本管理工具
echo ═══════════════════════════════════════════════════════════════════

if "%1"=="" (
    echo 💡 使用方法：
    echo    version-bump patch      [默认] 修订版本 ^(0.32.0 → 0.32.1^)
    echo    version-bump minor      次版本   ^(0.32.0 → 0.33.0^)
    echo    version-bump major      主版本   ^(0.32.0 → 1.0.0^)
    echo    version-bump -i         交互式模式 ^(输入更新内容^)
    echo.
    echo 📝 示例：
    echo    version-bump patch "修复自动盯盘bug"
    echo    version-bump minor "新增监控面板功能"
    echo    version-bump major "架构重构"
    echo.
    
    set /p choice="请选择版本类型 [patch/minor/major] (默认patch): "
    if "%choice%"=="" set choice=patch
    
    echo.
    echo 🔄 执行版本升级：%choice%
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType "%choice%" -Interactive
) else if "%1"=="-i" (
    echo 🔄 启动交互式版本管理...
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -Interactive
) else if "%1"=="-h" (
    echo 💡 使用方法：
    echo    version-bump patch      [默认] 修订版本 ^(0.32.0 → 0.32.1^)
    echo    version-bump minor      次版本   ^(0.32.0 → 0.33.0^)
    echo    version-bump major      主版本   ^(0.32.0 → 1.0.0^)
    echo    version-bump -i         交互式模式 ^(输入更新内容^)
    echo    version-bump -h         显示帮助
) else (
    set versionType=%1
    set updateMessage=%2
    
    if "%updateMessage%"=="" (
        echo 🔄 执行版本升级：%versionType%
        powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType "%versionType%"
    ) else (
        echo 🔄 执行版本升级：%versionType% 
        echo 📝 更新说明：%updateMessage%
        powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType "%versionType%" -UpdateMessage "%updateMessage%"
    )
)

echo.
echo ✅ 版本管理完成！
pause 