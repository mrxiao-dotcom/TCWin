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
                _positionProfiles.Clear();
                foreach (var position in positions.Where(p => Math.Abs(p.PositionAmt) > 0))
                {
                    var key = GetPositionKey(position.Symbol, position.PositionSideString);
                    _positionProfiles[key] = new PositionProfile
                    {
                        Symbol = position.Symbol,
                        PositionSide = position.PositionSideString,
                        InitialQuantity = Math.Abs(position.PositionAmt),
                        InitialEntryPrice = position.EntryPrice,
                        CreateTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now
                    };
                    
                    _logger.LogDebug($"📝 建档持仓: {key}, 数量: {position.PositionAmt:F6}, 入场价: {position.EntryPrice:F4}");
                }
                
                _logger.LogInformation($"📊 初始化完成 - 建档{_positionProfiles.Count}个持仓");
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

                var activePositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                
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

            // 检查各种触发条件
            await CheckBreakEvenTriggerAsync(position, profile, currentPnl);
            await CheckAddPositionTriggersAsync(position, profile, currentPnl);
            await CheckProfitProtectionTriggersAsync(position, profile, currentPnl);
        }

        /// <summary>
        /// 检查自动保本触发条件
        /// </summary>
        private async Task CheckBreakEvenTriggerAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.BreakEvenConfig.IsEnabled) return;
            if (currentPnl <= _config.BreakEvenConfig.TriggerProfitAmount) return;

            var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_BreakEven";
            if (profile.TriggerRecords.ContainsKey(triggerKey)) return;

            try
            {
                _logger.LogInformation($"🎯 触发自动保本: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {_config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                
                var success = await ExecuteBreakEvenStopLossAsync(position);
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, success);
                
                _logger.LogInformation($"✅ 自动保本执行{(success ? "成功" : "失败")}: {position.Symbol}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行自动保本时发生错误: {position.Symbol}");
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, false);
            }
        }

        /// <summary>
        /// 检查自动推仓触发条件
        /// </summary>
        private async Task CheckAddPositionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.AddPositionConfig.IsEnabled) return;

            var enabledStages = _config.AddPositionConfig.Tiers.Where(s => !s.IsTriggered).OrderBy(s => s.TriggerProfitAmount);
            
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
                    
                    _logger.LogInformation($"✅ 推仓阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行推仓阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, false);
                }
            }
        }

        /// <summary>
        /// 检查保盈止损触发条件
        /// </summary>
        private async Task CheckProfitProtectionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.ProfitProtectionConfig.IsEnabled) return;

            var enabledStages = _config.ProfitProtectionConfig.Tiers.Where(s => !s.IsTriggered).OrderBy(s => s.TriggerProfitAmount);
            
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
                    
                    _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行保盈止损阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, false);
                }
            }
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

                // 计算保本价格（入场价格 + 1U缓冲）
                var quantity = Math.Abs(position.PositionAmt);
                var bufferPerUnit = 1.0m / quantity;
                var stopPrice = position.PositionAmt > 0 
                    ? position.EntryPrice + bufferPerUnit
                    : position.EntryPrice - bufferPerUnit;
                stopPrice = Math.Round(stopPrice, 4);

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
                // 临时设置MainViewModel的参数
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _mainViewModel.Symbol = position.Symbol.Replace("USDT", "");
                    _mainViewModel.StopLossRatio = stage.StopLossRatio;
                });

                // 计算风险金和加仓数量
                var accountEquity = _mainViewModel.AccountInfo?.TotalEquity ?? 0;
                var riskTimes = _mainViewModel.SelectedAccount?.RiskCapitalTimes ?? 8;
                var riskCapital = accountEquity / riskTimes * stage.RiskMultiplier;

                // 获取最新价格
                var latestPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                if (latestPrice <= 0) return false;

                // 计算加仓数量
                var addQuantity = riskCapital / latestPrice;

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
                    TimeInForce = "GTC"
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

                // 设置新的保本止损
                var stopQuantity = Math.Abs(updatedPosition.PositionAmt);
                var bufferPerUnit = 1.0m / stopQuantity;
                var newStopPrice = updatedPosition.PositionAmt > 0 
                    ? updatedPosition.EntryPrice + bufferPerUnit
                    : updatedPosition.EntryPrice - bufferPerUnit;
                newStopPrice = Math.Round(newStopPrice, 4);

                var stopOrderSide = updatedPosition.PositionAmt > 0 ? "SELL" : "BUY";
                var stopOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = stopOrderSide,
                    Type = "STOP_MARKET",
                    Quantity = stopQuantity,
                    StopPrice = newStopPrice,
                    TimeInForce = "GTC",
                    ReduceOnly = true
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

            _executionHistory.Add(new ExecutionHistory
            {
                Symbol = position.Symbol,
                PositionSide = position.PositionSideString,
                ExecutionType = executionType,
                ExecutionTime = DateTime.Now,
                TriggerPnl = currentPnl,
                IsSuccess = success,
                Details = $"浮盈{currentPnl:F2}U时触发{executionType}"
            });

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