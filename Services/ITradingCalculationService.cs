using System.Threading.Tasks;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 交易计算服务接口
    /// </summary>
    public interface ITradingCalculationService
    {
        /// <summary>
        /// 计算止损价格
        /// </summary>
        decimal CalculateStopLossPrice(decimal currentPrice, decimal stopLossRatio, string side);

        /// <summary>
        /// 根据止损金额计算交易数量
        /// </summary>
        Task<decimal> CalculateQuantityFromLossAsync(decimal stopLossAmount, decimal currentPrice, decimal stopLossRatio, string symbol);

        /// <summary>
        /// 计算最大风险资金
        /// </summary>
        decimal CalculateMaxRiskCapital(decimal availableBalance, decimal riskPercentage = 0.1m);

        /// <summary>
        /// 计算浮盈条件单触发价格
        /// </summary>
        decimal CalculateProfitConditionalPrice(PositionInfo position, decimal targetProfit);

        /// <summary>
        /// 验证订单参数
        /// </summary>
        Task<(bool isValid, string errorMessage)> ValidateOrderParametersAsync(OrderRequest request);

        /// <summary>
        /// 调整价格精度
        /// </summary>
        Task<decimal> AdjustPricePrecisionAsync(decimal price, string symbol);

        /// <summary>
        /// 调整数量精度
        /// </summary>
        Task<decimal> AdjustQuantityPrecisionAsync(decimal quantity, string symbol, decimal minQuantity, decimal maxQuantity);
    }
} 