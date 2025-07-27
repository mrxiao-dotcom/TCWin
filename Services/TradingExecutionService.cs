using System;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text;

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
                
                // 🔧 【模拟模式检查】
                if (IsSimulationMode())
                {
                    _logger.LogInformation($"🎯【模拟模式】{profile.Symbol}: 模拟保本止损执行");
                    
                    // 🎯 模拟下单直接返回成功
                    var simulationResult = TradingExecutionResult.Success($"模拟保本止损成功，价格: {breakEvenPrice:F4}");
                    
                    // 更新配置状态
                    config.BreakEvenPrice = breakEvenPrice;
                    config.IsExecuted = true;
                    config.ExecutionTime = DateTime.Now;
                    config.ExecutionMessage = $"模拟保本止损已设置，价格: {breakEvenPrice:F4}";
                    
                    // 更新档案状态
                    profile.BreakEvenState.ExecutionStatus = "模拟执行";
                    profile.BreakEvenState.ExecutionResult = config.ExecutionMessage;
                    
                    profile.AddOperationHistory("保本执行", "模拟成功", config.ExecutionMessage);
                    
                    _logger.LogInformation($"🎯 模拟保本止损执行成功: {profile.DisplayName}, 止损价格: {breakEvenPrice:F4}");
                    
                    return simulationResult;
                }
                
                // 🔧 【实盘模式】正常执行
                
                // 验证价格合理性
                if (!ValidateStopLossPrice(profile, breakEvenPrice))
                {
                    var errorMsg = $"保本止损价格不合理: {breakEvenPrice:F4}";
                    _logger.LogWarning(errorMsg);
                    return TradingExecutionResult.Failed(errorMsg);
                }
                
                // 设置止损订单
                var orderResult = await SetStopLossOrderAsync(profile, breakEvenPrice, "保本止损");
                
                _logger.LogCritical($"🔍【保本执行调试】{profile.Symbol}: SetStopLossOrderAsync返回结果");
                _logger.LogCritical($"   📈 IsSuccess: {orderResult?.IsSuccess}");
                _logger.LogCritical($"   📝 Message: {orderResult?.Message}");
                _logger.LogCritical($"   🎯 止损价格: {breakEvenPrice:F4}");
                
                if (orderResult?.IsSuccess == true)
                {
                    // 更新配置状态
                    config.BreakEvenPrice = breakEvenPrice;
                    config.IsExecuted = true;
                    config.ExecutionTime = DateTime.Now;
                    config.ExecutionMessage = $"保本止损已设置，价格: {breakEvenPrice:F4}";
                    
                    // 更新档案状态
                    profile.BreakEvenState.ExecutionStatus = StatusConstants.Executed;
                    profile.BreakEvenState.ExecutionResult = config.ExecutionMessage;
                    
                    profile.AddOperationHistory("保本执行", "成功", config.ExecutionMessage);
                    
                    _logger.LogInformation($"保本止损执行成功: {profile.DisplayName}, 止损价格: {breakEvenPrice:F4}");
                }
                else
                {
                    _logger.LogError($"保本止损执行失败: {profile.DisplayName}, 原因: {orderResult?.Message ?? "返回结果为null"}");
                }
                
                return orderResult ?? TradingExecutionResult.Failed("保本执行返回null");
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
        /// <param name="tier">推仓阶梯</param>
        /// <returns>执行结果</returns>
        public async Task<TradingExecutionResult> ExecuteAddPositionAsync(ContractProfile profile, ContractAddPositionTier tier)
        {
            try
            {
                _logger.LogInformation($"🚀 开始执行推仓: {profile.DisplayName}, 阶梯{tier.TierIndex}, 触发金额: {tier.TriggerProfitAmount:F2}U");
                
                // 🔧 【第一步】：获取实时价格和交易规则
                var currentPrice = await GetLatestPriceAsync(profile.Symbol);
                if (currentPrice <= 0)
                {
                    return TradingExecutionResult.Failed($"无法获取{profile.Symbol}的实时价格");
                }
                
                var tradingRules = await GetTradingRulesAsync(profile.Symbol);
                if (!tradingRules.IsValid)
                {
                    return TradingExecutionResult.Failed($"无法获取{profile.Symbol}的交易规则");
                }
                
                // 🔧 【第二步】：计算本次开仓的总市值
                var positionValue = CalculateAddPositionValue(profile, tier, currentPrice);
                if (positionValue <= 0)
                {
                    return TradingExecutionResult.Failed($"计算加仓市值失败: {positionValue}");
                }
                
                // 🔧 【第三步】：计算精确的加仓数量
                var addQuantity = positionValue / currentPrice;
                addQuantity = AdjustQuantityToPrecision(addQuantity, tradingRules);
                
                if (addQuantity < tradingRules.MinQuantity)
                {
                    return TradingExecutionResult.Failed($"加仓数量{addQuantity:F6}小于最小交易量{tradingRules.MinQuantity:F6}");
                }
                
                _logger.LogInformation($"💰 推仓计算结果: 市值={positionValue:F2}U, 价格={currentPrice:F4}, 数量={addQuantity:F6}");
                
                // 🔧 【第四步】：检查模拟模式
                var isSimulation = IsSimulationMode();
                _logger.LogCritical($"🎯 【重要】推仓执行模式检查: {(isSimulation ? "模拟模式" : "实盘模式")}");
                
                if (isSimulation)
                {
                    _logger.LogWarning($"⚠️ 【模拟模式】推仓不会进行真实下单，仅更新状态和日志");
                    _logger.LogWarning($"💡 如需真实下单，请确保API配置有效并重启服务");
                    return await ExecuteSimulatedAddPosition(profile, tier, addQuantity, currentPrice, positionValue);
                }
                
                // 🔧 【第五步】：执行实际加仓下单
                var side = profile.Side == "LONG" ? "BUY" : "SELL";
                var orderResult = await PlaceMarketOrderAsync(profile.Symbol, side, addQuantity, $"推仓阶梯{tier.TierIndex}", profile.Side);
                
                if (!orderResult.IsSuccess)
                {
                    return TradingExecutionResult.Failed($"加仓下单失败: {orderResult.Message}");
                }
                
                _logger.LogInformation($"✅ 加仓下单成功: {profile.Symbol} {side} {addQuantity:F6}");
                
                // 🔧 【第六步】：获取加仓后的持仓信息
                await Task.Delay(1000); // 等待订单确认
                var updatedPosition = await GetUpdatedPositionAsync(profile.Symbol);
                if (updatedPosition == null)
                {
                    _logger.LogWarning($"⚠️ 无法获取加仓后的持仓信息: {profile.Symbol}");
                    return TradingExecutionResult.Success($"加仓完成，但无法获取最新持仓信息");
                }
                
                // 🔧 【第七步】：计算并设置新的止损委托
                var stopLossResult = await UpdateStopLossAfterAddPosition(profile, tier, updatedPosition);
                
                // 🔧 【第八步】：更新阶梯状态
                UpdateTierState(tier, addQuantity, currentPrice, positionValue, stopLossResult.StopLossPrice);
                
                _logger.LogInformation($"🎯 推仓执行完成: {profile.Symbol}-阶梯{tier.TierIndex}, 加仓数量: {addQuantity:F6}, 止损价: {stopLossResult.StopLossPrice:F4}");
                
                return TradingExecutionResult.Success($"推仓阶梯{tier.TierIndex}执行成功: 加仓{addQuantity:F6}, 止损价{stopLossResult.StopLossPrice:F4}");
            }
            catch (Exception ex)
            {
                var errorMsg = $"推仓执行异常: {ex.Message}";
                _logger.LogError(ex, errorMsg);
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
                
                // 🔧 【模拟模式检查】
                if (IsSimulationMode())
                {
                    _logger.LogInformation($"🎯【模拟模式】{profile.Symbol}-阶梯{tier.TierIndex}: 模拟保盈止损执行");
                    
                    // 🎯 模拟下单直接返回成功
                    var simulationResult = TradingExecutionResult.Success($"模拟保盈止损成功，阶梯{tier.TierIndex}，止损价: {protectionPrice:F4}");
                    
                    // 更新阶梯状态
                    tier.StopLossPrice = protectionPrice;
                    tier.IsExecuted = true;
                    tier.ExecutionTime = DateTime.Now;
                    tier.ExecutionMessage = $"模拟保盈阶梯{tier.TierIndex}已{StatusConstants.Executed}，保护价格: {protectionPrice:F4}";
                    
                    // 更新档案状态
                    var tierState = profile.ProfitProtectionStates.Find(s => s.TierIndex == tier.TierIndex);
                    if (tierState != null)
                    {
                        tierState.ExecutionStatus = "模拟执行";
                        tierState.ExecutionResult = tier.ExecutionMessage;
                    }
                    
                    profile.AddOperationHistory("保盈执行", "模拟成功", tier.ExecutionMessage);
                    
                    _logger.LogInformation($"🎯 模拟保盈止损执行成功: {profile.DisplayName}, 阶梯{tier.TierIndex}, 止损价格: {protectionPrice:F4}");
                    
                    return simulationResult;
                }
                
                // 🔧 【实盘模式】正常执行
                // 设置/更新止损订单
                var orderResult = await UpdateStopLossOrderAsync(profile, protectionPrice, $"保盈阶梯{tier.TierIndex}");
                
                if (orderResult.IsSuccess)
                {
                    // 更新阶梯状态
                    tier.StopLossPrice = protectionPrice;
                    tier.IsExecuted = true;
                    tier.ExecutionTime = DateTime.Now;
                    tier.ExecutionMessage = $"保盈阶梯{tier.TierIndex}已{StatusConstants.Executed}，保护价格: {protectionPrice:F4}";
                    
                    // 更新档案状态
                    var tierState = profile.ProfitProtectionStates.Find(s => s.TierIndex == tier.TierIndex);
                    if (tierState != null)
                    {
                        tierState.ExecutionStatus = StatusConstants.Executed;
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
            // 检查关键参数
            if (profile.EntryPrice <= 0)
            {
                var errorMsg = $"开仓价格无效: {profile.EntryPrice}，无法计算保本价格";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
            
            // 🔧 【重要修复】保本止损价格应该以持仓成本价为基准
            var entryPrice = profile.EntryPrice;
            
            _logger.LogCritical($"🔍【保本价格计算】{profile.Symbol}:");
            _logger.LogCritical($"   📊 持仓方向: {profile.Side}");
            _logger.LogCritical($"   💰 持仓成本价: {entryPrice:F4}");
            _logger.LogCritical($"   🎯 保本策略: 以成本价为基准，确保真正保本");
            
            // 🔧 修复：直接使用成本价作为止损价格
            // 这样确保触发时真正保本，而不是略微亏损
            var breakEvenPrice = entryPrice;
            
            _logger.LogCritical($"   ✅ 计算结果: 止损价格 = {breakEvenPrice:F4} (等于成本价)");
            
            return breakEvenPrice;
        }
        
        /// <summary>
        /// 计算加仓数量
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">推仓阶梯</param>
        /// <returns>加仓数量</returns>
        private decimal CalculateAddPositionQuantity(ContractProfile profile, ContractAddPositionTier tier)
        {
            try
            {
                // 检查关键参数
                if (profile.EntryPrice <= 0)
                {
                    _logger.LogError($"开仓价格无效: {profile.EntryPrice}，无法计算推仓数量");
                    return 0;
                }
                
                if (profile.PositionSize <= 0)
                {
                    _logger.LogError($"持仓数量无效: {profile.PositionSize}，无法计算推仓数量");
                    return 0;
                }
                
                // 🚨 修复：使用正确的推仓计算逻辑
                // 1. 获取账户权益（这里需要从服务中获取，暂时使用持仓估算）
                var accountEquity = profile.PositionSize * profile.EntryPrice * 10; // 简化估算
                var riskTimes = 8; // 默认风险次数
                
                if (riskTimes <= 0)
                {
                    _logger.LogError($"风险次数无效: {riskTimes}，无法计算推仓数量");
                    return 0;
                }
                
                var singleRiskCapital = accountEquity / riskTimes;
                
                // 2. 从配置获取参数
                var riskMultiplier = tier.RiskMultiplier;
                var stopLossRatio = tier.StopLossRatio;
                var currentPrice = profile.CurrentPrice;
                
                if (stopLossRatio <= 0 || currentPrice <= 0)
                {
                    _logger.LogWarning($"推仓计算参数无效: 止损比例={stopLossRatio}, 当前价格={currentPrice}");
                    return 0;
                }
                
                // 3. 计算推仓货值和数量
                var addPositionValue = riskMultiplier * singleRiskCapital / stopLossRatio;
                var addQuantity = addPositionValue / currentPrice;
                
                _logger.LogInformation($"推仓计算: {profile.Symbol} 货值={addPositionValue:F2}U, 数量={addQuantity:F6}");
                
                return Math.Round(addQuantity, 6);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算推仓数量失败: {profile.Symbol}");
                return 0;
            }
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
        
        /// <summary>
        /// 检查当前是否为模拟环境
        /// </summary>
        /// <returns>是否为模拟模式</returns>
        private bool IsSimulationMode()
        {
            try
            {
                // 🔧 【关键修复】首先检查BinanceService的IP限制状态
                if (BinanceService.IsIpRestricted)
                {
                    _logger.LogDebug($"🔍 TradingExecutionService模拟环境检查: IP受限模式，判断结果=true");
                    return true;
                }
                
                // 🔧 检查BinanceService的API配置来判断是否为模拟环境
                if (_binanceService == null) return true;
                
                // 通过反射检查API配置
                var accountProperty = _binanceService.GetType().GetProperty("CurrentAccount");
                if (accountProperty != null)
                {
                    var currentAccount = accountProperty.GetValue(_binanceService);
                    if (currentAccount != null)
                    {
                        var apiKeyProperty = currentAccount.GetType().GetProperty("ApiKey");
                        var secretKeyProperty = currentAccount.GetType().GetProperty("SecretKey");
                        
                        if (apiKeyProperty != null && secretKeyProperty != null)
                        {
                            var apiKey = apiKeyProperty.GetValue(currentAccount) as string;
                            var secretKey = secretKeyProperty.GetValue(currentAccount) as string;
                            
                            // 如果API Key或Secret Key为空，或者长度不足，认为是模拟环境
                            bool isSimulation = string.IsNullOrEmpty(apiKey) || 
                                               string.IsNullOrEmpty(secretKey) ||
                                               apiKey.Length < 10 || 
                                               secretKey.Length < 10;
                            
                            _logger.LogDebug($"🔍 TradingExecutionService模拟环境检查: API Key长度={apiKey?.Length ?? 0}, Secret Key长度={secretKey?.Length ?? 0}, IP受限={BinanceService.IsIpRestricted}, 判断结果={isSimulation || BinanceService.IsIpRestricted}");
                            return isSimulation;
                        }
                    }
                }
                
                // 默认返回模拟模式
                _logger.LogDebug($"🔍 TradingExecutionService模拟环境检查: 无法获取API配置，默认返回true");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查模拟环境失败，默认返回模拟模式");
                return true;
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
                _logger.LogInformation($"设置止损订单: {profile.Symbol}, 止损价: {stopPrice:F4}, 原因: {reason}");
                
                // 🔧 【关键修复】检查模拟模式
                if (IsSimulationMode())
                {
                    _logger.LogInformation($"🎯【模拟模式】{profile.Symbol}: 模拟止损订单设置");
                    await Task.Delay(200); // 模拟网络延迟
                    var simulationMsg = $"模拟止损订单设置成功: {profile.Symbol} @ {stopPrice:F4}";
                    _logger.LogInformation($"✅ {simulationMsg}");
                    return TradingExecutionResult.Success(simulationMsg);
                }
                
                // 🔧 计算持仓数量和方向
                var quantity = Math.Abs(profile.PositionSize);
                var side = profile.Side == "LONG" ? "SELL" : "BUY"; // 止损与持仓方向相反
                
                // 🔧 构造止损订单请求
                var orderRequest = new OrderRequest
                {
                    Symbol = profile.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    ReduceOnly = true,  // 🔧 止损订单应该设置为减仓
                    PositionSide = profile.Side == "LONG" ? "LONG" : "SHORT",  // 双向持仓模式
                    TimeInForce = "GTC",
                    WorkingType = "MARK_PRICE"  // 🔧 使用标记价格触发，更可靠
                };
                
                // 🔍 SetStopLossOrderAsync 诊断
                _logger.LogCritical($"🎯 SetStopLossOrderAsync参数:");
                _logger.LogCritical($"   Symbol: {orderRequest.Symbol}");
                _logger.LogCritical($"   Side: {orderRequest.Side}");
                _logger.LogCritical($"   Type: {orderRequest.Type}");
                _logger.LogCritical($"   Quantity: {orderRequest.Quantity:F6}");
                _logger.LogCritical($"   StopPrice: {orderRequest.StopPrice:F4}");
                _logger.LogCritical($"   TimeInForce: {orderRequest.TimeInForce}");
                _logger.LogCritical($"   WorkingType: {orderRequest.WorkingType}");
                _logger.LogCritical($"   ReduceOnly: {orderRequest.ReduceOnly}");
                
                var orderResult = await _binanceService.PlaceOrderAsync(orderRequest);
                
                // 🔍 【API结果诊断】详细记录API调用结果
                _logger.LogCritical($"📈【API结果诊断】PlaceOrderAsync返回结果: {orderResult}");
                _logger.LogCritical($"   是否成功: {orderResult}");
                _logger.LogCritical($"   下一步: {(orderResult ? "返回成功结果" : "返回失败结果")}");
                
                if (orderResult)
                {
                    var successMsg = $"✅ 止损订单设置成功: {profile.Symbol} @ {stopPrice:F4}";
                    _logger.LogInformation(successMsg);
                    _logger.LogCritical($"🎉【成功诊断】{successMsg}，将返回Success结果");
                    return TradingExecutionResult.Success($"止损订单已设置，价格: {stopPrice:F4}");
                }
                else
                {
                    var errorMsg = $"❌ 止损订单设置失败: {profile.Symbol}";
                    _logger.LogError(errorMsg);
                    _logger.LogCritical($"💥【失败诊断】{errorMsg}，将返回Failed结果");
                    return TradingExecutionResult.Failed($"止损订单设置失败");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"设置止损订单失败: {profile.Symbol}";
                _logger.LogError(ex, errorMsg);
                _logger.LogCritical($"💥【异常诊断】{errorMsg}: {ex.Message}，将返回Failed结果");
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
                
                // 🔧 【关键修复】检查模拟模式
                if (IsSimulationMode())
                {
                    _logger.LogInformation($"🎯【模拟模式】{profile.Symbol}: 模拟更新止损订单");
                    await Task.Delay(200); // 模拟网络延迟
                    var simulationMsg = $"模拟更新止损订单成功: {profile.Symbol} @ {newStopPrice:F4}";
                    _logger.LogInformation($"✅ {simulationMsg}");
                    return TradingExecutionResult.Success(simulationMsg);
                }
                
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
        /// <param name="positionSide">持仓方向(LONG/SHORT)</param>
        /// <returns>执行结果</returns>
        private async Task<TradingExecutionResult> PlaceMarketOrderAsync(string symbol, string side, decimal quantity, string reason, string positionSide = "BOTH")
        {
            try
            {
                _logger.LogInformation($"🚀 准备下市价单: {symbol}, 方向: {side}, 数量: {quantity:F4}, 原因: {reason}");
                
                // 🔧 【关键修复】检查模拟模式
                if (IsSimulationMode())
                {
                    _logger.LogInformation($"🎯【模拟模式】{symbol}: 模拟市价单执行");
                    await Task.Delay(200); // 模拟网络延迟
                    var simulationMsg = $"模拟市价单成功: {side} {quantity:F6} {symbol}";
                    _logger.LogInformation($"✅ {simulationMsg}");
                    return TradingExecutionResult.Success(simulationMsg);
                }
                
                // 🔧 调用币安API（实盘模式）
                try
                {
                    _logger.LogInformation($"📡 调用币安API下单: {symbol}");
                    
                    // 构造订单请求
                    var orderRequest = new OrderRequest
                    {
                        Symbol = symbol,
                        Side = side,
                        Type = "MARKET",
                        Quantity = quantity,
                        PositionSide = positionSide,  // 🚨 关键修复：添加持仓方向参数
                        ReduceOnly = false  // 🔧 确保推仓时不设置为减仓
                    };
                    
                    // 🔍 PlaceMarketOrderAsync 诊断
                    _logger.LogCritical($"🚀 PlaceMarketOrderAsync参数:");
                    _logger.LogCritical($"   Symbol: {orderRequest.Symbol}");
                    _logger.LogCritical($"   Side: {orderRequest.Side}");
                    _logger.LogCritical($"   Type: {orderRequest.Type}");
                    _logger.LogCritical($"   Quantity: {orderRequest.Quantity:F6}");
                    _logger.LogCritical($"   PositionSide: {orderRequest.PositionSide} (关键参数！)");
                    _logger.LogCritical($"   ReduceOnly: {orderRequest.ReduceOnly} (推仓应为false)");
                    _logger.LogCritical($"   原因: {reason}");
                    
                    // 调用币安API下市价单
                    var orderResult = await _binanceService.PlaceOrderAsync(orderRequest);
                    
                    if (orderResult)
                    {
                        _logger.LogInformation($"✅ 市价单执行成功: {symbol}");
                        return TradingExecutionResult.Success($"市价单已{StatusConstants.Executed}，数量: {quantity:F4}");
                    }
                    else
                    {
                        _logger.LogError($"❌ 币安API下单失败: {symbol}");
                        return TradingExecutionResult.Failed($"币安API下单失败");
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, $"❌ 币安API调用失败: {symbol}");
                    return TradingExecutionResult.Failed($"币安API调用失败: {apiEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 下市价单失败: {symbol}");
                return TradingExecutionResult.Failed($"下市价单失败: {ex.Message}");
            }
        }
        
        #endregion

        /// <summary>
        /// 计算加仓市值
        /// </summary>
        private decimal CalculateAddPositionValue(ContractProfile profile, ContractAddPositionTier tier, decimal currentPrice)
        {
            try
            {
                // 🔧 【核心计算公式】：根据风险金倍数与止损比例计算总市值
                // 公式：总市值 = (风险金倍数 × 单笔风险金) ÷ 止损比例
                
                // 1. 获取账户信息计算单笔风险金
                var accountEquity = GetAccountEquity();
                var riskTimes = GetRiskTimes();
                var singleRiskCapital = accountEquity / riskTimes;
                
                // 2. 应用阶梯配置
                var riskMultiplier = tier.RiskMultiplier;
                var stopLossRatio = tier.StopLossRatio;
                
                // 3. 计算总市值
                var totalValue = (riskMultiplier * singleRiskCapital) / stopLossRatio;
                
                _logger.LogInformation($"💰 市值计算: 账户权益={accountEquity:F2}U, 风险次数={riskTimes}, " +
                    $"单笔风险金={singleRiskCapital:F2}U, 风险倍数={riskMultiplier:F2}, " +
                    $"止损比例={stopLossRatio:F4}, 总市值={totalValue:F2}U");
                
                return totalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算加仓市值失败: {profile.Symbol}");
                return 0;
            }
        }
        
        /// <summary>
        /// 获取实时价格
        /// </summary>
        private async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var price = await _binanceService.GetLatestPriceAsync(symbol);
                return price;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取{symbol}实时价格失败");
                return 0;
            }
        }
        
        /// <summary>
        /// 获取交易规则
        /// </summary>
        private async Task<TradingRules> GetTradingRulesAsync(string symbol)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(symbol);
                
                return new TradingRules
                {
                    IsValid = true,
                    MinQuantity = minQty,
                    MaxQuantity = maxQty,
                    StepSize = stepSize,
                    TickSize = tickSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取{symbol}交易规则失败");
                return new TradingRules { IsValid = false };
            }
        }
        
        /// <summary>
        /// 调整数量精度
        /// </summary>
        private decimal AdjustQuantityToPrecision(decimal quantity, TradingRules rules)
        {
            if (!rules.IsValid || rules.StepSize <= 0)
            {
                return Math.Round(quantity, 6);
            }
            
            // 向下调整到步长精度
            return Math.Floor(quantity / rules.StepSize) * rules.StepSize;
        }
        
        /// <summary>
        /// 执行模拟加仓
        /// </summary>
        private async Task<TradingExecutionResult> ExecuteSimulatedAddPosition(
            ContractProfile profile, 
            ContractAddPositionTier tier, 
            decimal addQuantity, 
            decimal currentPrice,
            decimal positionValue)
        {
            _logger.LogInformation($"🎯【模拟推仓】{profile.Symbol}-阶梯{tier.TierIndex}: 市值={positionValue:F2}U, 数量={addQuantity:F6}");
            
            // 计算模拟止损价格
            var simulatedStopPrice = CalculateStopLossPrice(profile, tier, currentPrice, addQuantity);
            
            // 更新阶梯状态
            UpdateTierState(tier, addQuantity, currentPrice, positionValue, simulatedStopPrice);
            
            // 更新档案状态
            var tierState = profile.AddPositionStates.Find(s => s.TierIndex == tier.TierIndex);
            if (tierState != null)
            {
                tierState.ExecutionStatus = "模拟执行";
                tierState.ExecutionResult = $"模拟推仓: 市值{positionValue:F2}U, 数量{addQuantity:F6}, 止损价{simulatedStopPrice:F4}";
            }
            
            profile.AddOperationHistory("推仓执行", "模拟成功", tierState?.ExecutionResult ?? "模拟推仓成功");
            
            return TradingExecutionResult.Success($"模拟推仓成功: 阶梯{tier.TierIndex}, 市值{positionValue:F2}U, 数量{addQuantity:F6}");
        }
        
        /// <summary>
        /// 获取加仓后的持仓信息
        /// </summary>
        private async Task<PositionInfo?> GetUpdatedPositionAsync(string symbol)
        {
            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                return positions.FirstOrDefault(p => p.Symbol == symbol && Math.Abs(p.PositionAmt) > 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取加仓后持仓信息失败: {symbol}");
                return null;
            }
        }
        
        /// <summary>
        /// 加仓后更新止损委托
        /// </summary>
        private async Task<StopLossResult> UpdateStopLossAfterAddPosition(
            ContractProfile profile, 
            ContractAddPositionTier tier, 
            PositionInfo updatedPosition)
        {
            try
            {
                // 计算新的止损价格（基于保盈目标）
                var newStopPrice = CalculateStopLossPrice(profile, tier, updatedPosition.MarkPrice, Math.Abs(updatedPosition.PositionAmt));
                
                _logger.LogInformation($"🎯 计算新止损价: {profile.Symbol} 成本价={updatedPosition.EntryPrice:F4}, 保盈目标={tier.ProfitProtectionAmount:F2}U, 止损价={newStopPrice:F4}");
                
                // 🔧 【关键】：撤销旧的止损委托，设置新的止损委托
                var cancelResult = await CancelExistingStopLossOrders(profile.Symbol);
                if (!cancelResult.IsSuccess)
                {
                    _logger.LogWarning($"⚠️ 撤销旧止损委托失败: {cancelResult.Message}");
                }
                
                // 设置新的止损委托
                var stopOrderResult = await PlaceStopLossOrderAsync(profile, newStopPrice, Math.Abs(updatedPosition.PositionAmt));
                
                return new StopLossResult
                {
                    IsSuccess = stopOrderResult.IsSuccess,
                    StopLossPrice = newStopPrice,
                    Message = stopOrderResult.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新止损委托失败: {profile.Symbol}");
                return new StopLossResult
                {
                    IsSuccess = false,
                    StopLossPrice = 0,
                    Message = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 计算止损价格（考虑保盈目标）
        /// </summary>
        private decimal CalculateStopLossPrice(ContractProfile profile, ContractAddPositionTier tier, decimal currentPrice, decimal totalQuantity)
        {
            var entryPrice = profile.EntryPrice;
            var profitProtectionAmount = tier.ProfitProtectionAmount;
            
            if (totalQuantity <= 0)
            {
                _logger.LogWarning($"持仓数量无效: {totalQuantity}，使用基本止损价计算");
                return profile.Side == "LONG" 
                    ? entryPrice * (1 - tier.StopLossRatio)
                    : entryPrice * (1 + tier.StopLossRatio);
            }
            
            // 根据保盈目标计算止损价
            if (profile.Side == "LONG")
            {
                // 多头：止损价 = 成本价 + (保盈金额 / 持仓数量)
                return entryPrice + (profitProtectionAmount / totalQuantity);
            }
            else
            {
                // 空头：止损价 = 成本价 - (保盈金额 / 持仓数量)
                return entryPrice - (profitProtectionAmount / totalQuantity);
            }
        }
        
        /// <summary>
        /// 撤销现有的止损委托
        /// </summary>
        private async Task<TradingExecutionResult> CancelExistingStopLossOrders(string symbol)
        {
            try
            {
                // 获取当前未完成的委托单
                var openOrders = await _binanceService.GetOpenOrdersAsync(symbol);
                var stopLossOrders = openOrders?.Where(o => o.Type.Contains("STOP") || o.Type.Contains("TAKE_PROFIT")).ToList();
                
                if (stopLossOrders?.Any() == true)
                {
                    _logger.LogInformation($"🔧 撤销{stopLossOrders.Count}个现有止损委托: {symbol}");
                    
                    foreach (var order in stopLossOrders)
                    {
                        try
                        {
                            await _binanceService.CancelOrderAsync(symbol, order.OrderId);
                            _logger.LogInformation($"✅ 已撤销委托: {symbol} OrderId={order.OrderId}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"⚠️ 撤销委托失败: {symbol} OrderId={order.OrderId}");
                        }
                    }
                }
                
                return TradingExecutionResult.Success($"撤销委托完成: {stopLossOrders?.Count ?? 0}个");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"撤销止损委托异常: {symbol}");
                return TradingExecutionResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// 下止损委托单
        /// </summary>
        private async Task<TradingExecutionResult> PlaceStopLossOrderAsync(ContractProfile profile, decimal stopPrice, decimal quantity)
        {
            try
            {
                var side = profile.Side == "LONG" ? "SELL" : "BUY"; // 止损方向与持仓方向相反
                
                var stopOrderRequest = new OrderRequest
                {
                    Symbol = profile.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    TimeInForce = "GTC",
                    ReduceOnly = true // 止损委托必须是减仓
                };
                
                _logger.LogInformation($"🛡️ 下止损委托: {profile.Symbol} {side} {quantity:F6} @ StopPrice={stopPrice:F4}");
                
                var result = await _binanceService.PlaceOrderAsync(stopOrderRequest);
                
                if (result)
                {
                    _logger.LogInformation($"✅ 止损委托下单成功: {profile.Symbol}");
                    return TradingExecutionResult.Success($"止损委托成功: {stopPrice:F4}");
                }
                else
                {
                    _logger.LogError($"❌ 止损委托下单失败: {profile.Symbol}");
                    return TradingExecutionResult.Failed($"止损委托失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"下止损委托异常: {profile.Symbol}");
                return TradingExecutionResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// 更新阶梯状态
        /// </summary>
        private void UpdateTierState(ContractAddPositionTier tier, decimal addQuantity, decimal currentPrice, decimal positionValue, decimal stopLossPrice)
        {
            tier.AddPositionQuantity = addQuantity;
            tier.StopLossPrice = stopLossPrice;
            tier.IsExecuted = true;
            tier.ExecutionTime = DateTime.Now;
                            tier.ExecutionMessage = $"推仓阶梯{tier.TierIndex}已{StatusConstants.Executed}: 市值{positionValue:F2}U, 数量{addQuantity:F6}, 价格{currentPrice:F4}, 止损{stopLossPrice:F4}";
        }
        
        /// <summary>
        /// 获取账户权益
        /// </summary>
        private decimal GetAccountEquity()
        {
            try
            {
                // 从服务或依赖注入获取账户信息
                // 这里需要根据实际架构调整
                return 10000m; // 临时默认值，实际应该从BinanceService获取
            }
            catch
            {
                return 10000m; // 默认账户权益
            }
        }
        
        /// <summary>
        /// 获取风险次数
        /// </summary>
        private int GetRiskTimes()
        {
            try
            {
                // 从配置或账户设置获取风险次数
                return 8; // 临时默认值
            }
            catch
            {
                return 8; // 默认风险次数
            }
        }
        
        /// <summary>
        /// 交易规则
        /// </summary>
        private class TradingRules
        {
            public bool IsValid { get; set; }
            public decimal MinQuantity { get; set; }
            public decimal MaxQuantity { get; set; }
            public decimal StepSize { get; set; }
            public decimal TickSize { get; set; }
        }
        
        /// <summary>
        /// 止损结果
        /// </summary>
        private class StopLossResult
        {
            public bool IsSuccess { get; set; }
            public decimal StopLossPrice { get; set; }
            public string Message { get; set; } = "";
        }

        /// <summary>
        /// 诊断推仓执行状态和模式
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="tier">推仓阶梯</param>
        /// <returns>诊断信息</returns>
        public string DiagnoseAddPositionExecution(ContractProfile profile, ContractAddPositionTier tier)
        {
            var diagnostic = new System.Text.StringBuilder();
            diagnostic.AppendLine($"🔍 【推仓执行诊断】{profile.Symbol}-阶梯{tier.TierIndex}");
            diagnostic.AppendLine("═══════════════════════════════════════");
            
            // 1. 检查运行模式
            var isSimulation = IsSimulationMode();
            diagnostic.AppendLine($"🎯 运行模式: {(isSimulation ? "模拟模式" : "实盘模式")}");
            
            if (isSimulation)
            {
                diagnostic.AppendLine("   💡 模拟模式说明:");
                diagnostic.AppendLine("   • 不会进行真实下单");
                diagnostic.AppendLine("   • 只记录日志和更新状态");
                diagnostic.AppendLine("   • 用于测试和验证逻辑");
                diagnostic.AppendLine("   • 实际持仓不会发生变化");
            }
            else
            {
                diagnostic.AppendLine("   💰 实盘模式说明:");
                diagnostic.AppendLine("   • 会进行真实API下单");
                diagnostic.AppendLine("   • 实际持仓会发生变化");
                diagnostic.AppendLine("   • 需要有效的API配置");
            }
            
            // 2. 检查API配置
            diagnostic.AppendLine();
            diagnostic.AppendLine($"🔑 API配置检查:");
            try
            {
                if (_binanceService == null)
                {
                    diagnostic.AppendLine("   ❌ BinanceService未初始化");
                }
                else
                {
                    var accountProperty = _binanceService.GetType().GetProperty("CurrentAccount");
                    if (accountProperty != null)
                    {
                        var currentAccount = accountProperty.GetValue(_binanceService);
                        if (currentAccount != null)
                        {
                            var apiKeyProperty = currentAccount.GetType().GetProperty("ApiKey");
                            var secretKeyProperty = currentAccount.GetType().GetProperty("SecretKey");
                            
                            if (apiKeyProperty != null && secretKeyProperty != null)
                            {
                                var apiKey = apiKeyProperty.GetValue(currentAccount) as string;
                                var secretKey = secretKeyProperty.GetValue(currentAccount) as string;
                                
                                diagnostic.AppendLine($"   API Key: {(string.IsNullOrEmpty(apiKey) ? "❌ 未设置" : $"✅ 已设置 (长度: {apiKey.Length})")}");
                                diagnostic.AppendLine($"   Secret Key: {(string.IsNullOrEmpty(secretKey) ? "❌ 未设置" : $"✅ 已设置 (长度: {secretKey.Length})")}");
                                
                                if (!string.IsNullOrEmpty(apiKey) && apiKey.Length >= 10 && 
                                    !string.IsNullOrEmpty(secretKey) && secretKey.Length >= 10)
                                {
                                    diagnostic.AppendLine("   ✅ API配置看起来有效");
                                }
                                else
                                {
                                    diagnostic.AppendLine("   ⚠️ API配置可能无效");
                                }
                            }
                        }
                        else
                        {
                            diagnostic.AppendLine("   ❌ 当前账户未设置");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostic.AppendLine($"   ❌ 检查API配置时出错: {ex.Message}");
            }
            
            // 3. 检查推仓配置
            diagnostic.AppendLine();
            diagnostic.AppendLine($"📊 推仓配置:");
            diagnostic.AppendLine($"   阶梯索引: {tier.TierIndex}");
            diagnostic.AppendLine($"   触发金额: {tier.TriggerProfitAmount:F2}U");
            diagnostic.AppendLine($"   风险倍数: {tier.RiskMultiplier:F2}");
            diagnostic.AppendLine($"   止损比例: {tier.StopLossRatio:F4} ({tier.StopLossRatio * 100:F2}%)");
            diagnostic.AppendLine($"   是否已执行: {tier.IsExecuted}");
            
            if (tier.IsExecuted)
            {
                diagnostic.AppendLine($"   执行时间: {tier.ExecutionTime}");
                diagnostic.AppendLine($"   执行消息: {tier.ExecutionMessage}");
            }
            
            // 4. 检查档案状态
            diagnostic.AppendLine();
            diagnostic.AppendLine($"📈 合约档案状态:");
            diagnostic.AppendLine($"   合约: {profile.Symbol}");
            diagnostic.AppendLine($"   方向: {profile.Side}");
            diagnostic.AppendLine($"   持仓大小: {profile.PositionSize:F6}");
            diagnostic.AppendLine($"   入场价格: {profile.EntryPrice:F4}");
            diagnostic.AppendLine($"   当前价格: {profile.CurrentPrice:F4}");
            diagnostic.AppendLine($"   未实现盈亏: {profile.UnrealizedPnl:F2}U");
            
            var tierState = profile.AddPositionStates.FirstOrDefault(s => s.TierIndex == tier.TierIndex);
            if (tierState != null)
            {
                diagnostic.AppendLine($"   阶梯状态: {tierState.ExecutionStatus}");
                diagnostic.AppendLine($"   执行结果: {tierState.ExecutionResult}");
            }
            
            // 5. 给出建议
            diagnostic.AppendLine();
            diagnostic.AppendLine($"💡 建议:");
            
            if (isSimulation)
            {
                diagnostic.AppendLine("   🔧 如果想要真实下单:");
                diagnostic.AppendLine("   1. 确保设置了有效的API Key和Secret Key");
                diagnostic.AppendLine("   2. API Key长度至少10位");
                diagnostic.AppendLine("   3. Secret Key长度至少10位");
                diagnostic.AppendLine("   4. 确保API权限包含期货交易");
                diagnostic.AppendLine("   5. 重新启动自动盯盘服务");
            }
            else
            {
                diagnostic.AppendLine("   ✅ 当前为实盘模式，推仓将进行真实下单");
                diagnostic.AppendLine("   ⚠️ 请确认风险承受能力");
                diagnostic.AppendLine("   💰 建议先在模拟模式下测试");
            }
            
            return diagnostic.ToString();
        }
        
        /// <summary>
        /// 获取推仓执行模式信息
        /// </summary>
        /// <returns>执行模式信息</returns>
        public string GetExecutionModeInfo()
        {
            var isSimulation = IsSimulationMode();
            var mode = isSimulation ? "模拟模式" : "实盘模式";
            var description = isSimulation ? 
                "不会进行真实下单，仅记录日志和更新状态" : 
                "会进行真实API下单，实际持仓会发生变化";
                
            return $"🎯 当前执行模式: {mode}\n💡 说明: {description}";
        }
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