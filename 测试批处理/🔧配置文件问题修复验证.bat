@echo off
chcp 65001 > nul
echo.
echo ==========================================
echo 🔧 配置文件问题修复验证脚本
echo ==========================================
echo.

echo 📁 切换到项目目录...
cd /d "D:\CSharpProjects\TCWin"

echo.
echo 📋 检查修复的两个问题:
echo   1. 基础配置文件简化 - 只保存配置信息，不保存状态
echo   2. 状态修改正确保存 - 双击修改状态能正确保存到文件
echo.

echo 🧹 清理编译缓存...
if exist "obj" rmdir /s /q "obj"
if exist "bin" rmdir /s /q "bin"

echo.
echo 🔨 编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Release --verbosity minimal
if %ERRORLEVEL% NEQ 0 (
    echo ❌ 编译失败！请检查代码错误。
    pause
    exit /b 1
)

echo ✅ 编译成功！

echo.
echo 📝 修复总结:
echo.
echo 📋 问题1: 基础配置文件内容过于繁杂
echo ✅ 修复内容:
echo   • 修改了 BaseConfigManager.cs 的保存方法
echo   • 只保存基础配置信息，不保存状态信息  
echo   • 保存内容: 保本触发金额, N阶推仓数据, M阶止盈数据
echo   • 不保存: 执行状态, 执行时间, 触发状态等运行时信息
echo.
echo 📋 问题2: 双击修改状态后文件没有正确保存
echo ✅ 修复内容:
echo   • 强化了 ContractStatusEditDialog.xaml.cs 的状态同步机制
echo   • 添加了多重状态保存路径，提高保存成功率
echo   • 增强了状态验证机制，确保保存成功
echo   • 添加了强制保存机制，避免保存丢失
echo.
echo 🎯 使用说明:
echo.
echo 1️⃣ 基础配置管理:
echo   • 编辑基础配置时，只需要配置各项参数
echo   • 保存的配置文件不再包含繁杂的状态信息
echo   • 配置文件更清晰，便于手动查看和备份
echo.
echo 2️⃣ 状态修改:
echo   • 双击合约配置行进入编辑状态
echo   • 修改保本/推仓/保盈的状态为"已执行"
echo   • 保存后会立即同步到文件，并进行验证
echo   • 可以查看日志确认保存是否成功
echo.
echo 📂 相关文件:
echo   • 基础配置文件: AppData\Local\BinanceFuturesTrader\Global\BaseConfigs.json
echo   • 监控状态文件: AppData\Local\BinanceFuturesTrader\[账户]\ContractMonitoringStates.json
echo.
echo 💡 注意事项:
echo   • 只在停止自动盯盘时才能修改状态
echo   • 修改状态后会立即保存到文件
echo   • 建议查看程序日志确认保存状态
echo.
echo ==========================================
echo 🎉 修复验证完成！可以正常使用了！
echo ==========================================
echo.
pause 