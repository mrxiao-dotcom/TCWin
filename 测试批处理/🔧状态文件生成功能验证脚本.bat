@echo off
chcp 65001 > nul
echo 🔧 状态文件生成功能验证脚本
echo.

echo 📋 测试目标：
echo   验证用户确认后是否正确生成 contract_monitoring_states.json 文件
echo   并从该文件加载数据显示到UI
echo.

echo 🧹 步骤1: 清理所有配置文件...
if exist "%APPDATA%\BinanceFuturesTrader" (
    rmdir /s /q "%APPDATA%\BinanceFuturesTrader"
    echo ✅ 已清理整个BinanceFuturesTrader配置目录
) else (
    echo ℹ️ 配置目录不存在，跳过清理
)

echo.
echo 🔨 步骤2: 编译项目...
dotnet build BinanceFuturesTrader.csproj --verbosity quiet
if %ERRORLEVEL% == 0 (
    echo ✅ 项目编译成功
) else (
    echo ❌ 项目编译失败
    pause
    exit /b 1
)

echo.
echo 🚀 步骤3: 手动验证测试...
echo.
echo 📋 【关键修复内容】：
echo.
echo ✅ 修复前的问题：
echo   - 用户确认生成状态文件后，只显示提示信息
echo   - 实际上没有生成 contract_monitoring_states.json 文件
echo   - 显示"状态文件生成功能需要完善，当前显示为空"
echo.
echo ✅ 修复后的功能：
echo   - 添加了真正的 GenerateStateFileFromPositions() 方法
echo   - 添加了真正的 LoadContractConfigsFromStateFile() 方法  
echo   - 添加了 PopulateConfigFromState() 方法填充UI数据
echo   - 修复了方法重复定义和只读属性问题
echo.
echo 📊 【详细验证步骤】：
echo.
echo 【测试1：状态文件生成验证】
echo   1. 确保您有活跃持仓（BTCUSDT等）
echo   2. 启动程序，点击"启动盯盘面板"按钮
echo   3. 应该弹出对话框："检测到 X 个活跃持仓，是否生成状态文件？"
echo   4. 点击"是"
echo   5. 查看日志，应该显示：
echo      - "🔄 开始为 X 个持仓生成状态文件..."
echo      - "📋 使用基础配置: XXX"
echo      - "🔄 为合约 BTCUSDT_LONG 生成监控状态"
echo      - "✅ 成功生成状态文件，包含 X 个合约配置"
echo      - "📊 开始从状态文件加载合约配置..."
echo      - "✅ 已从状态文件加载 X 个合约配置到UI"
echo.
echo 【测试2：文件实际存在验证】
echo   1. 导航到目录：%%APPDATA%%\BinanceFuturesTrader\Accounts\Test\
echo   2. 检查是否存在 contract_monitoring_states.json 文件
echo   3. 打开文件，验证内容是否包含：
echo      - "Symbol": "BTCUSDT"
echo      - "PositionSide": "LONG" 或 "SHORT"
echo      - "BreakEvenConfig": {...}
echo      - "AddPositionConfig": {...}
echo      - "ProfitProtectionConfig": {...}
echo.
echo 【测试3：UI数据验证】
echo   1. 检查合约配置区域是否显示数据
echo   2. 验证合约名称：应该显示为 "BTCUSDT LONG"
echo   3. 验证保本目标：应该显示为基础配置中的 TriggerProfitAmount
echo   4. 验证推仓和保盈数据：应该从状态文件中正确读取
echo.
echo 【测试4：重新打开验证】
echo   1. 关闭盯盘面板
echo   2. 重新点击"启动盯盘面板"
echo   3. 应该显示："✅ 找到状态文件，从文件加载数据..."
echo   4. 不应该再弹出对话框
echo   5. 直接从文件加载并显示数据
echo.

echo 🎯 【预期的完整日志流程】：
echo.
echo [时间] 🔄 检查合约监控状态文件...
echo [时间] 📁 状态文件路径: C:\Users\...\contract_monitoring_states.json
echo [时间] 📝 状态文件不存在，检查是否需要生成...
echo [时间] ✅ 用户确认生成状态文件，正在处理 1 个持仓...
echo [时间] 🔄 开始为 1 个持仓生成状态文件...
echo [时间] 📋 使用基础配置: 新配置
echo [时间] 🔄 为合约 BTCUSDT_LONG 生成监控状态
echo [时间] ✅ 成功生成状态文件，包含 1 个合约配置
echo [时间] 📊 开始从状态文件加载合约配置...
echo [时间] 📊 从状态文件加载到 1 个合约配置
echo [时间] 🔄 已转换合约配置: BTCUSDT_LONG
echo [时间] ✅ 已从状态文件加载 1 个合约配置到UI
echo.

echo 🎉 关键修复总结：
echo.
echo ✅ 问题：用户确认后没有实际生成文件
echo ✅ 修复：实现了真正的状态文件生成逻辑
echo ✅ 问题：没有从文件加载UI数据
echo ✅ 修复：实现了真正的状态文件加载逻辑
echo ✅ 问题：编译错误（重复方法定义）
echo ✅ 修复：删除重复方法，修复只读属性问题
echo.
echo 现在请按照上述步骤手动验证修复效果！
echo 特别关注：contract_monitoring_states.json 文件是否真的生成了！
echo.
pause 