@echo off
chcp 65001 > nul
echo.
echo 🔧 持仓总品种数显示问题诊断脚本
echo ==========================================
echo.

echo 📋 问题描述：
echo "面板上：持仓总品种数始终为1"
echo.

echo 🔍 开始问题诊断...
echo.

echo 1. 检查主界面持仓数据源
echo ==========================================
echo 💡 主界面的持仓数据来自：MainViewModel.Positions
echo 💡 Test账户应该自动添加3个测试持仓：BTCUSDT、ETHUSDT、XRPUSDT
echo.

echo 2. 检查自动盯盘窗口数据源
echo ==========================================
echo 💡 自动盯盘窗口从以下位置获取持仓数据：
echo    - RefreshPositionDataAsync() 方法
echo    - 调用 _binanceService.GetPositionsAsync()
echo    - 过滤条件：activePositions = positions.Where(p => p.PositionAmt != 0)
echo.

echo 🚨 可能的问题原因：
echo ==========================================
echo.
echo 【原因1】API数据覆盖了测试数据
echo ----------------------------------------
echo • 问题：API返回的数据包含了同名合约，导致测试数据被跳过
echo • 位置：MainViewModel.Data.cs 第174行检查逻辑
echo • 代码：if (!existingSymbols.Contains(requiredPosition.Symbol))
echo • 结果：如果API返回BTCUSDT，则不会添加测试BTCUSDT
echo.

echo 【原因2】自动盯盘窗口使用了不同的数据源
echo ----------------------------------------
echo • 问题：自动盯盘窗口直接调用API，而不是读取主界面数据
echo • 位置：AutoMonitorConfigWindowSimple.xaml.cs RefreshPositionDataAsync()
echo • 代码：var positions = await _binanceService.GetPositionsAsync();
echo • 结果：无论主界面有多少测试数据，盯盘窗口只看API数据
echo.

echo 【原因3】过滤条件过于严格
echo ----------------------------------------
echo • 问题：PositionAmt != 0 的过滤条件可能过滤掉了某些持仓
echo • 位置：AutoMonitorConfigWindowSimple.xaml.cs 第741行
echo • 代码：var activePositions = positions.Where(p => p.PositionAmt != 0)
echo • 结果：如果API返回的持仓中只有1个PositionAmt != 0，就显示1
echo.

echo 🔧 修复方案
echo ==========================================
echo.
echo 【方案1】统一数据源 - 推荐
echo ----------------------------------------
echo • 修改自动盯盘窗口从MainViewModel读取持仓数据
echo • 确保测试数据在所有界面都可见
echo • 保持数据一致性
echo.

echo 【方案2】强制添加测试数据
echo ----------------------------------------
echo • 在自动盯盘窗口中也添加测试数据逻辑
echo • 确保Test账户始终显示3个持仓
echo • 防止API数据覆盖
echo.

echo 【方案3】改进过滤逻辑
echo ----------------------------------------
echo • 检查PositionAmt为0的持仓是否应该被计算
echo • 添加更详细的持仓分析日志
echo • 确保过滤条件正确
echo.

echo 🧪 验证步骤
echo ==========================================
echo.
echo 1. 检查主界面持仓数量
echo    - 启动程序，选择Test账户
echo    - 查看主界面持仓列表数量
echo    - 应该显示3个持仓（BTCUSDT, ETHUSDT, XRPUSDT）
echo.
echo 2. 检查自动盯盘窗口持仓数量
echo    - 打开自动盯盘窗口
echo    - 查看"持仓总品种数"显示
echo    - 检查日志中的持仓详情
echo.
echo 3. 查看详细日志
echo    - 查找"📡 正在调用Binance API获取持仓数据"
echo    - 查找"📊 API返回持仓总数"
echo    - 查找"🔍 活跃持仓过滤结果"
echo    - 查找每个持仓的详细信息
echo.

echo 💡 立即检查方法
echo ==========================================
echo 1. 启动程序
echo 2. 选择Test账户  
echo 3. 打开自动盯盘窗口
echo 4. 点击"刷新持仓数据"按钮
echo 5. 查看日志输出，特别关注：
echo    • API返回持仓总数是多少？
echo    • 活跃持仓（PositionAmt != 0）有几个？
echo    • 具体哪些持仓被认为是"活跃"的？
echo.

echo 🎯 预期诊断结果
echo ==========================================
echo • 如果API返回持仓总数 = 1，问题在API或测试数据添加
echo • 如果API返回持仓总数 > 1，但活跃持仓 = 1，问题在过滤条件
echo • 如果主界面显示3个，但盯盘窗口显示1个，问题在数据源不一致
echo.

echo 📋 收集诊断信息
echo ==========================================
echo 请在程序运行时收集以下信息：
echo 1. 主界面持仓列表显示的合约数量
echo 2. 自动盯盘窗口"持仓总品种数"的显示值  
echo 3. 程序日志中关于持仓数据的相关输出
echo 4. 是否看到"添加缺失的测试持仓"的日志
echo.

echo 🔧 下一步操作
echo ==========================================
echo 1. 先运行诊断收集信息
echo 2. 根据诊断结果确定具体原因
echo 3. 实施对应的修复方案
echo 4. 验证修复效果
echo.

echo 🎉 诊断脚本运行完成
echo ==========================================
echo 请按照上述步骤进行诊断，然后反馈具体的日志信息！
echo.
pause 