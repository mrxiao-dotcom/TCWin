using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动监控服务 - 集成现有交易功能的完整实现
    /// </summary>
    public class AutoMonitorService : IDisposable
    {
        private readonly IBinanceService _binanceService;
        private readonly MainViewModel _mainViewModel;
        private readonly ILogger<AutoMonitorService> _logger;
        private readonly AutoMonitorPersistenceService _persistenceService;
        private Timer? _scanTimer;
        private bool _isRunning;
        private AutoMonitorConfig? _config;
        private readonly object _lockObject = new();

        // 持仓档案存储
        private readonly Dictionary<string, PositionProfile> _positionProfiles = new();
        
        // 执行历史记录
        private readonly List<ExecutionHistory> _executionHistory = new();

        // 事件定义
        public event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;
        public event EventHandler<ExecutionResultEventArgs>? ExecutionCompleted;

        public AutoMonitorService(
            IBinanceService binanceService, 
            MainViewModel mainViewModel,
            ILogger<AutoMonitorService> logger)
        {
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 🔧 新增：初始化持久化服务
            _persistenceService = new AutoMonitorPersistenceService();
        }

        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync(AutoMonitorConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            lock (_lockObject)
            {
                if (_isRunning) throw new InvalidOperationException("自动监控已在运行中");
                _config = config;
                _isRunning = true;
            }

            try
            {
                _logger.LogInformation("🚀 启动自动监控服务...");
                await InitializePositionProfilesAsync();
                
                var intervalMs = _config.ScanIntervalSeconds * 1000;
                _scanTimer = new Timer(async _ => await ScanPositionsAsync(), null, 0, intervalMs);
                
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = true, 
                    Message = $"自动监控已启动 - 扫描间隔{_config.ScanIntervalSeconds}秒" 
                });
                
                _logger.LogInformation($"✅ 自动监控启动成功 - 配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒");
                return true;
            }
            catch (Exception ex)
            {
                lock (_lockObject) { _isRunning = false; }
                _logger.LogError(ex, "自动监控启动失败");
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = false, 
                    Message = $"启动失败：{ex.Message}" 
                });
                return false;
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        public void StopMonitoring()
        {
            lock (_lockObject)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _scanTimer?.Dispose();
                _scanTimer = null;
                
                // 🔧 新增：停止时保存状态到持久化存储
                try
                {
                    _persistenceService.SavePositionProfiles(_positionProfiles);
                    _persistenceService.SaveExecutionHistory(_executionHistory);
                    _logger.LogInformation("💾 已保存自动盯盘状态到持久化存储");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 保存自动盯盘状态失败");
                }
            }
            
            _logger.LogInformation("⏹️ 自动监控已停止");
            OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                IsRunning = false, 
                Message = "自动监控已停止" 
            });
        }

        /// <summary>
        /// 初始化持仓档案
        /// </summary>
        private async Task InitializePositionProfilesAsync()
        {
            var positions = await _binanceService.GetPositionsAsync();
            if (positions == null) return;

            lock (_lockObject)
            {
                // 🔧 修改：先加载持久化的状态
                _positionProfiles.Clear();
                var persistedProfiles = _persistenceService.LoadPositionProfiles();
                
                _logger.LogInformation($"📖 从持久化存储加载了 {persistedProfiles.Count} 个持仓档案");
                
                foreach (var position in positions.Where(p => Math.Abs(p.PositionAmt) > 0))
                {
                    var key = GetPositionKey(position.Symbol, position.PositionSideString);
                    
                    // 如果持久化存储中有该持仓的档案，则使用持久化的数据（保留执行状态）
                    if (persistedProfiles.ContainsKey(key))
                    {
                        var persistedProfile = persistedProfiles[key];
                        // 更新实时数据，但保留执行状态
                        persistedProfile.InitialQuantity = Math.Abs(position.PositionAmt);
                        persistedProfile.InitialEntryPrice = position.EntryPrice;
                        persistedProfile.LastUpdateTime = DateTime.Now;
                        persistedProfile.IsActive = true;
                        
                        _positionProfiles[key] = persistedProfile;
                        _logger.LogInformation($"🔄 恢复持仓档案: {key} - 触发记录: {persistedProfile.TriggerRecords.Count}");
                    }
                    else
                    {
                        // 新建档案
                        _positionProfiles[key] = new PositionProfile
                        {
                            Symbol = position.Symbol,
                            PositionSide = position.PositionSideString,
                            InitialQuantity = Math.Abs(position.PositionAmt),
                            InitialEntryPrice = position.EntryPrice,
                            CreateTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now,
                            IsActive = true
                        };
                        _logger.LogDebug($"📝 新建档案: {key}, 数量: {position.PositionAmt:F6}, 入场价: {position.EntryPrice:F4}");
                    }
                }
                
                // 🔧 新增：加载执行历史
                var persistedHistory = _persistenceService.LoadExecutionHistory();
                _executionHistory.Clear();
                _executionHistory.AddRange(persistedHistory);
                
                _logger.LogInformation($"📊 初始化完成 - 持仓档案: {_positionProfiles.Count}个, 执行历史: {_executionHistory.Count}条");
            }
        }

        /// <summary>
        /// 定时扫描持仓
        /// </summary>
        private async Task ScanPositionsAsync()
        {
            if (!_isRunning || _config == null) return;

            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                if (positions == null) return;

                // 🔧 修复：只处理有实际持仓的合约，过滤掉零持仓和无效数据
                var activePositions = positions.Where(p => 
                    Math.Abs(p.PositionAmt) > 0.0001m && // 数量过滤：过滤掉零持仓
                    !string.IsNullOrEmpty(p.Symbol) &&   // 合约名称过滤：确保合约名称有效
                    p.MarkPrice > 0 &&                   // 标记价格过滤：确保价格有效
                    p.EntryPrice > 0                     // 开仓价格过滤：确保开仓价有效
                ).ToList();
                
                _logger.LogDebug($"🔍 扫描持仓: {activePositions.Count}个活跃持仓");
                
                foreach (var position in activePositions)
                {
                    await ProcessPositionAsync(position);
                }
                
                CleanupClosedPositions(activePositions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描持仓时发生错误");
            }
        }

        /// <summary>
        /// 处理单个持仓
        /// </summary>
        private async Task ProcessPositionAsync(PositionInfo position)
        {
            var key = GetPositionKey(position.Symbol, position.PositionSideString);
            
            // 确保持仓档案存在
            lock (_lockObject)
            {
                if (!_positionProfiles.ContainsKey(key))
                {
                    _positionProfiles[key] = new PositionProfile
                    {
                        Symbol = position.Symbol,
                        PositionSide = position.PositionSideString,
                        InitialQuantity = Math.Abs(position.PositionAmt),
                        InitialEntryPrice = position.EntryPrice,
                        CreateTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now
                    };
                    
                    _logger.LogInformation($"📝 新建档案: {key}");
                }
                _positionProfiles[key].LastUpdateTime = DateTime.Now;
            }

            var profile = _positionProfiles[key];
            var currentPnl = position.UnrealizedProfit;

            // 只对有盈利的持仓进行检查
            if (currentPnl <= 0) return;

            // 🔧 新增：检查是否在冷却期内，防止短时间内重复执行
            var lastExecutionTime = GetLastExecutionTime(profile);
            var cooldownSeconds = 30; // 30秒冷却期
            if (lastExecutionTime.HasValue && (DateTime.Now - lastExecutionTime.Value).TotalSeconds < cooldownSeconds)
            {
                _logger.LogDebug($"⏳ {key} 在冷却期内，跳过检查 (剩余{cooldownSeconds - (DateTime.Now - lastExecutionTime.Value).TotalSeconds:F0}秒)");
                return;
            }

            // 检查各种触发条件 - 🔧 修复：每次扫描最多只执行一个操作，防止连续触发
            var executed = false;
            
            if (!executed)
            {
                executed = await CheckBreakEvenTriggerAsync(position, profile, currentPnl);
                if (executed) _logger.LogInformation($"🎯 {key} 执行了自动保本，跳过其他检查");
            }
            
            if (!executed)
            {
                executed = await CheckAddPositionTriggersAsync(position, profile, currentPnl);
                if (executed) _logger.LogInformation($"🚀 {key} 执行了推仓操作，跳过其他检查");
            }
            
            if (!executed)
            {
                executed = await CheckProfitProtectionTriggersAsync(position, profile, currentPnl);
                if (executed) _logger.LogInformation($"🛡️ {key} 执行了保盈止损，跳过其他检查");
            }
        }

        /// <summary>
        /// 检查自动保本触发条件
        /// </summary>
        private async Task<bool> CheckBreakEvenTriggerAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.BreakEvenConfig.IsEnabled) return false;
            if (currentPnl <= _config.BreakEvenConfig.TriggerProfitAmount) return false;

            var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_BreakEven";
            if (profile.TriggerRecords.ContainsKey(triggerKey)) return false;

            try
            {
                _logger.LogInformation($"🎯 触发自动保本: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {_config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                
                var success = await ExecuteBreakEvenStopLossAsync(position);
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, success);
                
                _logger.LogInformation($"✅ 自动保本执行{(success ? "成功" : "失败")}: {position.Symbol}");
                return true; // 表示执行了操作
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行自动保本时发生错误: {position.Symbol}");
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, false);
                return true; // 表示执行了操作（即使失败）
            }
        }

        /// <summary>
        /// 检查自动推仓触发条件
        /// </summary>
        private async Task<bool> CheckAddPositionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.AddPositionConfig.IsEnabled) return false;

            // 🔧 修复：移除全局IsTriggered检查，只依赖合约独立的TriggerRecords机制
            // 原来的逻辑会导致一个合约触发后，其他合约无法执行相同阶梯
            var enabledStages = _config.AddPositionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (currentPnl <= stage.TriggerProfitAmount) continue;

                var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_AddPosition_Stage{stage.TierIndex}";
                if (profile.TriggerRecords.ContainsKey(triggerKey)) continue;

                try
                {
                    _logger.LogInformation($"🚀 触发推仓阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteAddPositionAsync(position, stage);
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, success);
                    
                    // 🔧 修复：不再设置全局IsTriggered状态，防止影响其他合约
                    // 防重复机制完全依赖profile.TriggerRecords，这是按合约独立的
                    if (success)
                    {
                        _logger.LogInformation($"✅ 推仓阶梯{stage.TierIndex}执行成功: {position.Symbol} (其他合约仍可独立触发此阶梯)");
                    }
                    
                    _logger.LogInformation($"✅ 推仓阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                    return true; // 🔧 关键修复：执行一个阶梯后立即返回，防止同一次扫描触发多个阶梯
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行推仓阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
            return false; // 没有执行任何操作
        }

        /// <summary>
        /// 检查保盈止损触发条件
        /// </summary>
        private async Task<bool> CheckProfitProtectionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.ProfitProtectionConfig.IsEnabled) return false;

            // 🔧 修复：移除全局IsTriggered检查，只依赖合约独立的TriggerRecords机制
            var enabledStages = _config.ProfitProtectionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (currentPnl <= stage.TriggerProfitAmount) continue;

                var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_ProfitProtection_Stage{stage.TierIndex}";
                if (profile.TriggerRecords.ContainsKey(triggerKey)) continue;

                try
                {
                    _logger.LogInformation($"🛡️ 触发保盈止损阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteProfitProtectionAsync(position, stage);
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, success);
                    
                    // 🔧 修复：不再设置全局IsTriggered状态，防止影响其他合约
                    if (success)
                    {
                        _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行成功: {position.Symbol} (其他合约仍可独立触发此阶梯)");
                    }
                    
                    _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                    return true; // 执行一个阶梯后立即返回
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行保盈止损阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
            return false; // 没有执行任何操作
        }

        /// <summary>
        /// 执行保本止损设置 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteBreakEvenStopLossAsync(PositionInfo position)
        {
            try
            {
                // 临时设置选中持仓
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var targetPosition = _mainViewModel.Positions.FirstOrDefault(p => 
                        p.Symbol == position.Symbol && p.PositionSide == position.PositionSide);
                    if (targetPosition != null)
                    {
                        _mainViewModel.SelectedPosition = targetPosition;
                    }
                });

                // 🔧 修复：计算真正的保本价格，使用百分比缓冲而不是固定金额缓冲
                var quantity = Math.Abs(position.PositionAmt);
                var entryPrice = position.EntryPrice;
                
                // 使用很小的百分比缓冲（0.05%），确保真正保本而不会被轻易触发
                var bufferPercentage = 0.0005m; // 0.05%
                var stopPrice = position.PositionAmt > 0 
                    ? entryPrice * (1 + bufferPercentage)  // 多头：成本价 + 0.05%
                    : entryPrice * (1 - bufferPercentage); // 空头：成本价 - 0.05%
                stopPrice = Math.Round(stopPrice, 4);
                
                _logger.LogInformation($"💰 自动保本止损计算: 成本价={entryPrice:F4}, 缓冲={bufferPercentage * 100:F2}%, 止损价={stopPrice:F4}");

                var side = position.PositionAmt > 0 ? "SELL" : "BUY";

                // 清理历史止损委托
                await CleanupAllStopOrdersAsync(position.Symbol);

                // 创建保本止损订单
                var stopLossRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                
                if (success)
                {
                    _logger.LogInformation($"💰 保本止损设置成功: {position.Symbol} @{stopPrice:F4}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行保本止损失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 执行一键保本加仓 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteAddPositionAsync(PositionInfo position, AddPositionTier stage)
        {
            try
            {
                // 🔧 修复：不要修改MainViewModel的Symbol，避免干扰用户界面
                // 自动盯盘应该独立运行，不影响用户的界面设置
                _logger.LogInformation($"💰 自动盯盘推仓: {position.Symbol}, 止损比例: {stage.StopLossRatio * 100:F1}%");

                // 🔧 修复：按照正确的计算方法计算加仓数量
                // 1. 计算单笔风险金：账户权益/风险次数=单笔风险金
                var accountEquity = _mainViewModel.AccountInfo?.TotalEquity ?? 0;
                var riskTimes = _mainViewModel.SelectedAccount?.RiskCapitalTimes ?? 8;
                var singleRiskCapital = accountEquity / riskTimes;
                
                // 2. 根据设置的止损比例，用单笔风险金计算出本次增仓的货值
                var stopLossRatio = stage.StopLossRatio; // 这里是小数形式，如0.1表示10%
                var positionValue = singleRiskCapital / stopLossRatio;
                
                // 3. 获取最新价格
                var latestPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                if (latestPrice <= 0) return false;
                
                // 4. 本次加仓的数量=增仓货值/合约单价
                var addQuantity = positionValue / latestPrice;
                
                _logger.LogInformation($"💰 加仓计算详情: 账户权益={accountEquity:F2}U, 风险次数={riskTimes}, 单笔风险金={singleRiskCapital:F2}U, 止损比例={stopLossRatio * 100:F1}%, 增仓货值={positionValue:F2}U, 合约单价={latestPrice:F4}, 加仓数量={addQuantity:F8}");

                // 检查交易规则
                try
                {
                    var (minQty, maxQty, stepSize, _, _) = await _binanceService.GetSymbolTradingRulesAsync(position.Symbol);
                    addQuantity = Math.Floor(addQuantity / stepSize) * stepSize;
                    
                    if (addQuantity < minQty || addQuantity > maxQty)
                    {
                        _logger.LogWarning($"加仓数量不符合交易规则: {addQuantity:F6}, 最小: {minQty:F6}, 最大: {maxQty:F6}");
                        return false;
                    }
                }
                catch
                {
                    addQuantity = Math.Round(addQuantity, 6);
                }

                // 执行加仓
                var addPositionSide = position.PositionAmt > 0 ? "BUY" : "SELL";
                var addOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = addPositionSide,
                    Type = "MARKET",
                    Quantity = addQuantity,
                    TimeInForce = "GTC",
                    PositionSide = position.PositionSideString // 🔧 修复：添加持仓方向参数
                };

                var addSuccess = await _binanceService.PlaceOrderAsync(addOrderRequest);
                if (!addSuccess) return false;

                // 等待订单执行
                await Task.Delay(2000);

                // 刷新持仓数据
                var updatedPositions = await _binanceService.GetPositionsAsync();
                var updatedPosition = updatedPositions?.FirstOrDefault(p => 
                    p.Symbol == position.Symbol && p.PositionSide == position.PositionSide);

                if (updatedPosition == null) return false;

                // 🔧 修复：设置真正的保本止损，使用加仓后的最新成本价
                var stopQuantity = Math.Abs(updatedPosition.PositionAmt);
                var entryPrice = updatedPosition.EntryPrice; // 这是加仓后的最新成本价
                
                // 使用很小的百分比缓冲（0.05%），确保真正保本而不会被轻易触发
                var bufferPercentage = 0.0005m; // 0.05%
                var newStopPrice = updatedPosition.PositionAmt > 0 
                    ? entryPrice * (1 + bufferPercentage)  // 多头：成本价 + 0.05%
                    : entryPrice * (1 - bufferPercentage); // 空头：成本价 - 0.05%
                newStopPrice = Math.Round(newStopPrice, 4);
                
                _logger.LogInformation($"💰 保本止损计算: 成本价={entryPrice:F4}, 缓冲={bufferPercentage * 100:F2}%, 止损价={newStopPrice:F4}");

                var stopOrderSide = updatedPosition.PositionAmt > 0 ? "SELL" : "BUY";
                var stopOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = stopOrderSide,
                    Type = "STOP_MARKET",
                    Quantity = stopQuantity,
                    StopPrice = newStopPrice,
                    TimeInForce = "GTC",
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString, // 🔧 修复：添加持仓方向参数
                    WorkingType = "CONTRACT_PRICE"               // 🔧 修复：添加工作类型参数
                };

                var stopSuccess = await _binanceService.PlaceOrderAsync(stopOrderRequest);
                
                _logger.LogInformation($"🚀 推仓完成: {position.Symbol}, 加仓: {addQuantity:F6}@{latestPrice:F4}, 保本止损: @{newStopPrice:F4}");
                
                return stopSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行推仓失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 执行保盈止损设置 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteProfitProtectionAsync(PositionInfo position, ProfitProtectionTier stage)
        {
            try
            {
                // 计算保盈止损价格
                var isLong = position.PositionAmt > 0;
                var entryPrice = position.EntryPrice;
                var quantity = Math.Abs(position.PositionAmt);
                var protectionAmount = stage.ProtectionAmount;
                
                decimal protectionPrice;
                if (isLong)
                {
                    protectionPrice = entryPrice + (protectionAmount / quantity);
                }
                else
                {
                    protectionPrice = entryPrice - (protectionAmount / quantity);
                }

                // 验证止损价合理性
                var currentPrice = position.MarkPrice;
                bool isValidStopPrice = isLong 
                    ? (protectionPrice < currentPrice && protectionPrice > entryPrice)
                    : (protectionPrice > currentPrice && protectionPrice < entryPrice);

                if (!isValidStopPrice)
                {
                    _logger.LogWarning($"保盈止损价格不合理: {protectionPrice:F4}, 当前价: {currentPrice:F4}, 入场价: {entryPrice:F4}");
                    return false;
                }

                // 清理历史止损委托
                await CleanupAllStopOrdersAsync(position.Symbol);

                // 创建保盈止损订单
                var side = isLong ? "SELL" : "BUY";
                var stopLossRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = protectionPrice,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                
                if (success)
                {
                    _logger.LogInformation($"🛡️ 保盈止损设置成功: {position.Symbol} @{protectionPrice:F4}, 保护: {protectionAmount:F2}U");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行保盈止损失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 清理历史止损委托
        /// </summary>
        private async Task CleanupAllStopOrdersAsync(string symbol)
        {
            try
            {
                var orders = await _binanceService.GetOpenOrdersAsync(symbol);
                if (orders == null) return;

                var stopOrders = orders.Where(o => 
                    o.Type == "STOP_MARKET" && 
                    o.ReduceOnly &&
                    o.Status == "NEW").ToList();

                foreach (var order in stopOrders)
                {
                    try
                    {
                        await _binanceService.CancelOrderAsync(symbol, order.OrderId);
                        await Task.Delay(100); // 避免API限制
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"取消止损单失败: {order.OrderId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"清理止损委托失败: {symbol}");
            }
        }

        /// <summary>
        /// 记录触发执行结果
        /// </summary>
        private void RecordTriggerExecution(PositionProfile profile, PositionInfo position, string triggerKey, string executionType, decimal currentPnl, bool success)
        {
            profile.TriggerRecords[triggerKey] = new TriggerRecord
            {
                TriggerType = executionType,
                TriggerTime = DateTime.Now,
                TriggerPnl = currentPnl,
                IsExecuted = success,
                ExecutionResult = success ? "成功" : "失败"
            };

            var executionHistory = new ExecutionHistory
            {
                Symbol = position.Symbol,
                PositionSide = position.PositionSideString,
                ExecutionType = executionType,
                ExecutionTime = DateTime.Now,
                TriggerPnl = currentPnl,
                IsSuccess = success,
                Details = $"浮盈{currentPnl:F2}U时触发{executionType}"
            };
            
            _executionHistory.Add(executionHistory);

            // 🔧 新增：实时保存状态到持久化存储
            try
            {
                _persistenceService.SavePositionProfiles(_positionProfiles);
                _persistenceService.SaveExecutionHistory(_executionHistory);
                _logger.LogDebug($"💾 已保存执行状态: {position.Symbol}_{position.PositionSideString} {executionType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存执行状态失败: {position.Symbol}_{position.PositionSideString} {executionType}");
            }

            OnExecutionCompleted(new ExecutionResultEventArgs
            {
                Symbol = position.Symbol,
                ExecutionType = executionType,
                IsSuccess = success,
                Message = success ? $"{executionType}执行成功" : $"{executionType}执行失败",
                PnlAtExecution = currentPnl
            });
        }

        /// <summary>
        /// 清理已平仓的持仓档案
        /// </summary>
        private void CleanupClosedPositions(List<PositionInfo> activePositions)
        {
            lock (_lockObject)
            {
                var activeKeys = activePositions.Select(p => GetPositionKey(p.Symbol, p.PositionSideString)).ToHashSet();
                var keysToRemove = _positionProfiles.Keys.Where(k => !activeKeys.Contains(k)).ToList();
                
                foreach (var key in keysToRemove)
                {
                    _logger.LogDebug($"🗑️ 清理已平仓档案: {key}");
                    _positionProfiles.Remove(key);
                }
            }
        }

        /// <summary>
        /// 获取持仓唯一标识
        /// </summary>
        private static string GetPositionKey(string symbol, string positionSide) => $"{symbol}_{positionSide}";

        /// <summary>
        /// 获取最后执行时间
        /// </summary>
        private DateTime? GetLastExecutionTime(PositionProfile profile)
        {
            if (!profile.TriggerRecords.Any()) return null;
            return profile.TriggerRecords.Values.Max(t => t.TriggerTime);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public List<ExecutionHistory> GetExecutionHistory() => _executionHistory.ToList();

        /// <summary>
        /// 获取持仓档案
        /// </summary>
        public Dictionary<string, PositionProfile> GetPositionProfiles()
        {
            lock (_lockObject) { return new Dictionary<string, PositionProfile>(_positionProfiles); }
        }

        // 事件触发方法
        protected virtual void OnMonitorStatusChanged(MonitorStatusChangedEventArgs e) => MonitorStatusChanged?.Invoke(this, e);
        protected virtual void OnExecutionCompleted(ExecutionResultEventArgs e) => ExecutionCompleted?.Invoke(this, e);

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            _scanTimer?.Dispose();
        }
    }

    /// <summary>
    /// 监控状态变化事件参数
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 执行结果事件参数
    /// </summary>
    public class ExecutionResultEventArgs : EventArgs
    {
        public string Symbol { get; set; } = "";
        public string ExecutionType { get; set; } = "";
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";
        public decimal PnlAtExecution { get; set; }
    }
} 