@echo off
chcp 65001 >nul
echo ========================================
echo 🎯 全部问题修复验证 - 最终测试
echo ========================================
echo.

echo 📋 本次修复内容：
echo   1. ✅ 双击编辑保存异常修复（contractState为null）
echo   2. ✅ 保盈2、3档列强制显示修复  
echo   3. ✅ 推仓1-4档列强制显示修复
echo   4. ✅ ETH浮盈数据匹配修复
echo   5. ✅ 数据保护机制已完善
echo.

echo 🔧 验证步骤：
echo.

echo 【第1步】检查编译状态
findstr /i "error" *.log >nul 2>&1
if %errorlevel%==0 (
    echo   ❌ 发现编译错误，请先解决
    goto :end
) else (
    echo   ✅ 编译无错误
)
echo.

echo 【第2步】检查关键修复代码
echo.
echo   📍 检查保存异常修复：
findstr /c:"if (!allStates.TryGetValue(contractKey, out var contractState))" Views\ContractConfigEditDialog.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 保存异常修复已应用
) else (
    echo   ❌ 保存异常修复未找到
)

echo.
echo   📍 检查UI强制列生成：
findstr /c:"强制生成3个保盈列" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 保盈列强制生成修复已应用
) else (
    echo   ❌ 保盈列强制生成修复未找到
)

findstr /c:"强制生成4个推仓列" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 推仓列强制生成修复已应用
) else (
    echo   ❌ 推仓列强制生成修复未找到
)

echo.
echo   📍 检查浮盈匹配修复：
findstr /c:"修复匹配逻辑" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 浮盈匹配修复已应用
) else (
    echo   ❌ 浮盈匹配修复未找到
)

echo.
echo   📍 检查数据保护机制：
findstr /c:"isManualChange=true" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 数据保护机制已完善
) else (
    echo   ❌ 数据保护机制未找到
)

echo.
echo 【第3步】检查补齐档位逻辑
findstr /c:"补齐档位" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 档位补齐逻辑已实现
) else (
    echo   ❌ 档位补齐逻辑未找到
)

echo.
echo ========================================
echo 🚀 测试指南
echo ========================================
echo.
echo 【重要】请按以下步骤进行功能测试：
echo.
echo 1️⃣ 启动程序测试：
echo    - 重启程序
echo    - 选择Test账户
echo    - 点击"自动盯盘"按钮
echo.
echo 2️⃣ UI显示验证：
echo    - 检查是否显示3个合约（BTC, ETH, XRP）
echo    - 检查推仓列是否显示4列（推仓1-4档）
echo    - 检查保盈列是否显示3列（保盈1-3档）
echo    - 检查ETH浮盈是否显示1000.00U
echo    - 检查XRP浮盈是否显示-100.00U
echo.
echo 3️⃣ 编辑保存测试：
echo    - 双击任一合约行
echo    - 修改保本状态为"√"
echo    - 点击保存按钮
echo    - 应该不再报异常
echo.
echo 4️⃣ 关键日志验证：
echo    应该看到以下日志：
echo    [🔧【强制列生成】已生成推仓1档列，绑定DynamicPush1]
echo    [🔧【强制列生成】已生成保盈1档列，绑定DynamicProfit1]
echo    [🎯【UI修复】已强制生成4个推仓列，确保UI显示完整]
echo    [🎯【UI修复】已强制生成3个保盈列，确保UI显示完整]
echo    [🔍 【持仓匹配】匹配结果: 找到]
echo    [🔧【补齐档位】保盈2设置默认显示: '1900 | 1520 -']
echo.
echo ⚠️  如果仍有问题，请提供：
echo    1. 具体的错误信息或异常堆栈
echo    2. UI界面截图
echo    3. 程序运行日志（特别是上述关键日志）
echo.

:end
echo ========================================
echo 验证脚本执行完成
echo ========================================
pause 