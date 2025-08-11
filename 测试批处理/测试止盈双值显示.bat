@echo off
chcp 65001 >nul
echo ============================
echo ✅ 验证止盈条件双值显示修复
echo ============================
echo.
echo 🎯 本次修复内容：
echo   • 修复了LoadContractMonitorsFromService方法
echo   • 为止盈条件添加了KeepValue属性设置
echo   • 止盈条件现在会正确显示触发值和保留值
echo.
echo 📋 测试步骤：
echo   1. 启动程序，设置API账号
echo   2. 进入自动盯盘配置，设置止盈参数
echo   3. 配置多个止盈阶梯，每个设置不同的触发值和保留值
echo   4. 保存配置后，打开监控面板
echo   5. 查看止盈条件列
echo.
echo ✅ 应该看到的效果：
echo   止盈条件列现在应该有3列：
echo     • 🎯 止盈X触发值：显示浮盈达到多少时触发
echo     • 💰 止盈X保留值：显示触发时保留多少盈利
echo     • 📊 止盈X状态：显示未触发/已执行状态
echo.
echo   例如配置：触发1000U，保留800U
echo     • 触发值列：显示 "1000.00"
echo     • 保留值列：显示 "800.00"
echo     • 状态列：显示 "未触发"（绿色）
echo.
echo 🔧 验证重点：
echo   • 每个止盈阶梯都有触发值和保留值两列
echo   • 保留值列不再为空
echo   • 数值显示格式正确（小数位）
echo   • 止盈条件用绿色边框区分
echo.
echo ⚠️ 修复前的问题：
echo   • 保留值列显示为空
echo   • 只有触发值和状态，缺少保留值
echo.
echo ============================
echo 🚀 启动程序测试
echo ============================
echo.
start "" "bin\Debug\net8.0-windows\TCWin.exe"
echo ✅ 程序已启动，请按照上述步骤测试
echo 🔍 重点检查：止盈条件的保留值列是否正确显示数值
echo.
pause 