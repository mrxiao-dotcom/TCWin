@echo off
chcp 65001 >nul
echo 🔧 统一数据源修复验证脚本
echo =====================================
echo.

echo 📋 本次修复的核心目标：
echo    • 确保所有UI组件只从统一状态文件读取数据
echo    • 消除多数据源导致的状态不一致问题
echo    • 实现"文件是唯一的数据源"
echo.

echo 🔍 验证步骤：
echo.

echo 步骤1: 检查统一数据服务是否创建
if exist "Services\UnifiedStateDataService.cs" (
    echo ✅ UnifiedStateDataService.cs 已创建
) else (
    echo ❌ UnifiedStateDataService.cs 未找到
)

findstr /n "GetAllContractStates" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 统一数据访问方法已实现
) else (
    echo ❌ 统一数据访问方法缺失
)
echo.

echo 步骤2: 检查后门路径是否已移除
findstr /n "_binanceService.GetPositionsAsync" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍存在Binance API后门路径
) else (
    echo ✅ Binance API后门路径已移除
)

findstr /n "统一状态文件不存在，需要先生成状态文件" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 后门路径已替换为友好提示
) else (
    echo ❌ 后门路径提示缺失
)
echo.

echo 步骤3: 检查UI组件是否使用统一数据服务
findstr /n "UnifiedStateDataService" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ AutoMonitorConfigWindowSimple已使用统一数据服务
) else (
    echo ❌ AutoMonitorConfigWindowSimple未使用统一数据服务
)

findstr /n "UnifiedStateDataService" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ AutoMonitorDashboard已使用统一数据服务
) else (
    echo ❌ AutoMonitorDashboard未使用统一数据服务
)
echo.

echo 步骤4: 检查日志标识是否正确
findstr /n "【统一数据源】" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 统一数据源日志标识已添加
) else (
    echo ❌ 统一数据源日志标识缺失
)
echo.

echo 步骤5: 查找可能遗漏的多数据源调用
echo.
echo 🔍 搜索可能的问题代码模式...

findstr /n "GetPositionProfiles" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ⚠️ 发现PositionProfiles调用，可能需要替换
) else (
    echo ✅ 无PositionProfiles后门调用
)

findstr /n "stateService.LoadMonitoringStates" "Views\AutoMonitorConfigWindowSimple.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ⚠️ 发现直接的LoadMonitoringStates调用，可能需要替换
) else (
    echo ✅ 无直接LoadMonitoringStates调用
)
echo.

echo 📊 测试建议：
echo =====================================
echo.
echo 🚀 启动测试步骤：
echo 1. 删除或重命名现有的状态文件（测试文件不存在的情况）
echo    位置: %%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo.
echo 2. 启动应用程序并打开自动盯盘配置窗口
echo.
echo 3. 观察行为是否符合预期：
echo    ✅ 应该看到友好提示对话框
echo    ✅ 不应该自动从Binance API获取数据显示
echo    ✅ 界面应该保持空白，等待用户操作
echo.
echo 4. 点击"从当前持仓生成"按钮
echo.
echo 5. 观察生成后的行为：
echo    ✅ 应该生成统一状态文件
echo    ✅ 应该从新生成的文件加载界面
echo    ✅ 应该看到【统一数据源】的日志标识
echo.

echo 📝 期望的日志输出：
echo =====================================
echo.
echo ✅ 正常情况应该看到：
echo    📊 【统一数据源】开始从统一状态文件加载合约配置...
echo    📁 状态文件路径: C:\Users\...\contract_monitoring_states.json  
echo    📁 状态文件存在: True/False
echo    📊 【统一数据源】从状态文件加载到 X 个合约配置
echo.
echo ❌ 不应该看到：
echo    - 直接从Binance API获取持仓的日志
echo    - 多种数据源混合调用的日志
echo    - 状态被意外重置的警告
echo.

echo 🔧 故障排除：
echo =====================================
echo.
echo 如果测试不符合预期，请检查：
echo.
echo 1. 📁 文件结构检查：
echo    - Services\UnifiedStateDataService.cs 是否存在
echo    - 是否包含GetAllContractStates方法
echo.
echo 2. 🔧 代码集成检查：
echo    - Views\AutoMonitorConfigWindowSimple.xaml.cs 是否已修改
echo    - Views\AutoMonitorDashboard.xaml.cs 是否已修改
echo.
echo 3. 📝 编译验证：
echo    - 项目是否能正常编译
echo    - 是否有UnifiedStateDataService相关的编译错误
echo.

echo 4. 🧪 运行时验证：
echo    - 启动应用是否正常
echo    - 是否出现统一数据服务相关的运行时异常
echo.

echo 📊 成功指标：
echo =====================================
echo.
echo 如果修复成功，您应该观察到：
echo.
echo ✅ 数据一致性：
echo    • 界面显示与状态文件内容完全一致
echo    • 执行状态不会被意外重置
echo    • 配置信息准确反映文件内容
echo.
echo ✅ 行为可预测性：
echo    • 没有状态文件时，界面保持空白
echo    • 有状态文件时，准确加载文件内容
echo    • 刷新操作不会改变执行状态
echo.
echo ✅ 日志清晰性：
echo    • 所有日志都标明【统一数据源】
echo    • 明确显示数据来源路径
echo    • 操作过程完全可追踪
echo.

echo 🎯 架构验证：
echo =====================================
echo.
echo 理想的数据流应该是：
echo.
echo 📁 contract_monitoring_states.json (唯一数据源)
echo      ↓
echo 🎯 UnifiedStateDataService (统一访问接口)
echo      ↓
echo 📊 所有UI组件 (一致的数据显示)
echo.
echo 不应该存在：
echo ❌ Binance API → UI (绕过状态文件)
echo ❌ PositionProfiles → UI (绕过状态文件)
echo ❌ 基础配置 → UI (绕过状态文件)
echo.

echo =====================================
echo 🔧 统一数据源验证完成！
echo.
echo 📞 如果发现问题：
echo • 检查上述验证要点
echo • 查看详细的日志输出
echo • 确认状态文件的内容和结构
echo • 验证UI操作是否符合预期行为
echo.
pause 