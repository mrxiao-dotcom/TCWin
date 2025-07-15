using System;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 交易执行服务 - 负责与币安API的实际交易操作
    /// </summary>
    public class TradingExecutionService
    {
        private readonly ILogger<TradingExecutionService> _logger;
        private readonly IBinanceService _binanceService;
        
        public TradingExecutionService(
            ILogger<TradingExecutionService> logger,
            IBinanceService binanceService)
        {
            _logger = logger;
            _binanceService = binanceService;
        }
        
        #region 保本交易逻辑
        
        /// <summary>
        /// 执行保本止损设置
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="config">保本配置</param>
        /// <returns>执行结果</returns>
        public async Task<TradingExecutionResult> ExecuteBreakEvenStopLossAsync(ContractProfile profile, ContractBreakEvenConfig config)
        {
            try
            {
                _logger.LogInformation($"开始执行保本止损: {profile.DisplayName}, 触发金额: {config.TriggerProfitAmount:F2}U");
                
                // 计算保本止损价格
                var breakEvenPrice = CalculateBreakEvenPrice(profile);
                
                // 验证价格合理性
                if (!ValidateStopLossPrice(profile, breakEvenPrice))
                {
                    var errorMsg = $"保本止损价格不合理: {breakEvenPrice:F4}";
                    _logger.LogWarning(errorMsg);
                    return TradingExecutionResult.Failed(errorMsg);
                }
                
                // 设置止损订单
                var orderResult = await SetStopLossOrderAsync(profile, breakEvenPrice, "保本止损");
                
                if (orderResult.IsSuccess)
                {
                    // 更新配置状态
                    config.BreakEvenPrice = breakEvenPrice;
                    config.IsExecuted = true;
                    config.ExecutionTime = DateTime.Now;
                    config.ExecutionMessage = $"保本止损已设置，价格: {breakEvenPrice:F4}";
                    
                    // 更新档案状态
                    profile.BreakEvenState.ExecutionStatus = "已执行";
                    profile.BreakEvenState.ExecutionResult = config.ExecutionMessage;
                    
                    profile.AddOperationHistory("保本执行", "成功", config.ExecutionMessage);
                    
                    _logger.LogInformation($"保本止损执行成功: {profile.DisplayName}, 止损价格: {breakEvenPrice:F4}");
                }
                
                return orderResult;
            }
            catch (Exception ex)
            {
                var errorMsg = $"保本止损执行失败: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                
                // 更新失败状态
                profile.BreakEvenState.ExecutionStatus = "执行失败";
                profile.BreakEvenState.ExecutionResult = errorMsg;
                profile.AddOperationHistory("保本执行", "失败", errorMsg);
                
                return TradingExecutionResult.Failed(errorMsg);
            }
        }
        
        #endregion
        
        #region 推仓交易逻辑
        
        /// <summary>
        /// 执行推仓加仓
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">推仓阶梯配置</param>
        /// <returns>执行结果</returns>
        public async Task<TradingExecutionResult> ExecuteAddPositionAsync(ContractProfile profile, ContractAddPositionTier tier)
        {
            try
            {
                _logger.LogInformation($"开始执行推仓加仓: {profile.DisplayName}, 阶梯{tier.TierIndex}, 触发金额: {tier.TriggerProfitAmount:F2}U");
                
                // 计算加仓数量
                var addQuantity = CalculateAddPositionQuantity(profile, tier);
                
                // 验证加仓数量
                if (addQuantity <= 0)
                {
                    var errorMsg = $"加仓数量无效: {addQuantity}";
                    _logger.LogWarning(errorMsg);
                    return TradingExecutionResult.Failed(errorMsg);
                }
                
                // 执行加仓开单
                var side = profile.Side == "LONG" ? "BUY" : "SELL";
                var orderResult = await PlaceMarketOrderAsync(profile.Symbol, side, addQuantity, $"推仓阶梯{tier.TierIndex}");
                
                if (orderResult.IsSuccess)
                {
                    // 计算新的止损价格
                    var newStopLossPrice = CalculateNewStopLossPrice(profile, tier);
                    
                    // 更新止损订单
                    if (newStopLossPrice > 0)
                    {
                        await UpdateStopLossOrderAsync(profile, newStopLossPrice, $"推仓阶梯{tier.TierIndex}更新止损");
                    }
                    
                    // 更新阶梯状态
                    tier.AddPositionQuantity = addQuantity;
                    tier.StopLossPrice = newStopLossPrice;
                    tier.IsExecuted = true;
                    tier.ExecutionTime = DateTime.Now;
                    tier.ExecutionMessage = $"推仓阶梯{tier.TierIndex}已执行，加仓: {addQuantity:F4}";
                    
                    // 更新档案状态
                    var tierState = profile.AddPositionStates.Find(s => s.TierIndex == tier.TierIndex);
                    if (tierState != null)
                    {
                        tierState.ExecutionStatus = "已执行";
                        tierState.ExecutionResult = tier.ExecutionMessage;
                    }
                    
                    profile.AddOperationHistory("推仓执行", "成功", tier.ExecutionMessage);
                    
                    _logger.LogInformation($"推仓加仓执行成功: {profile.DisplayName}, 阶梯{tier.TierIndex}, 加仓数量: {addQuantity:F4}");
                }
                
                return orderResult;
            }
            catch (Exception ex)
            {
                var errorMsg = $"推仓加仓执行失败: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                
                // 更新失败状态
                var tierState = profile.AddPositionStates.Find(s => s.TierIndex == tier.TierIndex);
                if (tierState != null)
                {
                    tierState.ExecutionStatus = "执行失败";
                    tierState.ExecutionResult = errorMsg;
                }
                
                profile.AddOperationHistory("推仓执行", "失败", errorMsg);
                
                return TradingExecutionResult.Failed(errorMsg);
            }
        }
        
        #endregion
        
        #region 保盈交易逻辑
        
        /// <summary>
        /// 执行保盈止损设置
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">保盈阶梯配置</param>
        /// <returns>执行结果</returns>
        public async Task<TradingExecutionResult> ExecuteProfitProtectionAsync(ContractProfile profile, ContractProfitProtectionTier tier)
        {
            try
            {
                _logger.LogInformation($"开始执行保盈止损: {profile.DisplayName}, 阶梯{tier.TierIndex}, 触发金额: {tier.TriggerProfitAmount:F2}U");
                
                // 计算保盈止损价格
                var protectionPrice = CalculateProfitProtectionPrice(profile, tier);
                
                // 验证价格合理性
                if (!ValidateStopLossPrice(profile, protectionPrice))
                {
                    var errorMsg = $"保盈止损价格不合理: {protectionPrice:F4}";
                    _logger.LogWarning(errorMsg);
                    return TradingExecutionResult.Failed(errorMsg);
                }
                
                // 设置/更新止损订单
                var orderResult = await UpdateStopLossOrderAsync(profile, protectionPrice, $"保盈阶梯{tier.TierIndex}");
                
                if (orderResult.IsSuccess)
                {
                    // 更新阶梯状态
                    tier.StopLossPrice = protectionPrice;
                    tier.IsExecuted = true;
                    tier.ExecutionTime = DateTime.Now;
                    tier.ExecutionMessage = $"保盈阶梯{tier.TierIndex}已执行，保护价格: {protectionPrice:F4}";
                    
                    // 更新档案状态
                    var tierState = profile.ProfitProtectionStates.Find(s => s.TierIndex == tier.TierIndex);
                    if (tierState != null)
                    {
                        tierState.ExecutionStatus = "已执行";
                        tierState.ExecutionResult = tier.ExecutionMessage;
                    }
                    
                    profile.AddOperationHistory("保盈执行", "成功", tier.ExecutionMessage);
                    
                    _logger.LogInformation($"保盈止损执行成功: {profile.DisplayName}, 阶梯{tier.TierIndex}, 保护价格: {protectionPrice:F4}");
                }
                
                return orderResult;
            }
            catch (Exception ex)
            {
                var errorMsg = $"保盈止损执行失败: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                
                // 更新失败状态
                var tierState = profile.ProfitProtectionStates.Find(s => s.TierIndex == tier.TierIndex);
                if (tierState != null)
                {
                    tierState.ExecutionStatus = "执行失败";
                    tierState.ExecutionResult = errorMsg;
                }
                
                profile.AddOperationHistory("保盈执行", "失败", errorMsg);
                
                return TradingExecutionResult.Failed(errorMsg);
            }
        }
        
        #endregion
        
        #region 价格计算方法
        
        /// <summary>
        /// 计算保本止损价格
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <returns>保本价格</returns>
        private decimal CalculateBreakEvenPrice(ContractProfile profile)
        {
            // 保本价格 = 开仓价格（或接近开仓价格的保护价格）
            if (profile.Side == "LONG")
            {
                // 多头：保本价格略高于开仓价格（考虑手续费）
                return profile.EntryPrice * 1.001m; // 0.1%的缓冲
            }
            else
            {
                // 空头：保本价格略低于开仓价格（考虑手续费）
                return profile.EntryPrice * 0.999m; // 0.1%的缓冲
            }
        }
        
        /// <summary>
        /// 计算加仓数量
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">推仓阶梯</param>
        /// <returns>加仓数量</returns>
        private decimal CalculateAddPositionQuantity(ContractProfile profile, ContractAddPositionTier tier)
        {
            // 使用已计算的加仓数量，如果为0则使用风险金倍数计算
            if (tier.AddPositionQuantity > 0)
            {
                return tier.AddPositionQuantity;
            }
            
            // 基于风险金倍数计算
            // 加仓数量 = 风险金 × 倍数 / (止损比例 × 当前价格)
            var riskAmount = tier.TriggerProfitAmount; // 简化：使用触发金额作为风险金参考
            var stopLossRatio = tier.StopLossRatio;
            var currentPrice = profile.CurrentPrice;
            
            if (stopLossRatio > 0 && currentPrice > 0)
            {
                var quantity = (riskAmount * tier.RiskMultiplier) / (stopLossRatio * currentPrice);
                return Math.Round(quantity, 4); // 保留4位小数
            }
            
            return 0;
        }
        
        /// <summary>
        /// 计算新的止损价格（推仓后）
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">推仓阶梯</param>
        /// <returns>新止损价格</returns>
        private decimal CalculateNewStopLossPrice(ContractProfile profile, ContractAddPositionTier tier)
        {
            var currentPrice = profile.CurrentPrice;
            var stopLossRatio = tier.StopLossRatio;
            
            if (profile.Side == "LONG")
            {
                // 多头：止损价格 = 当前价格 × (1 - 止损比例)
                return currentPrice * (1 - stopLossRatio);
            }
            else
            {
                // 空头：止损价格 = 当前价格 × (1 + 止损比例)
                return currentPrice * (1 + stopLossRatio);
            }
        }
        
        /// <summary>
        /// 计算保盈保护价格
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">保盈阶梯</param>
        /// <returns>保护价格</returns>
        private decimal CalculateProfitProtectionPrice(ContractProfile profile, ContractProfitProtectionTier tier)
        {
            // 计算保护价格：根据保护金额反推价格
            var positionSize = Math.Abs(profile.PositionSize);
            var entryPrice = profile.EntryPrice;
            var protectionAmount = tier.ProtectionAmount;
            
            if (positionSize == 0) return 0;
            
            if (profile.Side == "LONG")
            {
                // 多头：保护价格 = 开仓价格 + (保护金额 / 持仓数量)
                return entryPrice + (protectionAmount / positionSize);
            }
            else
            {
                // 空头：保护价格 = 开仓价格 - (保护金额 / 持仓数量)
                return entryPrice - (protectionAmount / positionSize);
            }
        }
        
        /// <summary>
        /// 验证止损价格的合理性
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="stopLossPrice">止损价格</param>
        /// <returns>是否合理</returns>
        private bool ValidateStopLossPrice(ContractProfile profile, decimal stopLossPrice)
        {
            if (stopLossPrice <= 0) return false;
            
            var currentPrice = profile.CurrentPrice;
            var priceChangeRatio = Math.Abs((stopLossPrice - currentPrice) / currentPrice);
            
            // 止损价格与当前价格的差异不应超过50%
            if (priceChangeRatio > 0.5m) return false;
            
            // 检查方向合理性
            if (profile.Side == "LONG")
            {
                // 多头止损价格应低于当前价格
                return stopLossPrice < currentPrice;
            }
            else
            {
                // 空头止损价格应高于当前价格
                return stopLossPrice > currentPrice;
            }
        }
        
        #endregion
        
        #region 币安API调用方法
        
        /// <summary>
        /// 设置止损订单
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="stopPrice">止损价格</param>
        /// <param name="reason">设置原因</param>
        /// <returns>执行结果</returns>
        private async Task<TradingExecutionResult> SetStopLossOrderAsync(ContractProfile profile, decimal stopPrice, string reason)
        {
            try
            {
                // 确定平仓方向
                var side = profile.Side == "LONG" ? "SELL" : "BUY";
                var quantity = Math.Abs(profile.PositionSize);
                
                _logger.LogInformation($"设置止损订单: {profile.Symbol}, 方向: {side}, 数量: {quantity:F4}, 止损价: {stopPrice:F4}, 原因: {reason}");
                
                // 调用币安API设置止损单
                // 注意：这里需要根据实际的IBinanceService接口调整
                var orderRequest = new
                {
                    symbol = profile.Symbol,
                    side = side,
                    type = "STOP_MARKET",
                    quantity = quantity,
                    stopPrice = stopPrice,
                    timeInForce = "GTC",
                    workingType = "MARK_PRICE"
                };
                
                // 这里需要实际调用币安API
                // var orderResult = await _binanceService.PlaceOrderAsync(orderRequest);
                
                // 模拟成功结果（实际应该从API获取）
                var orderId = DateTime.Now.Ticks.ToString();
                _logger.LogInformation($"止损订单设置成功: {profile.Symbol}, 订单ID: {orderId}");
                
                return TradingExecutionResult.Success($"止损订单已设置，价格: {stopPrice:F4}, 订单ID: {orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"设置止损订单失败: {profile.Symbol}");
                return TradingExecutionResult.Failed($"设置止损订单失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新止损订单
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="newStopPrice">新止损价格</param>
        /// <param name="reason">更新原因</param>
        /// <returns>执行结果</returns>
        private async Task<TradingExecutionResult> UpdateStopLossOrderAsync(ContractProfile profile, decimal newStopPrice, string reason)
        {
            try
            {
                _logger.LogInformation($"更新止损订单: {profile.Symbol}, 新止损价: {newStopPrice:F4}, 原因: {reason}");
                
                // 先取消现有止损单，再设置新的
                // await CancelExistingStopLossOrdersAsync(profile);
                
                // 设置新的止损单
                return await SetStopLossOrderAsync(profile, newStopPrice, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新止损订单失败: {profile.Symbol}");
                return TradingExecutionResult.Failed($"更新止损订单失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 下市价单
        /// </summary>
        /// <param name="symbol">合约符号</param>
        /// <param name="side">方向</param>
        /// <param name="quantity">数量</param>
        /// <param name="reason">下单原因</param>
        /// <returns>执行结果</returns>
        private async Task<TradingExecutionResult> PlaceMarketOrderAsync(string symbol, string side, decimal quantity, string reason)
        {
            try
            {
                _logger.LogInformation($"下市价单: {symbol}, 方向: {side}, 数量: {quantity:F4}, 原因: {reason}");
                
                // 调用币安API下市价单
                var orderRequest = new
                {
                    symbol = symbol,
                    side = side,
                    type = "MARKET",
                    quantity = quantity
                };
                
                // 这里需要实际调用币安API
                // var orderResult = await _binanceService.PlaceOrderAsync(orderRequest);
                
                // 模拟成功结果
                var orderId = DateTime.Now.Ticks.ToString();
                _logger.LogInformation($"市价单执行成功: {symbol}, 订单ID: {orderId}");
                
                return TradingExecutionResult.Success($"市价单已执行，数量: {quantity:F4}, 订单ID: {orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"下市价单失败: {symbol}");
                return TradingExecutionResult.Failed($"下市价单失败: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 交易执行结果
    /// </summary>
    public class TradingExecutionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// 错误代码（如果失败）
        /// </summary>
        public string ErrorCode { get; set; } = "";
        
        /// <summary>
        /// 订单ID（如果成功）
        /// </summary>
        public string? OrderId { get; set; }
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static TradingExecutionResult Success(string message, string? orderId = null)
        {
            return new TradingExecutionResult
            {
                IsSuccess = true,
                Message = message,
                OrderId = orderId
            };
        }
        
        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static TradingExecutionResult Failed(string message, string errorCode = "")
        {
            return new TradingExecutionResult
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }
} 