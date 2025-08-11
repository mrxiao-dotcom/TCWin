@echo off
chcp 65001 >nul
echo.
echo ===============================================
echo 🔍 合约重复记录修复验证脚本
echo ===============================================
echo.
echo 🎯 验证目标：
echo    确认"同一个合约生成两组记录（如playusdt_long, playUSDT_both）"问题已修复
echo.
echo 🚀 已实施的关键修复：
echo    1. 强化键名标准化：完全重构EnsureUniqueContractStates方法
echo    2. 统一生成逻辑：不依赖API原始PositionSide，基于PositionAmt重新判断
echo    3. 字段标准化：确保Symbol大写，PositionSide为LONG/SHORT
echo    4. 详细日志记录：重复检测时记录详细信息
echo.
echo ===============================================
echo 🧪 详细测试步骤
echo ===============================================
echo.
echo 【测试1：状态文件检查】
echo 1. 启动程序，进入自动盯盘界面
echo 2. 点击【刷新持仓】按钮
echo 3. 观察日志输出，检查是否有重复记录提示
echo 4. 打开ContractMonitoringStates.json文件
echo 5. 检查文件内容格式
echo.
echo 预期结果：
echo ✅ 每个合约只有一条记录
echo ✅ 所有键名格式为 SYMBOL_SIDE（如BTCUSDT_LONG）
echo ✅ 没有大小写混用（如btcusdt_long 和 BTCUSDT_LONG）
echo ✅ 没有方向混用（如BTCUSDT_BOTH 和 BTCUSDT_LONG）
echo.
echo 【测试2：去重功能验证】
echo 手动创建重复记录测试：
echo 1. 停止监控程序
echo 2. 手动编辑ContractMonitoringStates.json
echo 3. 添加重复记录（不同大小写或BOTH/LONG）：
echo    例如："btcusdt_long": {...}
echo         "BTCUSDT_BOTH": {...}
echo 4. 保存文件并重启程序
echo 5. 启动监控并观察日志
echo.
echo 预期结果：
echo ✅ 日志显示："🔄 去重处理: 原始X条 → 去重后Y条"
echo ✅ 日志显示："⚠️ 发现重复合约状态: xxx → 标准化为 XXX"
echo ✅ 最终文件只保留一条标准化记录
echo ✅ 保留LastUpdateTime较新的记录
echo.
echo 【测试3：实际持仓测试】
echo 1. 确保有真实持仓（或Test账户测试数据）
echo 2. 启动自动盯盘
echo 3. 观察加载过程的日志
echo 4. 检查界面显示的合约列表
echo.
echo 预期结果：
echo ✅ 每个持仓合约只显示一次
echo ✅ 没有重复的合约条目
echo ✅ 合约名称格式统一（SYMBOL SIDE）
echo ✅ 状态显示正确
echo.
echo 【测试4：不同持仓模式测试】
echo 测试单向持仓和双向持仓模式：
echo 1. 在单向持仓模式下测试
echo 2. 在双向持仓模式下测试
echo 3. 观察API返回的PositionSide处理
echo 4. 确认最终键名格式一致
echo.
echo 预期结果：
echo ✅ 无论API返回BOTH还是LONG/SHORT
echo ✅ 最终都标准化为LONG或SHORT
echo ✅ 不会产生_BOTH格式的键名
echo ✅ 键名格式完全统一
echo.
echo ===============================================
echo 🔧 修复前 vs 修复后对比
echo ===============================================
echo.
echo 修复前的问题：
echo ❌ playusdt_long 和 playUSDT_both 同时存在
echo ❌ BTCUSDT_LONG 和 btcusdt_long 重复
echo ❌ 界面显示同一合约两套状态
echo ❌ 状态混乱，数据不一致
echo.
echo 修复后的效果：
echo ✅ 所有键名强制标准化为大写
echo ✅ 方向统一为LONG/SHORT，无BOTH
echo ✅ 智能去重保留最新状态
echo ✅ 每个合约唯一记录
echo ✅ 界面显示一致
echo.
echo ===============================================
echo 📋 详细验证检查清单
echo ===============================================
echo.
echo 键名格式检查：
echo □ 所有Symbol都是大写（如BTCUSDT，不是btcusdt）
echo □ 所有PositionSide都是LONG或SHORT
echo □ 没有_BOTH格式的键名
echo □ 格式完全统一为{SYMBOL}_{SIDE}
echo.
echo 重复检测功能：
echo □ 能检测到大小写不一致的重复
echo □ 能检测到BOTH与LONG/SHORT的重复
echo □ 正确保留LastUpdateTime较新的记录
echo □ 详细日志记录去重过程
echo.
echo 生成逻辑验证：
echo □ 不依赖API原始PositionSide
echo □ 完全基于PositionAmt重新判断方向
echo □ 强制Symbol大写转换
echo □ 确保状态字段标准化
echo.
echo 界面显示验证：
echo □ 合约列表无重复条目
echo □ 合约名称格式统一
echo □ 状态显示正确一致
echo □ 无数据混乱问题
echo.
echo 文件内容验证：
echo □ JSON文件格式正确
echo □ 每个合约只有一条记录
echo □ 键名格式完全标准化
echo □ 状态数据完整一致
echo.
echo ===============================================
echo 🚨 如果仍有问题
echo ===============================================
echo.
echo 问题1：仍然出现重复记录
echo 可能原因：
echo • 修复未完全生效，需要重新编译
echo • 现有文件有遗留重复数据
echo • 其他代码路径未修复
echo.
echo 解决方案：
echo 1. 重新编译：dotnet build --configuration Release
echo 2. 清空状态文件（备份后）
echo 3. 重新生成状态数据
echo 4. 检查所有生成键名的代码路径
echo.
echo 问题2：去重功能不工作
echo 可能原因：
echo • EnsureUniqueContractStates方法未调用
echo • 标准化逻辑有问题
echo • 日志级别设置问题
echo.
echo 解决方案：
echo 1. 检查SaveMonitoringStates调用链
echo 2. 确认去重方法正确调用
echo 3. 调整日志级别为Debug或Critical
echo 4. 手动测试去重逻辑
echo.
echo 问题3：界面仍显示重复
echo 可能原因：
echo • 状态文件已修复但界面未刷新
echo • 加载逻辑有缓存问题
echo • UI数据绑定问题
echo.
echo 解决方案：
echo 1. 重启程序重新加载
echo 2. 手动点击刷新按钮
echo 3. 检查UI数据加载逻辑
echo 4. 清除可能的缓存数据
echo.
echo ===============================================
echo 📊 测试结果记录
echo ===============================================
echo.
echo 测试时间：%date% %time%
echo 测试环境：_________________
echo.
echo 状态文件检查：
echo □ 文件格式正确
echo □ 无重复键名
echo □ 格式完全统一
echo □ 数据完整一致
echo 重复记录数：_____ → _____ （修复前 → 修复后）
echo.
echo 去重功能测试：
echo □ 能检测重复记录
echo □ 日志输出正确
echo □ 保留策略正确
echo □ 最终结果正确
echo 去重日志：_________________________________
echo.
echo 界面显示测试：
echo □ 无重复合约条目
echo □ 格式统一显示
echo □ 状态正确一致
echo □ 刷新功能正常
echo 显示合约数：_____（应与文件记录数一致）
echo.
echo 持仓模式测试：
echo □ 单向持仓模式正常
echo □ 双向持仓模式正常
echo □ BOTH转换为LONG/SHORT
echo □ 键名格式一致
echo.
echo 总体评价：
echo □ 完全修复 - 无重复记录问题
echo □ 基本修复 - 主要问题解决
echo □ 仍有问题 - 需要进一步处理
echo.
echo 备注：_________________________________
echo.
echo ===============================================
echo 💡 修复成功的标志
echo ===============================================
echo.
echo ✅ ContractMonitoringStates.json中每个合约只有一条记录
echo ✅ 所有键名格式为SYMBOL_SIDE（大写Symbol + LONG/SHORT）
echo ✅ 界面显示的合约列表无重复条目
echo ✅ 去重日志能正确显示处理过程
echo ✅ 不再出现playusdt_long和playUSDT_both同时存在的情况
echo.
echo 如果以上标志都达成，说明合约重复记录问题已彻底解决！
echo.
echo 🎉 修复成功后，您将拥有：
echo    • 干净统一的状态文件
echo    • 清晰一致的界面显示
echo    • 可靠的数据一致性
echo    • 智能的重复检测和处理机制
echo.
echo ===============================================
pause 