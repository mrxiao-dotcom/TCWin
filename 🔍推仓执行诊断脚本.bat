@echo off
chcp 65001 > nul
echo 🔍 推仓执行诊断脚本
echo ===================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 诊断时间: %datestamp% %timestamp%
echo.

echo 🔍 推仓执行流程完整诊断
echo.

echo 📋 问题描述:
echo   用户反馈："当浮盈达到推仓触发，并没有看到加仓，请检查现在的流程，是否没有下单加仓"
echo.

echo 🎯 推仓执行完整流程分析:
echo.
echo 1️⃣ 条件检查阶段:
echo   ✅ 浮盈检查: currentPnl >= tier.TriggerProfitAmount
echo   ✅ 状态检查: ExecutionState != Executed
echo   ✅ 冷却期检查: CanExecute()
echo.
echo 2️⃣ 执行标记阶段:
echo   ✅ 标记为"执行中": MarkAsExecuting()
echo   ✅ 记录冷却期: RecordExecution()
echo.
echo 3️⃣ 推仓计算阶段:
echo   ✅ 获取实时价格: GetLatestPriceAsync()
echo   ✅ 获取交易规则: GetTradingRulesAsync()
echo   ✅ 计算加仓市值: CalculateAddPositionValue()
echo   ✅ 计算加仓数量: positionValue / currentPrice
echo.
echo 4️⃣ 模拟模式检查阶段（关键）:
echo   🔍 IsSimulationMode() 检查:
echo      - IP限制状态: BinanceService.IsIpRestricted
echo      - API Key配置: apiKey长度 > 10
echo      - Secret Key配置: secretKey长度 > 10
echo.
echo 5️⃣ 实际下单阶段:
echo   ✅ 构造订单请求: OrderRequest
echo   ✅ 调用下单API: PlaceMarketOrderAsync()
echo   ✅ 币安API调用: BinanceService.PlaceOrderAsync()
echo.

echo 🚨 可能的问题点:
echo.
echo 问题1: 系统被错误识别为模拟模式
echo   症状: 日志显示"⚠️ 【模拟模式】推仓不会进行真实下单"
echo   原因: 
echo   - IP限制标志被错误设置
echo   - API Key/Secret Key配置不完整
echo   - 账户选择为"Test"账户
echo.
echo 问题2: API配置缺失或无效
echo   症状: 日志显示"🎯【模拟下单】无API配置"
echo   原因:
echo   - CurrentAccount为null
echo   - ApiKey或SecretKey为空
echo   - API Key长度不足10位
echo.
echo 问题3: IP限制模式激活
echo   症状: 日志显示"🎯【模拟下单】IP受限模式"
echo   原因:
echo   - BinanceService.IsIpRestricted = true
echo   - 之前触发了IP限制检测
echo.
echo 问题4: 下单API执行失败
echo   症状: 日志显示"❌ 加仓下单失败"
echo   原因:
echo   - 网络连接问题
echo   - API权限不足
echo   - 交易规则违反
echo.

echo 🔍 诊断检查清单:
echo.
echo 第1步: 检查日志中的关键信息
echo   查找以下日志标记:
echo   ✅ "🎯 【重要】推仓执行模式检查: 模拟模式 OR 实盘模式"
echo   ✅ "⚠️ 【模拟模式】推仓不会进行真实下单" (如果看到说明是模拟模式)
echo   ✅ "🚀 调用币安API下单" (如果看到说明进入了实际下单)
echo   ✅ "✅ 加仓下单成功" OR "❌ 加仓下单失败"
echo.
echo 第2步: 检查账户配置
echo   查看当前选择的账户:
echo   ❌ 如果是"Test"账户 → 这是模拟账户，不会实际下单
echo   ✅ 如果是其他账户名 → 检查API配置
echo.
echo 第3步: 检查API配置状态
echo   在日志中查找:
echo   ✅ API Key长度 > 10
echo   ✅ Secret Key长度 > 10
echo   ❌ 如果长度不足或为空 → 系统会自动进入模拟模式
echo.
echo 第4步: 检查IP限制状态
echo   在日志中查找:
echo   ❌ "IP受限模式" → 系统自动使用模拟数据
echo   ❌ "🚫 检测到IP限制" → 需要重置IP限制状态
echo.

echo 💡 解决方案:
echo.
echo 方案1: 如果是模拟模式问题
echo   ✅ 确认当前账户不是"Test"
echo   ✅ 检查API Key和Secret Key配置
echo   ✅ 确保API Key长度 >= 10位
echo   ✅ 确保Secret Key长度 >= 10位
echo.
echo 方案2: 如果是IP限制问题
echo   ✅ 在界面或代码中调用 BinanceService.ResetIpRestriction()
echo   ✅ 重启应用程序
echo   ✅ 检查网络连接
echo.
echo 方案3: 如果是API权限问题
echo   ✅ 确认API Key有期货交易权限
echo   ✅ 检查IP白名单设置
echo   ✅ 验证API Key/Secret Key的有效性
echo.
echo 方案4: 如果是交易规则问题
echo   查看日志中的具体错误信息:
echo   - "加仓数量小于最小交易量"
echo   - "计算加仓市值失败"
echo   - "无法获取交易规则"
echo.

echo 📊 关键日志模式识别:
echo.
echo 🟢 正常实盘执行模式:
echo [HH:MM:SS] 🎯 【重要】推仓执行模式检查: 实盘模式
echo [HH:MM:SS] 🚀 调用币安API下单: BTCUSDT
echo [HH:MM:SS] ✅ 加仓下单成功: BTCUSDT BUY 0.001000
echo.
echo 🟡 模拟模式（不会实际下单）:
echo [HH:MM:SS] 🎯 【重要】推仓执行模式检查: 模拟模式
echo [HH:MM:SS] ⚠️ 【模拟模式】推仓不会进行真实下单，仅更新状态和日志
echo [HH:MM:SS] 💡 如需真实下单，请确保API配置有效并重启服务
echo.
echo 🔴 API配置问题:
echo [HH:MM:SS] 🎯【模拟下单】无API配置
echo [HH:MM:SS] 🔍 TradingExecutionService模拟环境检查: API Key长度=0, Secret Key长度=0
echo.
echo 🔴 IP限制问题:
echo [HH:MM:SS] 🎯【模拟下单】IP受限模式
echo [HH:MM:SS] 🚫 检测到IP限制，自动启用模拟数据模式
echo.

echo 🚀 立即诊断步骤:
echo.
echo 1. 启动自动盯盘并等待推仓触发
echo 2. 仔细观察日志中的"推仓执行模式检查"信息
echo 3. 确认是"实盘模式"还是"模拟模式"
echo 4. 如果是模拟模式，按照上述方案进行修复
echo 5. 如果是实盘模式但仍无下单，检查后续的下单步骤
echo.

echo 🎯 预期的正确执行流程:
echo 条件满足 → 实盘模式检查通过 → 调用API下单 → 下单成功 → 持仓增加
echo.

echo 📞 如果问题仍然存在:
echo 1. 提供完整的推仓触发日志
echo 2. 确认当前账户名和API配置状态
echo 3. 检查币安账户的API权限设置
echo 4. 验证网络连接和API访问性
echo.

echo 🎉 成功标志:
echo 看到"✅ 加仓下单成功"并且在币安账户中确实有新的持仓增加
echo.

pause 