using System;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 交易计算服务实现
    /// </summary>
    public class TradingCalculationService : ITradingCalculationService
    {
        private readonly IBinanceService _binanceService;
        private readonly ILogger<TradingCalculationService> _logger;

        public TradingCalculationService(IBinanceService binanceService, ILogger<TradingCalculationService> logger)
        {
            _binanceService = binanceService;
            _logger = logger;
        }

        /// <summary>
        /// 计算止损价格
        /// </summary>
        public decimal CalculateStopLossPrice(decimal currentPrice, decimal stopLossRatio, string side)
        {
            if (currentPrice <= 0 || stopLossRatio <= 0)
                return 0;

            decimal stopLossPrice;
            if (side == "BUY")
            {
                // 买入（做多）：止损价 = 当前价 × (1 - 止损比例)
                stopLossPrice = currentPrice * (1 - stopLossRatio / 100);
            }
            else
            {
                // 卖出（做空）：止损价 = 当前价 × (1 + 止损比例)
                stopLossPrice = currentPrice * (1 + stopLossRatio / 100);
            }

            _logger.LogDebug("计算止损价: 当前价={CurrentPrice}, 止损比例={StopLossRatio}%, 方向={Side}, 止损价={StopLossPrice}",
                currentPrice, stopLossRatio, side, stopLossPrice);

            return stopLossPrice;
        }

        /// <summary>
        /// 根据止损金额计算交易数量
        /// </summary>
        public async Task<decimal> CalculateQuantityFromLossAsync(decimal stopLossAmount, decimal currentPrice, decimal stopLossRatio, string symbol)
        {
            if (stopLossAmount <= 0 || currentPrice <= 0 || stopLossRatio <= 0)
            {
                _logger.LogWarning("以损定量计算参数无效: 止损金额={StopLossAmount}, 当前价={CurrentPrice}, 止损比例={StopLossRatio}",
                    stopLossAmount, currentPrice, stopLossRatio);
                return 0;
            }

            try
            {
                // 正确公式：数量 = 止损金额 / (止损比例 × 最新价格)
                var quantity = stopLossAmount / (stopLossRatio / 100 * currentPrice);

                _logger.LogInformation("以损定量计算: 止损金额={StopLossAmount}U, 当前价={CurrentPrice}, 止损比例={StopLossRatio}%, 计算数量={Quantity}",
                    stopLossAmount, currentPrice, stopLossRatio, quantity);

                // 获取交易规则并调整精度
                var (minQuantity, maxQuantity, _, _, _) = await GetSymbolLimitsAsync(symbol);
                quantity = await AdjustQuantityPrecisionAsync(quantity, symbol, minQuantity, maxQuantity);

                return quantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "以损定量计算失败");
                return 0;
            }
        }

        /// <summary>
        /// 计算最大风险资金
        /// </summary>
        public decimal CalculateMaxRiskCapital(decimal availableBalance, decimal riskPercentage = 0.1m)
        {
            var maxRisk = availableBalance * riskPercentage;
            var result = Math.Ceiling(maxRisk); // 向上取整

            _logger.LogDebug("最大风险资金计算: 可用余额={AvailableBalance}, 风险比例={RiskPercentage}%, 最大风险={MaxRisk}",
                availableBalance, riskPercentage * 100, result);

            return result;
        }

        /// <summary>
        /// 计算浮盈条件单触发价格
        /// </summary>
        public decimal CalculateProfitConditionalPrice(PositionInfo position, decimal targetProfit)
        {
            if (position.PositionAmt == 0 || targetProfit <= 0)
                return 0;

            decimal triggerPrice;
            if (position.PositionAmt > 0) // 多头持仓
            {
                // 多头：触发价 = 开仓价 + (目标浮盈 / 持仓数量)
                triggerPrice = position.EntryPrice + (targetProfit / Math.Abs(position.PositionAmt));
            }
            else // 空头持仓
            {
                // 空头：触发价 = 开仓价 + (目标浮盈 / 持仓数量)
                triggerPrice = position.EntryPrice + (targetProfit / Math.Abs(position.PositionAmt));
            }

            _logger.LogDebug("浮盈条件单价格计算: 持仓={Position}, 目标浮盈={TargetProfit}, 触发价={TriggerPrice}",
                position.PositionAmt, targetProfit, triggerPrice);

            return triggerPrice;
        }

        /// <summary>
        /// 验证订单参数
        /// </summary>
        public async Task<(bool isValid, string errorMessage)> ValidateOrderParametersAsync(OrderRequest request)
        {
            try
            {
                // 基础参数验证
                if (string.IsNullOrEmpty(request.Symbol))
                    return (false, "合约名称不能为空");

                if (request.Quantity <= 0)
                    return (false, "交易数量必须大于0");

                // 获取交易规则进行验证
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice) = await GetSymbolLimitsAsync(request.Symbol);

                if (request.Quantity < minQuantity)
                    return (false, $"交易数量不能小于{minQuantity}");

                if (request.Quantity > maxQuantity)
                    return (false, $"交易数量不能大于{maxQuantity}");

                // 验证名义价值
                var notionalValue = request.Quantity * estimatedPrice;
                if (notionalValue > maxNotional)
                    return (false, $"名义价值{notionalValue:F2}超过限制{maxNotional:F2}");

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订单参数验证失败");
                return (false, $"验证失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调整价格精度
        /// </summary>
        public async Task<decimal> AdjustPricePrecisionAsync(decimal price, string symbol)
        {
            try
            {
                var precisionData = await _binanceService.GetSymbolPrecisionAsync(symbol);
                return RoundToTickSize(price, precisionData.tickSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "价格精度调整失败，使用默认精度");
                return GetFallbackPricePrecision(price, symbol);
            }
        }

        /// <summary>
        /// 调整数量精度
        /// </summary>
        public async Task<decimal> AdjustQuantityPrecisionAsync(decimal quantity, string symbol, decimal minQuantity, decimal maxQuantity)
        {
            try
            {
                var precisionData = await _binanceService.GetSymbolPrecisionAsync(symbol);
                var adjustedQuantity = RoundToStepSize(quantity, precisionData.stepSize);

                // 确保在允许范围内
                adjustedQuantity = Math.Max(adjustedQuantity, minQuantity);
                adjustedQuantity = Math.Min(adjustedQuantity, maxQuantity);

                return adjustedQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数量精度调整失败，使用传统方法");
                return AdjustQuantityPrecisionTraditional(quantity, symbol);
            }
        }

        #region 私有辅助方法

        private async Task<(decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice)> GetSymbolLimitsAsync(string symbol)
        {
            try
            {
                // 从API获取真实的交易规则
                var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(symbol);
                
                // 获取当前价格用于计算最大名义价值
                var currentPrice = await _binanceService.GetLatestPriceAsync(symbol);
                
                // 计算合理的最大名义价值（基于杠杆和风险控制）
                var maxNotional = Math.Min(1000000m, maxQty * currentPrice); // 限制最大名义价值为100万或实际最大数量价值
                
                _logger.LogInformation("获取到真实交易规则: {Symbol} - minQty: {MinQty}, maxQty: {MaxQty}, maxLeverage: {MaxLeverage}, 当前价格: {CurrentPrice}",
                    symbol, minQty, maxQty, maxLeverage, currentPrice);
                
                return (minQty, maxQty, maxLeverage, maxNotional, currentPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取交易规则失败，使用备用方案");
                
                // 备用方案：根据当前价格动态设置
                try
                {
                    var currentPrice = await _binanceService.GetLatestPriceAsync(symbol);
                    return GetDynamicLimits(currentPrice);
                }
                catch
                {
                    // 最终备用方案
                    return (0.001m, 1000000m, 125, 1000000m, 50000m);
                }
            }
        }

        private (decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice) GetDynamicLimits(decimal currentPrice)
        {
            // 基于价格的动态限制，调整maxQuantity为更合理的值
            return currentPrice switch
            {
                >= 1000 => (0.001m, 1000000m, 125, 1000000m, currentPrice),    // 高价币种如BTC
                >= 100 => (0.001m, 10000000m, 100, 1000000m, currentPrice),   // 中高价币种如ETH
                >= 10 => (0.01m, 100000000m, 75, 1000000m, currentPrice),     // 中价币种
                >= 1 => (0.1m, 1000000000m, 75, 1000000m, currentPrice),      // 中低价币种
                >= 0.1m => (1m, 10000000000m, 75, 1000000m, currentPrice),    // 低价币种
                >= 0.01m => (10m, 100000000000m, 50, 1000000m, currentPrice), // 超低价币种
                >= 0.001m => (100m, 1000000000000m, 25, 1000000m, currentPrice), // 极低价币种
                _ => (1000m, 10000000000000m, 25, 1000000m, currentPrice)      // 微价币种
            };
        }

        private decimal RoundToTickSize(decimal price, decimal tickSize)
        {
            if (tickSize <= 0) return Math.Round(price, 4);

            var steps = Math.Floor(price / tickSize);
            var adjustedPrice = steps * tickSize;
            int decimalPlaces = GetDecimalPlaces(tickSize);
            return Math.Round(adjustedPrice, decimalPlaces);
        }

        private decimal RoundToStepSize(decimal value, decimal stepSize)
        {
            if (stepSize <= 0) return value;
            return Math.Floor(value / stepSize) * stepSize;
        }

        private int GetDecimalPlaces(decimal value)
        {
            var valueStr = value.ToString();
            var decimalIndex = valueStr.IndexOf('.');
            if (decimalIndex == -1) return 0;

            var trimmed = valueStr.TrimEnd('0');
            if (trimmed.EndsWith(".")) return 0;

            return trimmed.Length - decimalIndex - 1;
        }

        private decimal GetFallbackPricePrecision(decimal price, string symbol)
        {
            // 硬编码的备用精度
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(price, 1),
                "ETHUSDT" => Math.Round(price, 2),
                _ => Math.Round(price, 4)
            };
        }

        private decimal AdjustQuantityPrecisionTraditional(decimal quantity, string symbol)
        {
            // 传统的精度调整方法
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(quantity, 3),
                "ETHUSDT" => Math.Round(quantity, 3),
                "ADAUSDT" => Math.Round(quantity, 0),
                "DOGEUSDT" => Math.Round(quantity, 0),
                _ => Math.Round(quantity, 3)
            };
        }

        #endregion
    }
} 