using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 智能下单服务 - 提供杠杆自动调节和分笔止损委托功能
    /// </summary>
    public class SmartOrderService
    {
        private readonly IBinanceService _binanceService;
        private readonly ILogger<SmartOrderService> _logger;

        public SmartOrderService(IBinanceService binanceService, ILogger<SmartOrderService> logger)
        {
            _binanceService = binanceService;
            _logger = logger;
        }

        /// <summary>
        /// 智能下单 - 包含预检查、杠杆自动调节、分笔处理等功能
        /// </summary>
        public async Task<SmartOrderResult> PlaceSmartOrderAsync(OrderRequest request)
        {
            try
            {
                _logger.LogInformation($"🤖 智能下单开始: {request.Symbol} {request.Side} {request.Quantity} @ {request.Type}");

                // 1. 预检查系统
                var preCheckResult = await PreCheckOrderAsync(request);
                if (!preCheckResult.IsSuccess)
                {
                    return new SmartOrderResult
                    {
                        IsSuccess = false,
                        ErrorMessage = preCheckResult.ErrorMessage,
                        Actions = preCheckResult.Actions
                    };
                }

                // 2. 尝试智能下单
                var orderResult = await TrySmartOrderWithAutoAdjustmentAsync(request);
                return orderResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"智能下单异常: {request.Symbol}");
                return new SmartOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"下单异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 智能分笔止损委托
        /// </summary>
        public async Task<SmartOrderResult> PlaceSmartStopLossAsync(string symbol, decimal totalQuantity, decimal stopPrice, string side, bool reduceOnly = true)
        {
            try
            {
                _logger.LogInformation($"🛡️ 智能分笔止损开始: {symbol} {side} {totalQuantity} @ {stopPrice}");

                // 1. 获取交易规则
                var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(symbol);

                // 2. 检查是否需要分笔
                if (totalQuantity <= maxQty)
                {
                    // 单笔即可
                    var singleRequest = new OrderRequest
                    {
                        Symbol = symbol,
                        Side = side,
                        Type = "STOP_MARKET",
                        Quantity = totalQuantity,
                        StopPrice = stopPrice,
                        ReduceOnly = reduceOnly
                    };

                    var success = await _binanceService.PlaceOrderAsync(singleRequest);
                    return new SmartOrderResult
                    {
                        IsSuccess = success,
                        ErrorMessage = success ? "" : "单笔止损下单失败",
                        Actions = success ? new List<string> { $"✅ 止损单下单成功: {totalQuantity} @ {stopPrice}" } : new List<string>()
                    };
                }

                // 3. 分笔处理
                return await PlaceSplitStopLossOrdersAsync(symbol, totalQuantity, stopPrice, side, maxQty, stepSize, reduceOnly);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"智能分笔止损异常: {symbol}");
                return new SmartOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"分笔止损异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 预检查系统
        /// </summary>
        private async Task<SmartOrderResult> PreCheckOrderAsync(OrderRequest request)
        {
            var actions = new List<string>();

            try
            {
                // 1. 获取当前持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                var existingPosition = positions.FirstOrDefault(p => p.Symbol == request.Symbol);

                // 2. 获取交易规则
                var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(request.Symbol);

                // 3. 检查数量限制
                if (request.Quantity > maxQty)
                {
                    actions.Add($"⚠️ 数量超限: 请求{request.Quantity} > 最大{maxQty}");
                    actions.Add($"💡 建议: 将拆分为多笔下单或使用分笔功能");
                    return new SmartOrderResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"单笔数量超过限制，最大允许: {maxQty}",
                        Actions = actions
                    };
                }

                // 4. 检查持仓限制（针对-2027错误）
                if (existingPosition != null)
                {
                    var leverageCheckResult = await CheckLeverageAndPositionLimitAsync(request, existingPosition);
                    if (!leverageCheckResult.IsSuccess)
                    {
                        return leverageCheckResult;
                    }
                }

                // 5. 检查杠杆合理性
                if (request.Leverage > maxLeverage)
                {
                    actions.Add($"⚠️ 杠杆过高: 请求{request.Leverage}x > 最大{maxLeverage}x");
                    actions.Add($"💡 建议: 自动调整为{maxLeverage}x杠杆");
                    request.Leverage = maxLeverage; // 自动调整
                }

                actions.Add("✅ 预检查通过");
                return new SmartOrderResult
                {
                    IsSuccess = true,
                    ErrorMessage = "",
                    Actions = actions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"预检查异常: {request.Symbol}");
                return new SmartOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"预检查失败: {ex.Message}",
                    Actions = actions
                };
            }
        }

        /// <summary>
        /// 检查杠杆和持仓限制
        /// </summary>
        private async Task<SmartOrderResult> CheckLeverageAndPositionLimitAsync(OrderRequest request, PositionInfo existingPosition)
        {
            var actions = new List<string>();

            try
            {
                // 计算新的总持仓
                var currentPositionAmt = Math.Abs(existingPosition.PositionAmt);
                var newPositionAmt = currentPositionAmt + request.Quantity;

                // 获取当前价格
                var currentPrice = await _binanceService.GetLatestPriceAsync(request.Symbol);

                // 估算不同杠杆下的最大持仓限制
                var currentLeverage = request.Leverage;
                var estimatedMaxPosition = EstimateMaxPositionForLeverage(request.Symbol, currentLeverage, currentPrice);

                actions.Add($"📊 持仓分析: 当前{currentPositionAmt} + 新增{request.Quantity} = 总计{newPositionAmt}");
                actions.Add($"📊 {currentLeverage}x杠杆预估限制: {estimatedMaxPosition}");

                // 如果可能超限，准备杠杆调节方案
                if (newPositionAmt > estimatedMaxPosition * 0.8m) // 80%安全阈值
                {
                    actions.Add($"⚠️ 可能接近持仓限制，准备杠杆调节方案");

                    // 计算建议杠杆
                    var suggestedLeverage = CalculateSuggestedLeverage(request.Symbol, newPositionAmt, currentPrice);
                    if (suggestedLeverage < currentLeverage)
                    {
                        actions.Add($"💡 建议降低杠杆: {currentLeverage}x → {suggestedLeverage}x");
                        actions.Add($"💡 降低杠杆后预估限制将增加到: {EstimateMaxPositionForLeverage(request.Symbol, suggestedLeverage, currentPrice)}");
                        
                        // 标记需要杠杆调节
                        return new SmartOrderResult
                        {
                            IsSuccess = true,
                            ErrorMessage = "",
                            Actions = actions,
                            RequiresLeverageAdjustment = true,
                            SuggestedLeverage = suggestedLeverage
                        };
                    }
                }

                return new SmartOrderResult
                {
                    IsSuccess = true,
                    ErrorMessage = "",
                    Actions = actions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"杠杆检查异常: {request.Symbol}");
                return new SmartOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"杠杆检查失败: {ex.Message}",
                    Actions = actions
                };
            }
        }

        /// <summary>
        /// 智能下单处理（包含自动杠杆调节）
        /// </summary>
        private async Task<SmartOrderResult> TrySmartOrderWithAutoAdjustmentAsync(OrderRequest request)
        {
            var actions = new List<string>();
            var maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation($"🎯 下单尝试 {attempt + 1}/{maxRetries}: {request.Symbol} {request.Side} {request.Quantity}");

                    // 设置杠杆
                    var leverageSuccess = await _binanceService.SetLeverageAsync(request.Symbol, request.Leverage);
                    if (leverageSuccess)
                    {
                        actions.Add($"✅ 杠杆设置成功: {request.Leverage}x");
                    }

                    // 尝试下单
                    var orderSuccess = await _binanceService.PlaceOrderAsync(request);

                    if (orderSuccess)
                    {
                        actions.Add($"✅ 下单成功: {request.Symbol} {request.Side} {request.Quantity}");
                        return new SmartOrderResult
                        {
                            IsSuccess = true,
                            ErrorMessage = "",
                            Actions = actions
                        };
                    }

                    // 下单失败，分析错误并尝试自动调节
                    actions.Add($"❌ 下单失败，尝试自动调节...");

                    var adjustmentResult = await AttemptAutoAdjustmentAsync(request, attempt);
                    actions.AddRange(adjustmentResult.Actions);

                    if (!adjustmentResult.IsSuccess)
                    {
                        break; // 无法调节，退出重试
                    }

                    // 应用调节结果
                    if (adjustmentResult.AdjustedLeverage.HasValue)
                    {
                        request.Leverage = adjustmentResult.AdjustedLeverage.Value;
                        actions.Add($"🔧 应用杠杆调节: {request.Leverage}x");
                    }

                    if (adjustmentResult.AdjustedQuantity.HasValue)
                    {
                        request.Quantity = adjustmentResult.AdjustedQuantity.Value;
                        actions.Add($"🔧 应用数量调节: {request.Quantity}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"下单尝试{attempt + 1}异常: {request.Symbol}");
                    actions.Add($"❌ 尝试{attempt + 1}异常: {ex.Message}");

                    if (attempt == maxRetries - 1)
                    {
                        break;
                    }
                }

                // 等待后重试
                await Task.Delay(1000 * (attempt + 1));
            }

            return new SmartOrderResult
            {
                IsSuccess = false,
                ErrorMessage = $"经过{maxRetries}次智能调节仍无法下单成功",
                Actions = actions
            };
        }

        /// <summary>
        /// 尝试自动调节
        /// </summary>
        private async Task<AutoAdjustmentResult> AttemptAutoAdjustmentAsync(OrderRequest request, int attemptNumber)
        {
            var actions = new List<string>();

            try
            {
                // 获取当前价格用于计算
                var currentPrice = await _binanceService.GetLatestPriceAsync(request.Symbol);

                switch (attemptNumber)
                {
                    case 0:
                        // 第一次重试：降低杠杆
                        var suggestedLeverage = CalculateSuggestedLeverage(request.Symbol, request.Quantity, currentPrice);
                        if (suggestedLeverage < request.Leverage)
                        {
                            actions.Add($"🔧 自动调节方案1: 降低杠杆 {request.Leverage}x → {suggestedLeverage}x");
                            return new AutoAdjustmentResult
                            {
                                IsSuccess = true,
                                AdjustedLeverage = suggestedLeverage,
                                Actions = actions
                            };
                        }
                        break;

                    case 1:
                        // 第二次重试：减少数量
                        var adjustedQuantity = request.Quantity * 0.7m; // 减少30%
                        var (minQty, maxQty, stepSize, _, _) = await _binanceService.GetSymbolTradingRulesAsync(request.Symbol);
                        adjustedQuantity = Math.Max(minQty, Math.Floor(adjustedQuantity / stepSize) * stepSize);

                        if (adjustedQuantity >= minQty)
                        {
                            actions.Add($"🔧 自动调节方案2: 减少数量 {request.Quantity} → {adjustedQuantity}");
                            return new AutoAdjustmentResult
                            {
                                IsSuccess = true,
                                AdjustedQuantity = adjustedQuantity,
                                Actions = actions
                            };
                        }
                        break;

                    case 2:
                        // 第三次重试：同时调节杠杆和数量
                        var conservativeLeverage = Math.Max(1, request.Leverage / 2);
                        var conservativeQuantity = request.Quantity * 0.5m;
                        var (minQty2, _, stepSize2, _, _) = await _binanceService.GetSymbolTradingRulesAsync(request.Symbol);
                        conservativeQuantity = Math.Max(minQty2, Math.Floor(conservativeQuantity / stepSize2) * stepSize2);

                        if (conservativeQuantity >= minQty2)
                        {
                            actions.Add($"🔧 自动调节方案3: 保守调节 杠杆{request.Leverage}x→{conservativeLeverage}x, 数量{request.Quantity}→{conservativeQuantity}");
                            return new AutoAdjustmentResult
                            {
                                IsSuccess = true,
                                AdjustedLeverage = conservativeLeverage,
                                AdjustedQuantity = conservativeQuantity,
                                Actions = actions
                            };
                        }
                        break;
                }

                actions.Add($"❌ 无可用调节方案 (尝试{attemptNumber + 1})");
                return new AutoAdjustmentResult
                {
                    IsSuccess = false,
                    Actions = actions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"自动调节异常: {request.Symbol}");
                actions.Add($"❌ 调节计算异常: {ex.Message}");
                return new AutoAdjustmentResult
                {
                    IsSuccess = false,
                    Actions = actions
                };
            }
        }

        /// <summary>
        /// 分笔止损委托处理
        /// </summary>
        private async Task<SmartOrderResult> PlaceSplitStopLossOrdersAsync(string symbol, decimal totalQuantity, decimal baseStopPrice, string side, decimal maxQty, decimal stepSize, bool reduceOnly)
        {
            var actions = new List<string>();
            var successCount = 0;
            var totalOrders = 0;

            try
            {
                // 计算分笔方案
                var orderChunks = CalculateOrderChunks(totalQuantity, maxQty, stepSize);
                totalOrders = orderChunks.Count;

                actions.Add($"📋 分笔方案: 总量{totalQuantity} 拆分为{totalOrders}笔");

                // 逐笔下单，每笔价格稍有差异
                for (int i = 0; i < orderChunks.Count; i++)
                {
                    var chunk = orderChunks[i];
                    
                    // 计算价格差异（每笔相差0.1%）
                    var priceAdjustment = 1m + (i * 0.001m); // 0.1%的价格差异
                    var adjustedStopPrice = baseStopPrice * priceAdjustment;

                    var chunkRequest = new OrderRequest
                    {
                        Symbol = symbol,
                        Side = side,
                        Type = "STOP_MARKET",
                        Quantity = chunk,
                        StopPrice = adjustedStopPrice,
                        ReduceOnly = reduceOnly
                    };

                    try
                    {
                        var success = await _binanceService.PlaceOrderAsync(chunkRequest);
                        if (success)
                        {
                            successCount++;
                            actions.Add($"✅ 分笔{i + 1}/{totalOrders}: {chunk} @ {adjustedStopPrice:F4}");
                        }
                        else
                        {
                            actions.Add($"❌ 分笔{i + 1}/{totalOrders}失败: {chunk} @ {adjustedStopPrice:F4}");
                        }

                        // 分笔间隔，避免频率过高
                        if (i < orderChunks.Count - 1)
                        {
                            await Task.Delay(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"分笔{i + 1}异常: {symbol}");
                        actions.Add($"❌ 分笔{i + 1}异常: {ex.Message}");
                    }
                }

                var isFullSuccess = successCount == totalOrders;
                var isPartialSuccess = successCount > 0;

                if (isFullSuccess)
                {
                    actions.Add($"🎉 分笔止损完全成功: {successCount}/{totalOrders}笔");
                }
                else if (isPartialSuccess)
                {
                    actions.Add($"⚠️ 分笔止损部分成功: {successCount}/{totalOrders}笔");
                }
                else
                {
                    actions.Add($"❌ 分笔止损全部失败: 0/{totalOrders}笔");
                }

                return new SmartOrderResult
                {
                    IsSuccess = isPartialSuccess,
                    ErrorMessage = isPartialSuccess ? "" : "所有分笔止损委托都失败了",
                    Actions = actions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"分笔止损异常: {symbol}");
                return new SmartOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"分笔止损处理异常: {ex.Message}",
                    Actions = actions
                };
            }
        }

        /// <summary>
        /// 计算分笔方案
        /// </summary>
        private List<decimal> CalculateOrderChunks(decimal totalQuantity, decimal maxQty, decimal stepSize)
        {
            var chunks = new List<decimal>();
            var remaining = totalQuantity;

            while (remaining > 0)
            {
                var chunkSize = Math.Min(remaining, maxQty);
                
                // 调整到正确的步长
                chunkSize = Math.Floor(chunkSize / stepSize) * stepSize;
                
                if (chunkSize > 0)
                {
                    chunks.Add(chunkSize);
                    remaining -= chunkSize;
                }
                else
                {
                    break; // 剩余量太小，无法继续分割
                }
            }

            return chunks;
        }

        /// <summary>
        /// 估算杠杆下的最大持仓限制
        /// </summary>
        private decimal EstimateMaxPositionForLeverage(string symbol, int leverage, decimal currentPrice)
        {
            // 基于历史经验的估算规则
            // 这里使用保守估算，实际限制可能更宽松
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => leverage switch
                {
                    <= 20 => 100m,
                    <= 50 => 50m,
                    <= 125 => 5m,
                    _ => 1m
                },
                "ETHUSDT" => leverage switch
                {
                    <= 25 => 1000m,
                    <= 50 => 500m,
                    <= 100 => 100m,
                    _ => 50m
                },
                _ when currentPrice < 1m => leverage switch
                {
                    <= 3 => 50000m,
                    <= 10 => 25000m,
                    <= 20 => 10000m,
                    <= 50 => 5000m,
                    _ => 1000m
                },
                _ => leverage switch
                {
                    <= 20 => 100000m,
                    <= 50 => 50000m,
                    _ => 10000m
                }
            };
        }

        /// <summary>
        /// 计算建议杠杆
        /// </summary>
        private int CalculateSuggestedLeverage(string symbol, decimal targetQuantity, decimal currentPrice)
        {
            // 逐步降低杠杆，找到合适的级别
            var testLeverages = new[] { 20, 15, 10, 5, 3, 1 };

            foreach (var testLeverage in testLeverages)
            {
                var estimatedLimit = EstimateMaxPositionForLeverage(symbol, testLeverage, currentPrice);
                if (targetQuantity <= estimatedLimit * 0.8m) // 80%安全边际
                {
                    return testLeverage;
                }
            }

            return 1; // 最保守的1倍杠杆
        }
    }

    /// <summary>
    /// 智能下单结果
    /// </summary>
    public class SmartOrderResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<string> Actions { get; set; } = new();
        public bool RequiresLeverageAdjustment { get; set; }
        public int? SuggestedLeverage { get; set; }
    }

    /// <summary>
    /// 自动调节结果
    /// </summary>
    public class AutoAdjustmentResult
    {
        public bool IsSuccess { get; set; }
        public int? AdjustedLeverage { get; set; }
        public decimal? AdjustedQuantity { get; set; }
        public List<string> Actions { get; set; } = new();
    }
} 