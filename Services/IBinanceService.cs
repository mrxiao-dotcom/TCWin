using System.Collections.Generic;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 币安API服务接口
    /// </summary>
    public interface IBinanceService
    {
        /// <summary>
        /// 设置账户配置
        /// </summary>
        void SetAccount(AccountConfig account);

        /// <summary>
        /// 获取账户信息
        /// </summary>
        Task<AccountInfo?> GetAccountInfoAsync();

        /// <summary>
        /// 获取持仓信息
        /// </summary>
        Task<List<PositionInfo>> GetPositionsAsync();

        /// <summary>
        /// 获取开放订单
        /// </summary>
        Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null);

        /// <summary>
        /// 获取最新价格
        /// </summary>
        Task<decimal> GetLatestPriceAsync(string symbol);

        /// <summary>
        /// 取消订单
        /// </summary>
        Task<bool> CancelOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 下单
        /// </summary>
        Task<bool> PlaceOrderAsync(OrderRequest request);

        /// <summary>
        /// 设置杠杆
        /// </summary>
        Task<bool> SetLeverageAsync(string symbol, int leverage);

        /// <summary>
        /// 设置保证金模式
        /// </summary>
        Task<bool> SetMarginTypeAsync(string symbol, string marginType);

        /// <summary>
        /// 平仓
        /// </summary>
        Task<bool> ClosePositionAsync(string symbol, string positionSide);

        /// <summary>
        /// 平掉所有持仓
        /// </summary>
        Task<bool> CloseAllPositionsAsync();

        /// <summary>
        /// 取消所有订单
        /// </summary>
        Task<bool> CancelAllOrdersAsync(string? symbol = null);

        /// <summary>
        /// 获取交易所信息
        /// </summary>
        Task<string?> GetRealExchangeInfoAsync(string? symbol = null);

        /// <summary>
        /// 获取所有订单历史
        /// </summary>
        Task<List<OrderInfo>> GetAllOrdersAsync(string symbol, int limit = 500);

        /// <summary>
        /// 验证订单
        /// </summary>
        Task<(bool isValid, string errorMessage)> ValidateOrderAsync(OrderRequest request);

        /// <summary>
        /// 获取合约精度信息
        /// </summary>
        Task<(decimal stepSize, decimal tickSize)> GetSymbolPrecisionAsync(string symbol);

        /// <summary>
        /// 获取完整的交易规则信息
        /// </summary>
        Task<(decimal minQty, decimal maxQty, decimal stepSize, decimal tickSize, int maxLeverage)> GetSymbolTradingRulesAsync(string symbol);

        /// <summary>
        /// 获取持仓模式
        /// </summary>
        Task<bool> GetPositionModeAsync();

        /// <summary>
        /// 设置持仓模式
        /// </summary>
        Task<bool> SetPositionModeAsync(bool dualSidePosition);
    }
} 