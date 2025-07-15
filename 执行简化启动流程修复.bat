@echo off
chcp 65001 >nul
echo.
echo ================================
echo 简化自动盯盘启动流程修复
echo ================================
echo.

REM 检查PowerShell脚本是否存在
if not exist "修复自动盯盘启动流程.ps1" (
    echo ❌ 错误：找不到 修复自动盯盘启动流程.ps1 文件
    pause
    exit /b 1
)

echo 🚀 正在执行修复脚本...
echo.

REM 执行PowerShell脚本
powershell -ExecutionPolicy Bypass -File "修复自动盯盘启动流程.ps1"

echo.
echo 🎉 修复执行完成！
echo.
pause 