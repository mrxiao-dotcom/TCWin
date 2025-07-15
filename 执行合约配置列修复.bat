@echo off
chcp 65001 > nul
color 0A
title 合约配置8个数据列显示修复

echo.
echo =====================================
echo 🔧 合约配置8个数据列显示问题修复
echo =====================================
echo.

echo 📋 问题说明：
echo   • 当前表格只显示3列：启用、合约、浮盈
echo   • 缺少详细的配置列：保本条件、推仓1、推仓2、止盈1、止盈2等
echo   • 根因：默认配置的所有子配置都是IsEnabled=false
echo.

echo 🛠️ 修复方案：
echo   • 修改CreateDefaultAutoMonitorConfig方法
echo   • 将所有子配置的IsEnabled设为true
echo   • 为加仓和止盈配置添加示例阶梯数据
echo.

echo 📁 需要修改的文件：
echo   Views/AutoMonitorDashboard.xaml.cs
echo.

echo 🎯 预期修复效果：
echo   • 表格将显示8个配置列
echo   • 用户可以看到完整的配置结构
echo   • 显示示例配置数据供用户参考
echo.

echo ⚠️ 手动修复说明：
echo.
echo 📍 在Views/AutoMonitorDashboard.xaml.cs文件中，找到CreateDefaultAutoMonitorConfig方法
echo    （大约在第6323行附近）
echo.
echo 🔄 将以下内容：
echo.
echo    BreakEvenConfig = new AutoBreakEvenConfig
echo    {
echo        IsEnabled = false,  // ❌ 修改这里
echo        TriggerProfitAmount = 50m
echo    },
echo.
echo ✅ 修改为：
echo.
echo    BreakEvenConfig = new AutoBreakEvenConfig
echo    {
echo        IsEnabled = true,   // ✅ 改为true
echo        TriggerProfitAmount = 50m
echo    },
echo.
echo 🔄 将以下内容：
echo.
echo    AddPositionConfig = new AutoAddPositionConfig
echo    {
echo        IsEnabled = false,  // ❌ 修改这里
echo        Tiers = new List^<AddPositionTier^>()  // ❌ 修改这里
echo    },
echo.
echo ✅ 修改为：
echo.
echo    AddPositionConfig = new AutoAddPositionConfig
echo    {
echo        IsEnabled = true,   // ✅ 改为true
echo        Tiers = new List^<AddPositionTier^>
echo        {
echo            new AddPositionTier
echo            {
echo                TierIndex = 1,
echo                TriggerProfitAmount = 100m,
echo                RiskMultiplier = 1.5m,
echo                StopLossRatio = 0.02m
echo            },
echo            new AddPositionTier
echo            {
echo                TierIndex = 2,
echo                TriggerProfitAmount = 200m,
echo                RiskMultiplier = 2.0m,
echo                StopLossRatio = 0.03m
echo            }
echo        }
echo    },
echo.
echo 🔄 将以下内容：
echo.
echo    ProfitProtectionConfig = new AutoProfitProtectionConfig
echo    {
echo        IsEnabled = false,  // ❌ 修改这里
echo        Tiers = new List^<ProfitProtectionTier^>()  // ❌ 修改这里
echo    }
echo.
echo ✅ 修改为：
echo.
echo    ProfitProtectionConfig = new AutoProfitProtectionConfig
echo    {
echo        IsEnabled = true,   // ✅ 改为true
echo        Tiers = new List^<ProfitProtectionTier^>
echo        {
echo            new ProfitProtectionTier
echo            {
echo                TierIndex = 1,
echo                TriggerProfitAmount = 300m,
echo                ProtectionAmount = 150m
echo            },
echo            new ProfitProtectionTier
echo            {
echo                TierIndex = 2,
echo                TriggerProfitAmount = 500m,
echo                ProtectionAmount = 250m
echo            }
echo        }
echo    }
echo.

echo 🚀 修改完成后的操作：
echo   1. 保存文件
echo   2. 编译项目：dotnet build
echo   3. 启动程序测试
echo   4. 打开自动盯盘面板
echo   5. 点击【加载配置】按钮
echo   6. 确认表格显示8个配置列
echo.

echo 🔍 验证结果：
echo   • 基础列：启用、合约、浮盈（3列）
echo   • 配置列：保本条件、推仓1、推仓2、止盈1、止盈2（5列）
echo   • 总计：8列
echo.

echo 现在请手动修改代码文件，然后进行编译测试。
echo.
echo 按任意键继续编译测试...
pause > nul

echo.
echo 🚀 开始编译测试...
dotnet build BinanceFuturesTrader.csproj --configuration Release

if %errorlevel% equ 0 (
    echo.
    echo ✅ 编译成功！现在可以启动程序测试了
    echo.
    echo 🎯 测试步骤：
    echo   1. 运行：dotnet run --configuration Release
    echo   2. 打开自动盯盘面板
    echo   3. 点击【加载配置】按钮
    echo   4. 观察表格列数是否从3列变为8列
    echo.
) else (
    echo.
    echo ❌ 编译失败，请检查代码修改是否正确
    echo.
)

echo 按任意键结束...
pause > nul 