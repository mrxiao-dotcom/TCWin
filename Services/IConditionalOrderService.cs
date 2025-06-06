using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 条件单管理服务接口
    /// </summary>
    public interface IConditionalOrderService
    {
        /// <summary>
        /// 获取条件单列表
        /// </summary>
        ObservableCollection<ConditionalOrderInfo> ConditionalOrders { get; }

        /// <summary>
        /// 下标准条件单
        /// </summary>
        Task<bool> PlaceStandardConditionalOrderAsync(string symbol, decimal triggerPrice, decimal quantity, string side, string workingType);

        /// <summary>
        /// 下浮盈条件单
        /// </summary>
        Task<bool> PlaceProfitConditionalOrderAsync(PositionInfo position, decimal targetProfit, decimal triggerPrice, decimal quantity);

        /// <summary>
        /// 取消条件单
        /// </summary>
        Task<bool> CancelConditionalOrderAsync(ConditionalOrderInfo order);

        /// <summary>
        /// 取消所有条件单
        /// </summary>
        Task<bool> CancelAllConditionalOrdersAsync();

        /// <summary>
        /// 监控条件单触发
        /// </summary>
        Task MonitorConditionalOrdersAsync();

        /// <summary>
        /// 验证条件单参数
        /// </summary>
        (bool isValid, string errorMessage) ValidateConditionalOrderParameters(string symbol, decimal triggerPrice, decimal quantity, string side);
    }
} 