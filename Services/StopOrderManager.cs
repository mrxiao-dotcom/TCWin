using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 止损单管理器 - 统一管理所有止损委托单，确保唯一性和一致性
    /// </summary>
    public class StopOrderManager : IDisposable
    {
        private readonly IBinanceService _binanceService;
        private readonly ILogger _logger;
        private readonly SmartOrderService _smartOrderService;
        
        // 🔒 止损单操作信号量，确保同一时间只能有一个止损单操作
        private readonly SemaphoreSlim _stopOrderSemaphore = new(1, 1);
        
        // 📊 活跃止损单缓存：合约 -> 止损单列表
        private readonly Dictionary<string, List<StopOrderInfo>> _activeStopOrders = new();
        
        // 🔒 缓存数据锁
        private readonly object _cacheLock = new();
        
        // ⏰ 定时器：定期刷新止损单缓存
        private Timer? _refreshTimer;
        
        // 📈 统计信息
        public StopOrderStats Statistics { get; private set; } = new();

        public StopOrderManager(IBinanceService binanceService, ILogger logger)
        {
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 🚀 初始化智能下单服务，提高止损单成功率
            var smartOrderLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<SmartOrderService>();
            _smartOrderService = new SmartOrderService(_binanceService, smartOrderLogger);
            
            // 启动定时刷新（每30秒）
            _refreshTimer = new Timer(async _ => await RefreshStopOrderCacheAsync(), 
                null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            _logger.LogInformation("🛡️ 止损单管理器已启动（含智能下单功能）");
        }

        /// <summary>
        /// 安全地创建止损单 - 确保每个合约只有一个有效止损单
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="orderRequest">止损单请求</param>
        /// <param name="stopOrderType">止损单类型</param>
        /// <returns>是否创建成功</returns>
        public async Task<bool> CreateStopOrderSafelyAsync(string symbol, OrderRequest orderRequest, StopOrderType stopOrderType)
        {
            if (string.IsNullOrEmpty(symbol) || orderRequest == null)
            {
                _logger.LogWarning("⚠️ 创建止损单失败：参数无效");
                return false;
            }

            await _stopOrderSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation($"🛡️ 开始安全创建止损单: {symbol}, 类型: {stopOrderType}");
                
                // 1. 清理该合约的所有历史止损单
                var cleanupSuccess = await CleanupContractStopOrdersAsync(symbol);
                if (!cleanupSuccess)
                {
                    _logger.LogWarning($"⚠️ 清理历史止损单失败，但继续创建新止损单: {symbol}");
                }
                
                // 2. 等待清理完成
                await Task.Delay(500);
                
                // 3. 刷新缓存，确保数据准确
                await RefreshStopOrderCacheAsync();
                
                // 4. 最终检查：确保该合约没有活跃止损单
                lock (_cacheLock)
                {
                    if (_activeStopOrders.TryGetValue(symbol, out var existingOrders) && existingOrders.Any())
                    {
                        _logger.LogWarning($"⚠️ 检测到合约 {symbol} 仍有 {existingOrders.Count} 个活跃止损单，取消创建");
                        return false;
                    }
                }
                
                // 5. 使用智能下单服务创建止损单
                _logger.LogInformation($"🚀 使用智能下单服务创建止损单: {symbol}");
                
                // 根据止损单类型选择合适的下单方式
                SmartOrderResult smartResult;
                if (orderRequest.Type?.Contains("STOP") == true && orderRequest.Quantity > 0)
                {
                    // 使用智能分笔止损功能
                    smartResult = await _smartOrderService.PlaceSmartStopLossAsync(
                        symbol, 
                        orderRequest.Quantity, 
                        orderRequest.StopPrice, 
                        orderRequest.Side, 
                        orderRequest.ReduceOnly);
                }
                else
                {
                    // 使用普通智能下单
                    smartResult = await _smartOrderService.PlaceSmartOrderAsync(orderRequest);
                }
                
                var success = smartResult.IsSuccess;
                
                // 记录智能下单的详细信息
                if (smartResult.IsSuccess)
                {
                    _logger.LogInformation($"✅ 智能止损单创建成功: {symbol} @{orderRequest.StopPrice:F4}, 类型: {stopOrderType}");
                    foreach (var action in smartResult.Actions)
                    {
                        _logger.LogInformation($"   {action}");
                    }
                }
                else
                {
                    _logger.LogError($"❌ 智能止损单创建失败: {symbol} - {smartResult.ErrorMessage}");
                    foreach (var action in smartResult.Actions)
                    {
                        _logger.LogWarning($"   {action}");
                    }
                }
                
                if (success)
                {
                    // 6. 记录到缓存
                    var stopOrderInfo = new StopOrderInfo
                    {
                        Symbol = symbol,
                        OrderType = stopOrderType,
                        Side = orderRequest.Side,
                        Quantity = orderRequest.Quantity,
                        StopPrice = orderRequest.StopPrice,
                        CreateTime = DateTime.Now,
                        Status = "NEW"
                    };
                    
                    lock (_cacheLock)
                    {
                        if (!_activeStopOrders.ContainsKey(symbol))
                            _activeStopOrders[symbol] = new List<StopOrderInfo>();
                        
                        _activeStopOrders[symbol].Add(stopOrderInfo);
                    }
                    
                    // 7. 更新统计信息
                    Statistics.TotalCreated++;
                    Statistics.LastCreateTime = DateTime.Now;
                }
                else
                {
                    Statistics.CreateFailures++;
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Statistics.CreateFailures++;
                _logger.LogError(ex, $"❌ 创建止损单时发生异常: {symbol}");
                return false;
            }
            finally
            {
                _stopOrderSemaphore.Release();
            }
        }

        /// <summary>
        /// 清理指定合约的所有止损单
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <returns>是否清理成功</returns>
        public async Task<bool> CleanupContractStopOrdersAsync(string symbol)
        {
            try
            {
                _logger.LogInformation($"🧹 开始清理合约 {symbol} 的所有止损单");
                
                // 1. 从API获取最新的止损单列表
                var allOrders = await _binanceService.GetOpenOrdersAsync(symbol);
                if (allOrders == null)
                {
                    _logger.LogWarning($"⚠️ 无法获取合约 {symbol} 的订单列表");
                    return false;
                }
                
                // 2. 过滤出止损单
                var stopOrders = allOrders.Where(order => 
                    order.Type?.Contains("STOP") == true && 
                    order.Status == "NEW").ToList();
                
                _logger.LogInformation($"📊 发现 {stopOrders.Count} 个活跃止损单需要清理");
                
                if (!stopOrders.Any())
                {
                    _logger.LogInformation($"✅ 合约 {symbol} 无需清理止损单");
                    return true;
                }
                
                // 3. 逐个取消止损单
                int successCount = 0;
                foreach (var order in stopOrders)
                {
                    try
                    {
                        var cancelled = await _binanceService.CancelOrderAsync(symbol, order.OrderId);
                        if (cancelled)
                        {
                            successCount++;
                            _logger.LogInformation($"🗑️ 已取消止损单: {symbol} #{order.OrderId}");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ 取消止损单失败: {symbol} #{order.OrderId}");
                        }
                        
                        // 避免API限制
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ 取消止损单异常: {symbol} #{order.OrderId}");
                    }
                }
                
                // 4. 更新统计信息
                Statistics.TotalCancelled += successCount;
                
                // 5. 清理缓存
                lock (_cacheLock)
                {
                    if (_activeStopOrders.ContainsKey(symbol))
                    {
                        _activeStopOrders[symbol].Clear();
                    }
                }
                
                var isFullSuccess = successCount == stopOrders.Count;
                _logger.LogInformation($"✅ 止损单清理完成: {symbol}, 成功: {successCount}/{stopOrders.Count}");
                
                return isFullSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 清理止损单时发生异常: {symbol}");
                return false;
            }
        }

        /// <summary>
        /// 检查合约是否有活跃止损单
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <returns>活跃止损单数量</returns>
        public int GetActiveStopOrderCount(string symbol)
        {
            lock (_cacheLock)
            {
                return _activeStopOrders.TryGetValue(symbol, out var orders) ? orders.Count : 0;
            }
        }

        /// <summary>
        /// 获取所有活跃止损单的概览
        /// </summary>
        /// <returns>止损单概览列表</returns>
        public List<StopOrderOverview> GetAllActiveStopOrders()
        {
            lock (_cacheLock)
            {
                var overview = new List<StopOrderOverview>();
                
                foreach (var kvp in _activeStopOrders)
                {
                    var symbol = kvp.Key;
                    var orders = kvp.Value;
                    
                    if (orders.Any())
                    {
                        overview.Add(new StopOrderOverview
                        {
                            Symbol = symbol,
                            Count = orders.Count,
                            OrderTypes = string.Join(", ", orders.Select(o => o.OrderType).Distinct()),
                            LatestCreateTime = orders.Max(o => o.CreateTime)
                        });
                    }
                }
                
                return overview.OrderBy(o => o.Symbol).ToList();
            }
        }

        /// <summary>
        /// 刷新止损单缓存
        /// </summary>
        private async Task RefreshStopOrderCacheAsync()
        {
            try
            {
                _logger.LogDebug("🔄 开始刷新止损单缓存");
                
                // 获取所有持仓的合约
                var positions = await _binanceService.GetPositionsAsync();
                if (positions == null) return;
                
                var activeSymbols = positions.Where(p => Math.Abs(p.PositionAmt) > 0)
                    .Select(p => p.Symbol).Distinct().ToList();
                
                var tempCache = new Dictionary<string, List<StopOrderInfo>>();
                
                // 为每个活跃合约刷新止损单信息
                foreach (var symbol in activeSymbols)
                {
                    try
                    {
                        var orders = await _binanceService.GetOpenOrdersAsync(symbol);
                        if (orders != null)
                        {
                            var stopOrders = orders.Where(o => o.Type?.Contains("STOP") == true && o.Status == "NEW")
                                .Select(o => new StopOrderInfo
                                {
                                    Symbol = symbol,
                                    OrderId = o.OrderId,
                                    Side = o.Side,
                                    Quantity = o.OrigQty,
                                    StopPrice = o.StopPrice,
                                    Status = o.Status,
                                    CreateTime = o.Time
                                }).ToList();
                            
                            if (stopOrders.Any())
                            {
                                tempCache[symbol] = stopOrders;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"⚠️ 刷新合约 {symbol} 止损单缓存失败");
                    }
                }
                
                // 更新缓存
                lock (_cacheLock)
                {
                    _activeStopOrders.Clear();
                    foreach (var kvp in tempCache)
                    {
                        _activeStopOrders[kvp.Key] = kvp.Value;
                    }
                }
                
                Statistics.LastRefreshTime = DateTime.Now;
                _logger.LogDebug($"✅ 止损单缓存刷新完成，活跃合约: {tempCache.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新止损单缓存时发生异常");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _stopOrderSemaphore?.Dispose();
            _logger.LogInformation("🛡️ 止损单管理器已释放资源");
        }
    }

    /// <summary>
    /// 止损单类型枚举
    /// </summary>
    public enum StopOrderType
    {
        BreakEven,           // 保本止损
        AddPosition,         // 推仓止损
        ProfitProtection,    // 保盈止损
        Manual,              // 手动止损
        TrailingStop         // 移动止损
    }

    /// <summary>
    /// 止损单信息
    /// </summary>
    public class StopOrderInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public long OrderId { get; set; }
        public StopOrderType OrderType { get; set; }
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal StopPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
    }

    /// <summary>
    /// 止损单概览
    /// </summary>
    public class StopOrderOverview
    {
        public string Symbol { get; set; } = string.Empty;
        public int Count { get; set; }
        public string OrderTypes { get; set; } = string.Empty;
        public DateTime LatestCreateTime { get; set; }
    }

    /// <summary>
    /// 止损单管理统计信息
    /// </summary>
    public class StopOrderStats
    {
        public int TotalCreated { get; set; }
        public int TotalCancelled { get; set; }
        public int CreateFailures { get; set; }
        public DateTime? LastCreateTime { get; set; }
        public DateTime? LastRefreshTime { get; set; }
        
        public double SuccessRate => TotalCreated > 0 ? 
            (double)(TotalCreated - CreateFailures) / TotalCreated * 100 : 0;
    }
} 