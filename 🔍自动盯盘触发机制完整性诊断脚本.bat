@echo off
chcp 65001 > nul
echo 🔍 自动盯盘触发机制完整性诊断脚本
echo ================================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 诊断时间: %datestamp% %timestamp%
echo.

echo 🎯 自动盯盘触发机制检查清单
echo.

echo 📋 完整的工作流程应该是：
echo   1️⃣ 定时器触发扫描 (每5秒)
echo   2️⃣ 获取持仓数据并匹配配置  
echo   3️⃣ 检查浮盈条件是否满足
echo   4️⃣ 执行相应操作 (保本/推仓/保盈)
echo   5️⃣ 更新执行状态为"已执行" 
echo   6️⃣ 保存到文件并通知界面刷新
echo.

echo 🔍 关键检查点:
echo.

echo ✅ 第1步：定时器触发
echo   位置: Services/AutoMonitorService.cs - ScanPositionsAsync()
echo   日志: "⏰ 定时器触发，开始扫描"
echo   验证: 每5秒应该看到这个日志
echo.

echo ✅ 第2步：持仓匹配
echo   位置: Services/AutoMonitorExecutionEngine.cs - ExecuteContractMonitoringAsync()
echo   日志: "🔍【浮盈比对】XXXUSDT_BOTH: 当前浮盈 X.XXU"
echo   验证: 每个活跃持仓都应该被检查
echo.

echo ✅ 第3步：条件检查
echo   保本条件: "🔍【浮盈比对-保本】XXXUSDT: 浮盈X.XXU vs 触发条件X.XXU"
echo   推仓条件: "🔍【浮盈比对-推仓】XXXUSDT 阶梯X: 浮盈X.XXU vs 触发条件X.XXU"
echo   保盈条件: "🔍【浮盈比对-保盈】XXXUSDT 阶梯X: 浮盈X.XXU vs 触发条件X.XXU"
echo   验证: 应该看到具体的数值比较
echo.

echo ✅ 第4步：执行操作
echo   触发日志: "✅【XXX触发】XXXUSDT: X.XXU >= X.XXU，开始执行"
echo   执行日志: "🚀 【XXX执行-引擎】XXXUSDT 开始执行XXX"
echo   结果日志: "✅ 【XXX结果-引擎】XXXUSDT 成功"
echo   验证: 应该看到完整的执行过程
echo.

echo ✅ 第5步：状态更新
echo   位置: Services/AutoMonitorExecutionEngine.cs - 各个ProcessXXXLogicAsync方法
echo   关键日志: "✅【状态更新】XXXUSDT 开始更新XXX状态"
echo   文件更新: "✅ UpdateExecutionStatus调用成功: XXXUSDT_BOTH"
echo   验证: 文件中ExecutionState应该变为"Executed"
echo.

echo ✅ 第6步：界面通知
echo   事件发布: "📤 XXX状态更新事件已发送: XXXUSDT 阶梯X"
echo   界面刷新: "🚀【触发状态更新事件】XXXUSDT_BOTH 调用NotifyStatusUpdated"
echo   验证: 界面状态应该立即更新
echo.

echo 🚨 常见问题诊断:
echo.

echo 问题1: 定时器不触发
echo   原因: 服务未启动或配置错误
echo   检查: 确认"启动盯盘"按钮已点击，状态显示"运行中"
echo   日志: 应该每5秒看到"⏰ 定时器触发，开始扫描"
echo.

echo 问题2: 条件满足但不执行
echo   原因: 状态检查阻止了执行（已执行过）
echo   检查: 查看"🔍【XXX状态检查】"日志
echo   验证: Config.IsExecuted, StateManager.IsExecuted, UnifiedFile.IsExecuted都应该是false
echo.

echo 问题3: 执行了但状态不更新
echo   原因: UpdateExecutionStatus调用失败
echo   检查: "❌ UpdateExecutionStatus调用失败" 或 "_contractStateService为null"
echo   解决: 确认服务注入正确
echo.

echo 问题4: 状态更新了但界面不刷新
echo   原因: 事件通知机制问题
echo   检查: "📤 XXX状态更新事件已发送" 和 "🚀【触发状态更新事件】"
echo   解决: 确认界面事件订阅正确
echo.

echo 问题5: 界面显示不正确
echo   原因: 数据同步问题
echo   检查: 文件中的ExecutionState值与界面显示
echo   解决: 手动刷新界面或重新加载配置
echo.

echo 🧪 测试建议:
echo.

echo 测试1: 保本触发测试
echo   1. 设置保本触发金额为较小值（如10U）
echo   2. 等待浮盈超过触发条件
echo   3. 观察完整的执行流程日志
echo   4. 验证状态文件和界面更新
echo.

echo 测试2: 推仓触发测试  
echo   1. 设置推仓阶梯1触发金额为较小值
echo   2. 等待浮盈达到条件
echo   3. 验证推仓执行和状态更新
echo   4. 确认下次扫描不会重复触发
echo.

echo 测试3: 重复触发防护测试
echo   1. 手动修改文件中ExecutionState为"NotTriggered"
echo   2. 再次等待条件满足
echo   3. 验证是否正确触发
echo   4. 验证执行后状态正确更新为"Executed"
echo.

echo 🎯 关键成功指标:
echo   ✅ 条件满足时自动执行操作
echo   ✅ 执行成功后状态更新为"已执行"
echo   ✅ 下次扫描不会重复触发相同操作
echo   ✅ 界面状态实时同步显示
echo   ✅ 文件中ExecutionState值正确
echo.

echo 📝 如果发现问题，请提供：
echo   1. 完整的扫描日志（包含5个步骤）
echo   2. 触发前后的状态文件内容
echo   3. 界面显示的状态变化
echo   4. 具体的浮盈数值和触发条件
echo.

echo 🚀 开始测试！观察日志输出，确认每个步骤都正常执行。
echo.

pause 