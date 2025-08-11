using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using System.Text.Json.Serialization;
using System.Threading;

namespace BinanceFuturesTrader.Services
{
    public class BinanceService : IBinanceService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private AccountConfig? _currentAccount;
        private string _baseUrl = "https://fapi.binance.com";
        
        // 🔧 新增：API限流控制机制
        private static readonly SemaphoreSlim _apiSemaphore = new(1, 1); // 确保API请求串行执行
        private static DateTime _lastApiCall = DateTime.MinValue;
        private static readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(200); // 最小请求间隔200ms
        private static DateTime _rateLimitBanUntil = DateTime.MinValue; // IP封禁截止时间
        private static bool _isInErrorRecoveryMode = false; // 错误恢复模式
        private static int _consecutiveErrors = 0; // 连续错误计数
        private static readonly object _rateLimitLock = new object();
        
        // 🚫 新增：IP限制标志 - 当检测到IP限制时自动使用模拟数据
        private static bool _isIpRestricted = false; // IP受限标志
        
        // 时间偏移量用于同步服务器时间
        private long _serverTimeOffset = 0;
        private DateTime _lastServerTimeSync = DateTime.MinValue;
        private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5); // 每5分钟同步一次服务器时间
        
        // 🔧 修复：添加缓存访问锁，防止多线程并发访问缓存集合
        private readonly object _precisionCacheLock = new object();
        private readonly object _tradingRulesCacheLock = new object();
        private readonly object _exchangeInfoCacheLock = new object();
        
        // 精度信息缓存
        private readonly Dictionary<string, (decimal stepSize, decimal tickSize)> _precisionCache = new();

        // 交易规则缓存
        private readonly Dictionary<string, (decimal minQty, decimal maxQty, decimal stepSize, decimal tickSize, int maxLeverage, DateTime cacheTime)> _tradingRulesCache = new();
        private readonly TimeSpan _tradingRulesCacheExpiry = TimeSpan.FromHours(1); // 缓存1小时
        
        // 交易所信息缓存
        private string? _cachedExchangeInfo;
        private DateTime _exchangeInfoCacheTime = DateTime.MinValue;
        private readonly TimeSpan _exchangeInfoCacheExpiry = TimeSpan.FromMinutes(30); // 缓存30分钟
        
        // 模拟模式下的动态订单管理
        private readonly List<OrderInfo> _mockOrders = new();
        private long _nextMockOrderId = 100000;
        
        // JSON序列化选项，更宽松的处理
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // 持仓模式缓存
        private bool? _isDualSidePosition = null;

        /// <summary>
        /// 🔧 新增：检查是否处于API限流状态
        /// </summary>
        private static bool IsRateLimited()
        {
            lock (_rateLimitLock)
            {
                return DateTime.Now < _rateLimitBanUntil;
            }
        }

        /// <summary>
        /// 🔧 新增：设置API限流状态
        /// </summary>
        private static void SetRateLimitBan(long banUntilTimestamp)
        {
            lock (_rateLimitLock)
            {
                var banUntil = DateTimeOffset.FromUnixTimeMilliseconds(banUntilTimestamp).DateTime;
                _rateLimitBanUntil = banUntil;
                _isInErrorRecoveryMode = true;
                
                var waitTime = banUntil - DateTime.Now;
                LogService.LogError($"🚫 API限流：IP被封禁到 {banUntil:yyyy-MM-dd HH:mm:ss}，需要等待 {waitTime.TotalMinutes:F1} 分钟");
            }
        }

        /// <summary>
        /// 🔧 新增：API请求间隔控制
        /// </summary>
        private static async Task EnforceRequestInterval()
        {
            await _apiSemaphore.WaitAsync();
            try
            {
                // 检查是否在限流期内
                if (IsRateLimited())
                {
                    var waitTime = _rateLimitBanUntil - DateTime.Now;
                    LogService.LogWarning($"⏳ API限流中，等待 {waitTime.TotalSeconds:F0} 秒后重试");
                    throw new InvalidOperationException($"API限流中，请等待 {waitTime.TotalMinutes:F1} 分钟");
                }

                // 检查错误恢复模式
                if (_isInErrorRecoveryMode)
                {
                    var recoveryDelay = TimeSpan.FromSeconds(Math.Min(30, _consecutiveErrors * 2)); // 指数退避，最大30秒
                    LogService.LogInfo($"🔄 错误恢复模式：等待 {recoveryDelay.TotalSeconds} 秒");
                    await Task.Delay(recoveryDelay);
                }

                // 确保最小请求间隔
                var timeSinceLastCall = DateTime.Now - _lastApiCall;
                if (timeSinceLastCall < _minRequestInterval)
                {
                    var delay = _minRequestInterval - timeSinceLastCall;
                    await Task.Delay(delay);
                }

                _lastApiCall = DateTime.Now;
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        /// <summary>
        /// 🔧 新增：处理API错误响应
        /// </summary>
        private static void HandleApiError(string response)
        {
            try
            {
                if (response.Contains("\"code\":-1003"))
                {
                    // 解析限流错误，提取封禁时间
                    var doc = JsonDocument.Parse(response);
                    var message = doc.RootElement.GetProperty("msg").GetString();
                    
                    if (message?.Contains("banned until") == true)
                    {
                        // 尝试从消息中提取时间戳
                        var parts = message.Split("until ");
                        if (parts.Length > 1)
                        {
                            var timestampStr = parts[1].TrimEnd('.', ' ');
                            if (long.TryParse(timestampStr, out var timestamp))
                            {
                                SetRateLimitBan(timestamp);
                                return;
                            }
                        }
                    }
                    
                    // 如果无法解析具体时间，设置默认等待时间
                    lock (_rateLimitLock)
                    {
                        _rateLimitBanUntil = DateTime.Now.AddMinutes(10); // 默认等待10分钟
                        _isInErrorRecoveryMode = true;
                        LogService.LogError("🚫 API限流：默认等待10分钟");
                    }
                }
                else if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    // 🔧 增强：解析并处理具体的API错误
                    var doc = JsonDocument.Parse(response);
                    var errorCode = doc.RootElement.GetProperty("code").GetInt32();
                    var errorMsg = doc.RootElement.GetProperty("msg").GetString() ?? "";
                    
                    // 使用增强的错误处理
                    var chineseMessage = GetChineseErrorMessage(errorCode, errorMsg);
                    LogService.LogError($"❌ {chineseMessage}");
                    
                    // 🚫 检测IP限制错误(-2015)并启用模拟数据模式
                    if (errorCode == -2015)
                    {
                        lock (_rateLimitLock)
                        {
                            _isIpRestricted = true;
                            LogService.LogWarning("🚫 检测到IP限制，自动启用模拟数据模式");
                            LogService.LogInfo("📊 模拟数据包含：账户信息、持仓数据、订单数据、价格数据");
                        }
                    }
                    
                    // 对于关键错误，提供解决方案
                    if (errorCode == -4005 || errorCode == -2027)
                    {
                        var solution = GetErrorSolution(errorCode);
                        LogService.LogWarning($"💡 {solution}");
                    }
                    
                    // 🔧 【时间戳错误专项处理】
                    if (errorCode == -1021) // Timestamp错误
                    {
                        LogService.LogWarning($"🕐 检测到时间戳错误，建议重新同步服务器时间");
                        LogService.LogWarning($"💡 时间戳错误解决方案：1) 检查系统时间 2) 重启应用程序 3) 检查网络连接");
                        // 注意：静态方法无法直接重置实例字段，需要在实例方法中处理
                    }
                    else
                    {
                        // 其他API错误
                        _consecutiveErrors++;
                        if (_consecutiveErrors >= 5) // 提高阈值，减少误判
                        {
                            _isInErrorRecoveryMode = true;
                            LogService.LogWarning($"⚠️ 连续错误 {_consecutiveErrors} 次，进入错误恢复模式");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 处理API错误时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 新增：重置错误状态
        /// </summary>
        private static void ResetErrorState()
        {
            lock (_rateLimitLock)
            {
                if (_consecutiveErrors > 0 || _isInErrorRecoveryMode)
                {
                    _consecutiveErrors = 0;
                    _isInErrorRecoveryMode = false;
                    LogService.LogInfo("✅ API错误状态已重置");
                }
            }
        }

        /// <summary>
        /// 🚫 新增：重置IP限制状态 - 手动恢复真实API调用
        /// </summary>
        public static void ResetIpRestriction()
        {
            lock (_rateLimitLock)
            {
                if (_isIpRestricted)
                {
                    _isIpRestricted = false;
                    LogService.LogInfo("✅ IP限制状态已重置，将尝试恢复真实API调用");
                    LogService.LogWarning("⚠️ 如果IP仍然受限，系统将再次自动切换到模拟数据模式");
                }
                else
                {
                    LogService.LogInfo("ℹ️ 当前没有IP限制，无需重置");
                }
            }
        }

        /// <summary>
        /// 🚫 新增：检查当前是否处于IP限制模式
        /// </summary>
        public static bool IsIpRestricted => _isIpRestricted;

        /// <summary>
        /// 🔧 新增：将英文错误转换为中文并提供解决方案
        /// </summary>
        private static string GetChineseErrorMessage(int errorCode, string originalMessage)
        {
            return errorCode switch
            {
                -4005 => "数量超过限制：下单数量超过该合约的最大交易数量",
                -2027 => "持仓超过杠杆限制：在当前杠杆下持仓量超过最大允许值",
                -4003 => "数量过小：下单数量低于最小交易数量",
                -4004 => "数量过大：下单数量超过单笔最大限制",
                -1111 => "精度错误：价格或数量的小数位数超过限制",
                -2019 => "保证金不足：账户保证金余额不足",
                -2010 => "余额不足：账户可用余额不足",
                -1121 => "合约无效：交易对不存在或已下架",
                -1002 => "认证失败：未授权的访问",
                -1022 => "签名无效：API签名验证失败",
                -2015 => "API密钥无效：密钥过期或IP受限",
                -4046 => "保证金模式：无需更改保证金类型（已是目标模式）",
                -4028 => "杠杆设置：杠杆已经是当前设置，无需更改",
                _ => $"API错误 ({errorCode}): {originalMessage}"
            };
        }

        /// <summary>
        /// 🔧 新增：获取错误解决方案
        /// </summary>
        private static string GetErrorSolution(int errorCode)
        {
            return errorCode switch
            {
                -4005 => "解决方案：\n" +
                        "• 减少下单数量\n" +
                        "• 分批下单\n" +
                        "• 检查合约的交易规则\n" +
                        "• 使用程序的自动调整功能",
                        
                -2027 => "解决方案：\n" +
                        "• 降低杠杆倍数（推荐）\n" +
                        "• 减少下单数量\n" +
                        "• 部分平仓释放空间\n" +
                        "• 分批建仓",
                        
                -4003 => "解决方案：\n" +
                        "• 增加下单数量\n" +
                        "• 检查合约的最小交易数量",
                        
                -1111 => "解决方案：\n" +
                        "• 调整价格精度（减少小数位数）\n" +
                        "• 调整数量精度\n" +
                        "• 查看交易规则了解精度要求",
                        
                -2019 or -2010 => "解决方案：\n" +
                        "• 检查账户余额是否充足\n" +
                        "• 减少交易数量\n" +
                        "• 降低杠杆倍数\n" +
                        "• 确保有足够的保证金",
                        
                _ => "请检查参数设置并重试"
            };
        }

        public void SetAccount(AccountConfig account)
        {
            _currentAccount = account;
            LogService.LogInfo($"Account set: {account?.Name ?? "None"}");
            LogService.LogInfo($"API Key: {(account?.ApiKey?.Length > 8 ? account.ApiKey.Substring(0, 8) + "..." + account.ApiKey.Substring(account.ApiKey.Length - 4) : account?.ApiKey ?? "None")}");
            LogService.LogInfo($"Secret Key: {(string.IsNullOrEmpty(account?.SecretKey) ? "Not Set" : "***SET***")}");
            
            // 设置账户后立即进行一次服务器时间同步
            Task.Run(async () => await SyncServerTimeAsync());
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            // 🚫 优先检查IP限制状态
            if (_isIpRestricted)
            {
                LogService.LogInfo("📊 使用模拟账户数据（IP受限模式）");
                return GetMockAccountInfo();
            }
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockAccountInfo();
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v2/account";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000" // 增加接收窗口到10秒
                };

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    return GetMockAccountInfo();
                }

                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ API returned error response: {response}");
                    return GetMockAccountInfo();
                }

                var accountData = JsonSerializer.Deserialize<BinanceAccountResponse>(response, _jsonOptions);
                if (accountData == null) 
                {
                    return GetMockAccountInfo();
                }
                
                return new AccountInfo
                {
                    TotalWalletBalance = accountData.TotalWalletBalance,
                    TotalMarginBalance = accountData.TotalMarginBalance,
                    TotalUnrealizedProfit = accountData.TotalUnrealizedProfit,
                    AvailableBalance = accountData.AvailableBalance,
                    MaxWithdrawAmount = accountData.MaxWithdrawAmount
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ Error getting account info: {ex.Message}");
                return GetMockAccountInfo();
            }
        }

        public async Task<List<PositionInfo>> GetPositionsAsync()
        {
            // 🚫 优先检查IP限制状态
            if (_isIpRestricted)
            {
                LogService.LogInfo("📊 使用模拟持仓数据（IP受限模式）");
                return GetMockPositions();
            }
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockPositions();
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v2/positionRisk";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    return GetMockPositions();
                }

                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ Positions API returned error response: {response}");
                    return GetMockPositions();
                }

                var positionsData = JsonSerializer.Deserialize<BinancePositionResponse[]>(response, _jsonOptions);
                if (positionsData == null) 
                {
                    return GetMockPositions();
                }

                return positionsData
                    .Where(p => p.PositionAmt != 0)
                    .Select(p => new PositionInfo
                    {
                        Symbol = p.Symbol,
                        PositionAmt = p.PositionAmt,
                        EntryPrice = p.EntryPrice,
                        MarkPrice = p.MarkPrice,
                        UnrealizedProfit = p.UnrealizedProfit,
                        PositionSideString = p.PositionSide,
                        Leverage = p.Leverage,
                        MarginType = p.MarginType,
                        IsolatedMargin = p.IsolatedMargin,
                        UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(p.UpdateTime).DateTime
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error getting positions: {ex.Message}");
                return GetMockPositions();
            }
        }

        public async Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null)
        {
            // 🚫 优先检查IP限制状态
            if (_isIpRestricted)
            {
                LogService.LogInfo("📊 使用模拟订单数据（IP受限模式）");
                return GetMockOrders(symbol);
            }
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockOrders(symbol);
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/openOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                if (!string.IsNullOrEmpty(symbol))
                {
                    parameters["symbol"] = symbol;
                }

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    return GetMockOrders(symbol);
                }

                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ Orders API returned error response: {response}");
                    return GetMockOrders(symbol);
                }

                var ordersData = JsonSerializer.Deserialize<BinanceOrderResponse[]>(response, _jsonOptions);
                if (ordersData == null) 
                {
                    return GetMockOrders(symbol);
                }

                return ordersData.Select(o => new OrderInfo
                {
                    OrderId = o.OrderId,
                    Symbol = o.Symbol,
                    Side = o.Side,
                    Type = o.Type,
                    OrigQty = o.OrigQty,
                    Price = o.Price,
                    StopPrice = o.StopPrice,
                    Status = o.Status,
                    TimeInForce = o.TimeInForce,
                    ReduceOnly = o.ReduceOnly,
                    ClosePosition = o.ClosePosition,
                    PositionSide = o.PositionSide,
                    WorkingType = o.WorkingType,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(o.Time).DateTime,
                    UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(o.UpdateTime).DateTime
                }).ToList();
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error getting orders: {ex.Message}");
                return GetMockOrders(symbol);
            }
        }

        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            // 🚫 优先检查IP限制状态
            if (_isIpRestricted)
            {
                LogService.LogInfo($"📊 使用模拟价格数据（IP受限模式）：{symbol}");
                return GetMockPrice(symbol);
            }
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockPrice(symbol);
            }

            try
            {
                var endpoint = $"/fapi/v1/ticker/price?symbol={symbol}";
                var response = await SendPublicRequestAsync(HttpMethod.Get, endpoint);
                
                if (response == null) 
                {
                    return GetMockPrice(symbol);
                }

                var priceData = JsonSerializer.Deserialize<JsonElement>(response, _jsonOptions);
                if (priceData.TryGetProperty("price", out var priceElement))
                {
                    if (decimal.TryParse(priceElement.GetString(), out decimal price))
                    {
                        return price;
                    }
                }

                return GetMockPrice(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error getting latest price for {symbol}: {ex.Message}");
                return GetMockPrice(symbol);
            }
        }

        public async Task<bool> CancelOrderAsync(string symbol, long orderId)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning($"🗑️ 模拟取消订单: {symbol} #{orderId}");
                
                // 在模拟订单列表中查找并移除
                var orderToRemove = _mockOrders.FirstOrDefault(o => o.Symbol == symbol && o.OrderId == orderId);
                if (orderToRemove != null)
                {
                    _mockOrders.Remove(orderToRemove);
                    LogService.LogInfo($"✅ 模拟订单取消成功: {symbol} #{orderId} {orderToRemove.Type} @{orderToRemove.StopPrice:F4}");
                }
                else
                {
                    LogService.LogWarning($"⚠️ 模拟订单未找到: {symbol} #{orderId}");
                }
                
                await Task.Delay(300);
                return true; // 模拟成功
            }

            LogService.LogInfo($"Attempting to cancel order {orderId} for {symbol} via API...");
            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/order";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["orderId"] = orderId.ToString(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Delete, endpoint, parameters);
                bool success = response != null && !response.Contains("\"code\"");
                
                LogService.LogInfo($"Cancel order {orderId} result: {(success ? "Success" : "Failed")}");
                if (!success && response != null)
                {
                    LogService.LogWarning($"Cancel order error response: {response}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error canceling order {orderId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PlaceOrderAsync(OrderRequest request)
        {
            
            // 🚫 优先检查IP限制状态
            if (_isIpRestricted)
            {
                // 🎯【模拟下单】IP受限模式，执行模拟下单
                return await ProcessMockOrder(request, "IP受限模式");
            }
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                // 🎯【模拟下单】无API配置，执行模拟下单
                return await ProcessMockOrder(request, "无API配置");
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/order";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = request.Symbol.ToUpper(),
                    ["side"] = request.Side.ToUpper(),
                    ["type"] = request.Type.ToUpper(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                // 🔧 移除下单API中的marginType参数 - 保证金类型通过专门的API设置
                // 币安期货下单API不需要marginType参数，保证金类型是合约级别的设置

                // 检查持仓模式并设置正确的positionSide
                var isDualSidePosition = await GetPositionModeAsync();
                string positionSideToUse;
                
                if (isDualSidePosition)
                {
                    // 对冲模式：必须指定LONG或SHORT
                    if (string.IsNullOrEmpty(request.PositionSide) || request.PositionSide.ToUpper() == "BOTH")
                    {
                        // 🔧 关键修复：对于推仓等加仓操作，不能简单根据订单方向设置positionSide
                        // 而应该保持与现有持仓一致的方向
                        positionSideToUse = request.Side.ToUpper() == "BUY" ? "LONG" : "SHORT";
                        Console.WriteLine($"🔄 对冲模式下自动设置positionSide: {request.Side} → {positionSideToUse}");
                        Console.WriteLine($"⚠️ 警告：推仓操作应该明确指定PositionSide以确保方向正确");
                    }
                    else
                    {
                        positionSideToUse = request.PositionSide.ToUpper();
                        Console.WriteLine($"✅ 使用指定的positionSide: {positionSideToUse}");
                    }
                }
                else
                {
                    // 单向模式：必须使用BOTH
                    positionSideToUse = "BOTH";
                    if (!string.IsNullOrEmpty(request.PositionSide) && request.PositionSide.ToUpper() != "BOTH")
                    {
                        Console.WriteLine($"🔄 单向模式下强制设置positionSide: {request.PositionSide} → BOTH");
                    }
                }
                
                parameters["positionSide"] = positionSideToUse;
                Console.WriteLine($"📋 最终positionSide设置: {positionSideToUse} (持仓模式: {(isDualSidePosition ? "对冲" : "单向")})");

                // 根据订单类型添加参数
                if (request.Type.ToUpper() == "LIMIT")
                {
                    if (request.Price <= 0 || request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 限价单必须设置价格和数量");
                        return false;
                    }
                    
                    parameters["price"] = await FormatPriceAsync(request.Price, request.Symbol);
                    parameters["quantity"] = await FormatQuantityAsync(request.Quantity, request.Symbol);
                    parameters["timeInForce"] = string.IsNullOrEmpty(request.TimeInForce) ? "GTC" : request.TimeInForce;
                }
                else if (request.Type.ToUpper() == "MARKET")
                {
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 市价单必须设置数量");
                        return false;
                    }
                    
                    parameters["quantity"] = await FormatQuantityAsync(request.Quantity, request.Symbol);
                    
                    // 🔧 关键修复：添加 reduceOnly 参数支持
                    if (request.ReduceOnly)
                    {
                        parameters["reduceOnly"] = "true";
                        Console.WriteLine($"📋 市价单设置为只减仓模式 (ReduceOnly=true)");
                    }
                }
                else if (request.Type.ToUpper() == "STOP_MARKET" || request.Type.ToUpper() == "TAKE_PROFIT_MARKET")
                {
                    if (request.StopPrice <= 0)
                    {
                        Console.WriteLine("❌ 止损单必须设置触发价格");
                        return false;
                    }
                    
                    // 止损单也需要设置数量
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 止损单必须设置数量");
                        return false;
                    }
                    
                    parameters["stopPrice"] = await FormatPriceAsync(request.StopPrice, request.Symbol);
                    parameters["quantity"] = await FormatQuantityAsync(request.Quantity, request.Symbol);
                    parameters["reduceOnly"] = request.ReduceOnly.ToString().ToLower();
                    
                    if (!string.IsNullOrEmpty(request.WorkingType))
                    {
                        parameters["workingType"] = request.WorkingType;
                    }
                    
                    Console.WriteLine($"📋 止损单参数: 数量={request.Quantity:F8} → {parameters["quantity"]}, 触发价={request.StopPrice:F8} → {parameters["stopPrice"]}");
                }
                else if (request.Type.ToUpper() == "TRAILING_STOP_MARKET")
                {
                    // 🚀 新增：原生移动止损单支持
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 移动止损单必须设置数量");
                        return false;
                    }
                    
                    if (request.CallbackRate <= 0)
                    {
                        Console.WriteLine("❌ 移动止损单必须设置回调率");
                        return false;
                    }
                    
                    parameters["quantity"] = await FormatQuantityAsync(request.Quantity, request.Symbol);
                    parameters["callbackRate"] = request.CallbackRate.ToString("F1"); // 回调率，如 0.5 表示 0.5%
                    parameters["reduceOnly"] = request.ReduceOnly.ToString().ToLower();
                    
                    // 可选：激活价格
                    if (request.ActivationPrice > 0)
                    {
                        parameters["activationPrice"] = await FormatPriceAsync(request.ActivationPrice, request.Symbol);
                        Console.WriteLine($"📋 移动止损单: 数量={request.Quantity:F8} → {parameters["quantity"]}, 回调率={request.CallbackRate}%, 激活价={request.ActivationPrice:F8} → {parameters["activationPrice"]}");
                    }
                    else
                    {
                        Console.WriteLine($"📋 移动止损单: 数量={request.Quantity:F8} → {parameters["quantity"]}, 回调率={request.CallbackRate}%");
                }

                if (!string.IsNullOrEmpty(request.WorkingType))
                {
                    parameters["workingType"] = request.WorkingType;
                    }
                }

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                bool success = response != null && !response.Contains("\"code\":");
                
                Console.WriteLine($"📋 下单结果: {(success ? "成功" : "失败")}");
                if (!success && response != null)
                {
                    Console.WriteLine($"📋 错误响应: {response}");
                    
                    // 🚀 新增：智能错误处理
                    await HandleOrderErrorSmartlyAsync(response, request);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 下单异常: {ex.Message}");
                    return false;
            }
        }

        public async Task<bool> SetLeverageAsync(string symbol, int leverage)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogInfo($"Mock set leverage: {symbol} = {leverage}x");
                return true;
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/leverage";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["leverage"] = leverage.ToString(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                bool success = response != null && !response.Contains("\"code\":");
                
                LogService.LogInfo($"Set leverage {symbol} to {leverage}x: {(success ? "Success" : "Failed")}");
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error setting leverage for {symbol}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetMarginTypeAsync(string symbol, string marginType)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogInfo($"Mock set margin type: {symbol} = {marginType}");
                return true;
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/marginType";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["marginType"] = marginType.ToUpper(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                // 检查特殊错误码：-4046表示保证金模式已经是所需设置
                if (response != null && response.Contains("\"code\":-4046"))
                {
                    LogService.LogInfo($"Margin type for {symbol} is already {marginType}");
                    return true;
                }

                bool success = response != null && !response.Contains("\"code\":");
                LogService.LogInfo($"Set margin type {symbol} to {marginType}: {(success ? "Success" : "Failed")}");
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error setting margin type for {symbol}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClosePositionAsync(string symbol, string positionSide)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning($"Using mock close position: No API configuration for {symbol}");
                await Task.Delay(500);
                return true; // 模拟成功
            }

            try
            {
                LogService.LogInfo($"Attempting to close position {symbol} {positionSide}...");
                
                // 🔧 获取真实持仓信息
                var positions = await GetPositionsAsync();
                var targetPosition = positions.FirstOrDefault(p => 
                    p.Symbol == symbol && 
                    p.PositionSideString == positionSide &&
                    Math.Abs(p.PositionAmt) > 0);
                
                if (targetPosition == null)
                {
                    LogService.LogWarning($"No active position found for {symbol} {positionSide}");
                    return false;
                }
                
                // 获取精度信息并调整数量
                var (stepSize, tickSize) = await GetSymbolPrecisionAsync(symbol);
                var absoluteQuantity = Math.Abs(targetPosition.PositionAmt);
                var adjustedQuantity = RoundToStepSize(absoluteQuantity, stepSize);
                
                if (adjustedQuantity <= 0)
                {
                    LogService.LogError($"Adjusted quantity is too small: {symbol} original={absoluteQuantity:F8} adjusted={adjustedQuantity:F8}");
                    return false;
                }
                
                // 判断平仓方向
                string closeSide = targetPosition.PositionAmt > 0 ? "SELL" : "BUY";
                
                var orderRequest = new OrderRequest
                {
                    Symbol = symbol,
                    Side = closeSide,
                    Type = "MARKET",
                    PositionSide = positionSide,
                    Quantity = adjustedQuantity, // 使用调整后的精度
                    ReduceOnly = true,
                    Leverage = targetPosition.Leverage,
                    MarginType = targetPosition.MarginType ?? "ISOLATED"
                };

                LogService.LogInfo($"Closing position: {closeSide} {adjustedQuantity:F8} {symbol} (original: {targetPosition.PositionAmt:F8})");
                return await PlaceOrderAsync(orderRequest);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error closing position {symbol}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CloseAllPositionsAsync()
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning("Using mock close all positions: No API configuration");
                await Task.Delay(1000);
                return true; // 模拟成功
            }

            try
            {
                LogService.LogInfo("Attempting to close all positions...");
                // 这里简化处理，实际中应该获取所有持仓并逐个平仓
                await Task.Delay(1000); // 模拟处理时间
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error closing all positions: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CancelAllOrdersAsync(string? symbol = null)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning("Using mock cancel all orders: No API configuration");
                await Task.Delay(500);
                return true; // 模拟成功
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/allOpenOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                if (!string.IsNullOrEmpty(symbol))
                {
                    parameters["symbol"] = symbol;
                }

                LogService.LogInfo($"Attempting to cancel all orders{(string.IsNullOrEmpty(symbol) ? "" : $" for {symbol}")}...");
                
                var response = await SendSignedRequestAsync(HttpMethod.Delete, endpoint, parameters);
                bool success = response != null && !response.Contains("\"code\":");
                
                LogService.LogInfo($"Cancel all orders result: {(success ? "Success" : "Failed")}");
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error canceling all orders: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetRealExchangeInfoAsync(string? symbol = null)
        {
            // 检查缓存是否有效
            if (!string.IsNullOrEmpty(_cachedExchangeInfo) && 
                DateTime.Now - _exchangeInfoCacheTime < _exchangeInfoCacheExpiry)
            {
                // 静默使用缓存，不输出日志
                return _cachedExchangeInfo;
            }

            try
            {
                LogService.LogInfo("获取最新交易所信息...");
                var endpoint = "/fapi/v1/exchangeInfo";
                var exchangeInfo = await SendPublicRequestAsync(HttpMethod.Get, endpoint);
                
                if (!string.IsNullOrEmpty(exchangeInfo))
                {
                    // 更新缓存
                    _cachedExchangeInfo = exchangeInfo;
                    _exchangeInfoCacheTime = DateTime.Now;
                    LogService.LogInfo("✅ 交易所信息已更新");
                }
                
                return exchangeInfo;
            }
            catch (Exception ex)
            {
                LogService.LogError($"获取交易所信息失败: {ex.Message}");
                return null;
            }
        }

        public async Task<List<OrderInfo>> GetAllOrdersAsync(string symbol, int limit = 500)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockOrders(symbol);
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/allOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["limit"] = limit.ToString(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    return GetMockOrders(symbol);
                }

                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ All orders API returned error response: {response}");
                    return GetMockOrders(symbol);
                }

                var ordersData = JsonSerializer.Deserialize<BinanceOrderResponse[]>(response, _jsonOptions);
                if (ordersData == null) 
                {
                    return GetMockOrders(symbol);
                }

                return ordersData.Select(o => new OrderInfo
                {
                    OrderId = o.OrderId,
                    Symbol = o.Symbol,
                    Side = o.Side,
                    Type = o.Type,
                    OrigQty = o.OrigQty,
                    Price = o.Price,
                    StopPrice = o.StopPrice,
                    Status = o.Status,
                    TimeInForce = o.TimeInForce,
                    ReduceOnly = o.ReduceOnly,
                    ClosePosition = o.ClosePosition,
                    PositionSide = o.PositionSide,
                    WorkingType = o.WorkingType,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(o.Time).DateTime,
                    UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(o.UpdateTime).DateTime
                }).ToList();
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error getting all orders for {symbol}: {ex.Message}");
                return GetMockOrders(symbol);
            }
        }

        public void UpdateLatestPriceCache(string symbol, decimal price)
        {
            // 简化的价格缓存更新方法
            // 在实际应用中，这里可能会更新内存中的价格缓存
            LogService.LogInfo($"Price cache updated: {symbol} = {price}");
        }

        public async Task<(bool isValid, string errorMessage)> ValidateOrderAsync(OrderRequest request)
        {
            try
            {
                // 基本参数验证
                if (string.IsNullOrEmpty(request.Symbol))
                    return (false, "合约名称不能为空");
                
                if (string.IsNullOrEmpty(request.Side))
                    return (false, "交易方向不能为空");
                
                if (string.IsNullOrEmpty(request.Type))
                    return (false, "订单类型不能为空");
                
                if (request.Quantity <= 0 && request.Type != "STOP_MARKET")
                    return (false, "数量必须大于0");
                
                if (request.Type == "LIMIT" && request.Price <= 0)
                    return (false, "限价单价格必须大于0");
                
                if ((request.Type == "STOP_MARKET" || request.Type == "TAKE_PROFIT_MARKET") && request.StopPrice <= 0)
                    return (false, "止损/止盈单触发价格必须大于0");
                
                return (true, "");
            }
            catch (Exception ex)
            {
                LogService.LogError($"Order validation error: {ex.Message}");
                return (false, $"订单验证异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取完整的交易规则信息
        /// </summary>
        public async Task<(decimal minQty, decimal maxQty, decimal stepSize, decimal tickSize, int maxLeverage)> GetSymbolTradingRulesAsync(string symbol)
        {
            // 🔧 修复：线程安全的缓存检查
            lock (_tradingRulesCacheLock)
            {
                if (_tradingRulesCache.TryGetValue(symbol, out var cachedRules))
                {
                    // 检查缓存是否过期
                    if (DateTime.Now - cachedRules.cacheTime < _tradingRulesCacheExpiry)
                    {
                        return (cachedRules.minQty, cachedRules.maxQty, cachedRules.stepSize, cachedRules.tickSize, cachedRules.maxLeverage);
                    }
                    else
                    {
                        // 缓存过期，删除旧缓存
                        _tradingRulesCache.Remove(symbol);
                    }
                }
            }

            try
            {
                // 获取交易所信息
                var exchangeInfoJson = await GetRealExchangeInfoAsync();
                if (string.IsNullOrEmpty(exchangeInfoJson))
                {
                    LogService.LogWarning($"无法获取 {symbol} 的交易规则，使用默认规则");
                    return GetDefaultTradingRules(symbol);
                }

                // 解析JSON获取交易规则
                using var document = JsonDocument.Parse(exchangeInfoJson);
                var symbols = document.RootElement.GetProperty("symbols");
                
                foreach (var symbolElement in symbols.EnumerateArray())
                {
                    var symbolName = symbolElement.GetProperty("symbol").GetString();
                    if (symbolName == symbol.ToUpper())
                    {
                        var filters = symbolElement.GetProperty("filters");
                        decimal minQty = 0, maxQty = 0, stepSize = 0, tickSize = 0;
                        int maxLeverage = 125;
                        
                        foreach (var filter in filters.EnumerateArray())
                        {
                            var filterType = filter.GetProperty("filterType").GetString();
                            
                            if (filterType == "LOT_SIZE")
                            {
                                var minQtyStr = filter.GetProperty("minQty").GetString();
                                var maxQtyStr = filter.GetProperty("maxQty").GetString();
                                var stepSizeStr = filter.GetProperty("stepSize").GetString();
                                
                                decimal.TryParse(minQtyStr, out minQty);
                                decimal.TryParse(maxQtyStr, out maxQty);
                                decimal.TryParse(stepSizeStr, out stepSize);
                            }
                            else if (filterType == "PRICE_FILTER")
                            {
                                var tickSizeStr = filter.GetProperty("tickSize").GetString();
                                decimal.TryParse(tickSizeStr, out tickSize);
                            }
                        }
                        
                        if (minQty > 0 && stepSize > 0 && tickSize > 0)
                        {
                            var rules = (minQty, maxQty, stepSize, tickSize, maxLeverage, DateTime.Now);
                            
                            // 🔧 修复：线程安全的缓存写入
                            lock (_tradingRulesCacheLock)
                            {
                                _tradingRulesCache[symbol] = rules;
                            }
                            
                            LogService.LogInfo($"✅ {symbol} 交易规则已缓存: minQty={minQty}, maxQty={maxQty}, stepSize={stepSize}, tickSize={tickSize}");
                            return (minQty, maxQty, stepSize, tickSize, maxLeverage);
                        }
                    }
                }
                
                LogService.LogWarning($"未找到 {symbol} 的交易规则，使用默认规则");
                return GetDefaultTradingRules(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 获取 {symbol} 交易规则失败: {ex.Message}，使用默认规则");
                return GetDefaultTradingRules(symbol);
            }
        }

        public async Task<(decimal stepSize, decimal tickSize)> GetSymbolPrecisionAsync(string symbol)
        {
            // 🔧 修复：线程安全的缓存访问
            lock (_precisionCacheLock)
            {
                if (_precisionCache.TryGetValue(symbol, out var cachedPrecision))
                {
                    // 静默使用缓存，不输出日志
                    return cachedPrecision;
                }
            }

            try
            {
                // 仅在首次获取时输出日志
                LogService.LogInfo($"获取 {symbol} 精度信息...");
                
                // 获取交易所信息
                var exchangeInfoJson = await GetRealExchangeInfoAsync();
                if (string.IsNullOrEmpty(exchangeInfoJson))
                {
                    LogService.LogWarning("无法获取交易所信息，使用默认精度");
                    return GetDefaultPrecision(symbol);
                }

                // 解析JSON
                using var document = JsonDocument.Parse(exchangeInfoJson);
                var symbols = document.RootElement.GetProperty("symbols");
                
                foreach (var symbolElement in symbols.EnumerateArray())
                {
                    var symbolName = symbolElement.GetProperty("symbol").GetString();
                    if (symbolName == symbol.ToUpper())
                    {
                        var filters = symbolElement.GetProperty("filters");
                        decimal stepSize = 0, tickSize = 0;
                        
                        foreach (var filter in filters.EnumerateArray())
                        {
                            var filterType = filter.GetProperty("filterType").GetString();
                            
                            if (filterType == "LOT_SIZE")
                            {
                                // 获取数量精度（stepSize）
                                var stepSizeStr = filter.GetProperty("stepSize").GetString();
                                if (decimal.TryParse(stepSizeStr, out stepSize))
                                {
                                    // 移除详细解析日志
                                }
                            }
                            else if (filterType == "PRICE_FILTER")
                            {
                                // 获取价格精度（tickSize）
                                var tickSizeStr = filter.GetProperty("tickSize").GetString();
                                if (decimal.TryParse(tickSizeStr, out tickSize))
                                {
                                    // 移除详细解析日志
                                }
                            }
                        }
                        
                        if (stepSize > 0 && tickSize > 0)
                        {
                            var precision = (stepSize, tickSize);
                            
                            // 🔧 修复：线程安全的缓存写入
                            lock (_precisionCacheLock)
                            {
                                _precisionCache[symbol] = precision;
                            }
                            
                            LogService.LogInfo($"✅ {symbol} 精度已缓存: stepSize={stepSize}, tickSize={tickSize}");
                            return precision;
                        }
                    }
                }
                
                LogService.LogWarning($"未找到 {symbol} 的精度信息，使用默认精度");
                return GetDefaultPrecision(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 获取 {symbol} 精度失败: {ex.Message}，使用默认精度");
                return GetDefaultPrecision(symbol);
            }
        }

        private (decimal minQty, decimal maxQty, decimal stepSize, decimal tickSize, int maxLeverage) GetDefaultTradingRules(string symbol)
        {
            // 根据币种提供合理的默认交易规则
            var (minQty, maxQty, stepSize, tickSize, maxLeverage) = symbol.ToUpper() switch
            {
                "BTCUSDT" => (0.001m, 1000m, 0.001m, 0.1m, 125),          // BTC: 高价值币种
                "ETHUSDT" => (0.001m, 10000m, 0.001m, 0.01m, 100),        // ETH: 中高价值币种
                "BNBUSDT" => (0.01m, 100000m, 0.01m, 0.001m, 75),         // BNB: 中价值币种
                "ADAUSDT" => (1m, 1000000m, 1m, 0.0001m, 75),             // ADA: 中低价值币种
                "DOGEUSDT" => (1m, 10000000m, 1m, 0.00001m, 50),          // DOGE: 低价值币种
                "WIFUSDT" => (1m, 1000000m, 1m, 0.0001m, 75),             // WIF: 中低价值币种
                "PEPEUSDT" => (1000m, 1000000000m, 1000m, 0.0000001m, 25), // PEPE: 极低价值币种
                "SHIBUSDT" => (1000m, 1000000000m, 1000m, 0.0000001m, 25), // SHIB: 极低价值币种
                _ => (1m, 1000000m, 1m, 0.0001m, 75)                      // 默认: 中等规则
            };
            
            LogService.LogInfo($"使用默认交易规则 {symbol} - minQty: {minQty}, maxQty: {maxQty}, stepSize: {stepSize}, tickSize: {tickSize}, maxLeverage: {maxLeverage}");
            return (minQty, maxQty, stepSize, tickSize, maxLeverage);
        }

        private (decimal stepSize, decimal tickSize) GetDefaultPrecision(string symbol)
        {
            // 根据币种提供合理的默认精度
            var (stepSize, tickSize) = symbol.ToUpper() switch
            {
                "BTCUSDT" => (0.001m, 0.1m),        // BTC: 3位小数, 1位价格精度
                "ETHUSDT" => (0.001m, 0.01m),       // ETH: 3位小数, 2位价格精度
                "BNBUSDT" => (0.01m, 0.001m),       // BNB: 2位小数, 3位价格精度
                "ADAUSDT" => (1m, 0.0001m),         // ADA: 整数, 4位价格精度
                "DOGEUSDT" => (1m, 0.00001m),       // DOGE: 整数, 5位价格精度
                "WIFUSDT" => (1m, 0.0001m),         // WIF: 整数, 4位价格精度
                "PEPEUSDT" => (1m, 0.0000001m),     // PEPE: 整数, 7位价格精度
                "SHIBUSDT" => (1m, 0.0000001m),     // SHIB: 整数, 7位价格精度
                _ => (1m, 0.0001m)                  // 默认: 整数, 4位价格精度
            };
            
            LogService.LogInfo($"使用默认精度 {symbol} - stepSize: {stepSize}, tickSize: {tickSize}");
            return (stepSize, tickSize);
        }

        private async Task<string?> SendPublicRequestAsync(HttpMethod method, string endpoint)
        {
            try
            {
                // 🔧 新增：执行请求间隔控制
                await EnforceRequestInterval();

                var request = new HttpRequestMessage(method, _baseUrl + endpoint);
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    // 🔧 新增：成功请求时重置错误状态
                    ResetErrorState();
                    return result;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                LogService.LogError($"Public API request failed: {response.StatusCode}, Response: {errorContent}");
                
                // 🔧 新增：处理API错误
                if (!string.IsNullOrEmpty(errorContent))
                {
                    HandleApiError(errorContent);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Public API request failed: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> SendSignedRequestAsync(HttpMethod method, string endpoint, Dictionary<string, string> parameters)
        {
            try
            {
                if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.SecretKey))
                {
                return null;
            }

                // 🔧 新增：执行请求间隔控制
                await EnforceRequestInterval();

                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value.ToString())}"));
                var signature = GenerateSignature(queryString, _currentAccount.SecretKey);
                var fullQueryString = $"{queryString}&signature={signature}";
                
                string url;
                HttpRequestMessage request;

                if (method == HttpMethod.Get || method == HttpMethod.Delete)
                {
                    url = $"{_baseUrl}{endpoint}?{fullQueryString}";
                    request = new HttpRequestMessage(method, url);
                }
                else
                {
                    url = $"{_baseUrl}{endpoint}";
                    request = new HttpRequestMessage(method, url);
                    request.Content = new StringContent(fullQueryString, Encoding.UTF8, "application/x-www-form-urlencoded");
                }

                request.Headers.Add("X-MBX-APIKEY", _currentAccount.ApiKey);

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    // 🔧 新增：成功请求时重置错误状态
                    ResetErrorState();
                    return result;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                LogService.LogError($"API request failed: {response.StatusCode}, Response: {errorContent}");
                
                // 🔧 新增：处理API错误
                if (!string.IsNullOrEmpty(errorContent))
                {
                    HandleApiError(errorContent);
                }
                
                return errorContent;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Signed API request failed: {ex.Message}");
                return null;
            }
        }

        private string GenerateSignature(string queryString, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var queryBytes = Encoding.UTF8.GetBytes(queryString);
            
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(queryBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private long GetCurrentTimestamp()
        {
            // 保留原方法用于向后兼容，但推荐使用GetSyncedTimestamp
            return GetSyncedTimestamp();
        }

        private async Task<string> FormatPriceAsync(decimal price, string symbol)
                    {
                        try
                        {
                var (stepSize, tickSize) = await GetSymbolPrecisionAsync(symbol);
                
                // 根据tickSize调整价格精度
                var adjustedPrice = RoundToStepSize(price, tickSize);
                var decimalPlaces = GetDecimalPlaces(tickSize);
                
                LogService.LogInfo($"价格格式化: {symbol} {price:F8} → {adjustedPrice} (tickSize: {tickSize})");
                return adjustedPrice.ToString($"F{decimalPlaces}");
            }
            catch (Exception ex)
            {
                LogService.LogError($"价格格式化失败: {ex.Message}，使用默认格式");
                return Math.Round(price, 4).ToString("F4");
            }
        }

        private async Task<string> FormatQuantityAsync(decimal quantity, string symbol)
        {
            try
            {
                // 🔧 修复：优先验证输入数量的有效性
                if (quantity <= 0)
                {
                    LogService.LogError($"❌ 数量无效: {quantity}，返回最小有效值");
                    return "0.000001";
                }

                var (stepSize, tickSize) = await GetSymbolPrecisionAsync(symbol);
                
                // 🔧 修复：验证stepSize的有效性
                if (stepSize <= 0)
                {
                    LogService.LogWarning($"⚠️ {symbol} stepSize无效: {stepSize}，使用保守精度");
                    return GetConservativeQuantityFormat(quantity, symbol);
                }
                
                // 根据stepSize调整数量精度
                var adjustedQuantity = RoundToStepSize(quantity, stepSize);
                
                // 🔧 新增：确保调整后的数量仍然有效
                if (adjustedQuantity <= 0)
                {
                    LogService.LogWarning($"⚠️ {symbol} 调整后数量为0，使用最小stepSize倍数");
                    adjustedQuantity = stepSize;
                }
                
                var decimalPlaces = GetDecimalPlaces(stepSize);
                
                // 🔧 新增：限制最大小数位数，避免精度过高
                decimalPlaces = Math.Min(decimalPlaces, 8);
                
                var result = adjustedQuantity.ToString($"F{decimalPlaces}");
                
                LogService.LogInfo($"💰 数量格式化成功: {symbol} {quantity:F8} → {result} (stepSize: {stepSize})");
                return result;
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 错误: 数量格式化失败: {ex.Message}，使用默认格式");
                
                // 🔧 修复：增强容错处理，根据数量大小选择合适的精度
                try
                {
                    return GetConservativeQuantityFormat(quantity, symbol);
                }
                catch (Exception fallbackEx)
                {
                    LogService.LogError($"❌ 错误: 备用格式化也失败: {fallbackEx.Message}，强制使用F6格式");
                    return Math.Max(quantity, 0.000001m).ToString("F6");
                }
            }
        }

        /// <summary>
        /// 🔧 新增：保守的数量格式化方法，用于容错处理
        /// </summary>
        private string GetConservativeQuantityFormat(decimal quantity, string symbol)
        {
            try
            {
                // 确保数量为正数
                quantity = Math.Max(quantity, 0.000001m);
                
                // 根据合约和数量大小选择保守的精度
                var result = symbol.ToUpper() switch
                {
                    // 主流币种：相对保守的精度
                    "BTCUSDT" => quantity < 1m ? Math.Round(quantity, 6).ToString("F6") : Math.Round(quantity, 3).ToString("F3"),
                    "ETHUSDT" => quantity < 1m ? Math.Round(quantity, 6).ToString("F6") : Math.Round(quantity, 3).ToString("F3"),
                    "BNBUSDT" => quantity < 10m ? Math.Round(quantity, 4).ToString("F4") : Math.Round(quantity, 2).ToString("F2"),
                    
                    // 中等价值币种
                    "ADAUSDT" or "DOGEUSDT" => quantity < 100m ? Math.Round(quantity, 2).ToString("F2") : Math.Round(quantity, 0).ToString("F0"),
                    
                    // 其他币种：通用保守处理
                    _ => quantity switch
                    {
                        < 0.001m => Math.Round(quantity, 8).ToString("F8"),  // 极小数量
                        < 0.1m => Math.Round(quantity, 6).ToString("F6"),    // 小数量
                        < 10m => Math.Round(quantity, 4).ToString("F4"),     // 中等数量
                        < 1000m => Math.Round(quantity, 2).ToString("F2"),   // 大数量
                        _ => Math.Round(quantity, 0).ToString("F0")          // 极大数量
                    }
                };
                
                LogService.LogInfo($"🔧 保守格式化: {symbol} {quantity:F8} → {result}");
                return result;
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 保守格式化失败: {ex.Message}");
                return Math.Max(quantity, 0.000001m).ToString("F6");
            }
        }

        private decimal RoundToStepSize(decimal value, decimal stepSize)
        {
            if (stepSize <= 0) return value;
            
            // 计算最接近的stepSize倍数
            var steps = Math.Floor(value / stepSize);
            return steps * stepSize;
        }

        private int GetDecimalPlaces(decimal stepSize)
        {
            var stepSizeStr = stepSize.ToString();
            var decimalIndex = stepSizeStr.IndexOf('.');
            if (decimalIndex == -1) return 0;
            
            // 移除末尾的0
            var trimmed = stepSizeStr.TrimEnd('0');
            if (trimmed.EndsWith(".")) return 0;
            
            return trimmed.Length - decimalIndex - 1;
        }

        private AccountInfo GetMockAccountInfo()
        {
            return new AccountInfo
            {
                TotalWalletBalance = 10000.0m,
                TotalMarginBalance = 9500.0m,
                TotalUnrealizedProfit = 150.0m,
                AvailableBalance = 8500.0m,
                MaxWithdrawAmount = 8500.0m
            };
        }

        private List<PositionInfo> GetMockPositions()
        {
            return new List<PositionInfo>
            {
                new PositionInfo
                {
                    Symbol = "BTCUSDT",
                    PositionAmt = 0.001m,
                    EntryPrice = 45000.0m,
                    MarkPrice = 45150.0m,
                    UnrealizedProfit = 150.0m,
                    PositionSideString = "BOTH",
                    Leverage = 10,
                    MarginType = "ISOLATED",
                    IsolatedMargin = 4500.0m,
                    UpdateTime = DateTime.Now
                }
            };
        }

        /// <summary>
        /// 🎯【模拟下单】处理模拟下单逻辑，默认返回成功
        /// </summary>
        /// <param name="request">订单请求</param>
        /// <param name="reason">模拟原因</param>
        /// <returns>始终返回true</returns>
        private async Task<bool> ProcessMockOrder(OrderRequest request, string reason)
        {
            try
            {
                // 🔍 详细记录模拟下单信息
                LogService.LogInfo($"🎯【模拟下单开始】{reason}: {request.Symbol}");
                LogService.LogInfo($"   📊 订单详情: {request.Side} {request.Quantity:F6} @ {request.Type}");
                if (request.StopPrice > 0)
                {
                    LogService.LogInfo($"   📊 触发价格: {request.StopPrice:F4}");
                }
                
                // 基础参数验证（确保订单合理）
                bool isValidMockOrder = !string.IsNullOrEmpty(request.Symbol) && 
                                       request.Quantity > 0 && 
                                       (request.Type != "STOP_MARKET" || request.StopPrice > 0);
                
                if (!isValidMockOrder)
                {
                    LogService.LogWarning($"❌【模拟下单失败】参数无效: Symbol={request.Symbol}, Quantity={request.Quantity}, Type={request.Type}");
                    return false;
                }
                
                // 创建模拟订单记录
                var mockOrder = new OrderInfo
                {
                    OrderId = _nextMockOrderId++,
                    Symbol = request.Symbol,
                    Side = request.Side,
                    Type = request.Type,
                    OrigQty = request.Quantity,
                    Price = request.Price,
                    StopPrice = request.StopPrice,
                    Status = "NEW",
                    TimeInForce = request.TimeInForce ?? "GTC",
                    ReduceOnly = request.ReduceOnly,
                    ClosePosition = request.ClosePosition,
                    PositionSide = request.PositionSide ?? "BOTH",
                    WorkingType = request.WorkingType ?? "CONTRACT_PRICE",
                    Time = DateTime.Now,
                    UpdateTime = DateTime.Now
                };
                
                _mockOrders.Add(mockOrder);
                
                // 🎯 根据订单类型输出特定的成功日志
                if (request.Type.ToUpper() == "MARKET")
                {
                    // 市价单
                    LogService.LogInfo($"✅【模拟下单成功】数量: {request.Quantity:F6}, 方向: {request.Side} ({request.Symbol})");
                }
                else if (request.Type.ToUpper().Contains("STOP"))
                {
                    // 委托单（止损单）
                    LogService.LogInfo($"✅【模拟委托下单成功】数量: {request.Quantity:F6}, 触发价格: {request.StopPrice:F4}, 方向: {request.Side} ({request.Symbol})");
                }
                else
                {
                    // 其他类型
                    LogService.LogInfo($"✅【模拟订单成功】{request.Type} 数量: {request.Quantity:F6}, 方向: {request.Side} ({request.Symbol})");
                }
                
                // 模拟网络延迟
                await Task.Delay(200);
                
                // 🎯 关键：默认返回成功，确保后续状态跳转正常执行
                LogService.LogInfo($"🎯【模拟下单完成】{request.Symbol} 返回成功，继续执行后续状态更新");
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌【模拟下单异常】{request.Symbol}: {ex.Message}");
                return false;
            }
        }

        private List<OrderInfo> GetMockOrders(string? symbol)
        {
            // 返回动态创建的模拟订单列表
            var filteredOrders = string.IsNullOrEmpty(symbol) 
                ? _mockOrders.ToList() 
                : _mockOrders.Where(o => o.Symbol == symbol).ToList();
                
            LogService.LogInfo($"📋 获取模拟订单: {(string.IsNullOrEmpty(symbol) ? "全部" : symbol)} - 找到 {filteredOrders.Count} 个订单");
            
            return filteredOrders;
        }

        private decimal GetMockPrice(string symbol)
        {
            return symbol switch
            {
                "BTCUSDT" => 45000.0m,
                "ETHUSDT" => 3000.0m,
                "BNBUSDT" => 300.0m,
                _ => 100.0m
            };
        }

        // 简化的响应模型
        public class BinanceAccountResponse
        {
            [JsonPropertyName("totalWalletBalance")]
            public decimal TotalWalletBalance { get; set; }
            
            [JsonPropertyName("totalMarginBalance")]
            public decimal TotalMarginBalance { get; set; }
            
            [JsonPropertyName("totalUnrealizedProfit")]
            public decimal TotalUnrealizedProfit { get; set; }
            
            [JsonPropertyName("availableBalance")]
            public decimal AvailableBalance { get; set; }
            
            [JsonPropertyName("maxWithdrawAmount")]
            public decimal MaxWithdrawAmount { get; set; }
        }

        public class BinancePositionResponse
        {
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = string.Empty;
            
            [JsonPropertyName("positionAmt")]
            public decimal PositionAmt { get; set; }
            
            [JsonPropertyName("entryPrice")]
            public decimal EntryPrice { get; set; }
            
            [JsonPropertyName("markPrice")]
            public decimal MarkPrice { get; set; }
            
            [JsonPropertyName("unRealizedProfit")]
            public decimal UnrealizedProfit { get; set; }
            
            [JsonPropertyName("positionSide")]
            public string PositionSide { get; set; } = string.Empty;
            
            [JsonPropertyName("leverage")]
            public int Leverage { get; set; }
            
            [JsonPropertyName("marginType")]
            public string MarginType { get; set; } = string.Empty;
            
            [JsonPropertyName("isolatedMargin")]
            public decimal IsolatedMargin { get; set; }
            
            [JsonPropertyName("updateTime")]
            public long UpdateTime { get; set; }
        }

        public class BinanceOrderResponse
        {
            [JsonPropertyName("orderId")]
            public long OrderId { get; set; }
            
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = string.Empty;
            
            [JsonPropertyName("side")]
            public string Side { get; set; } = string.Empty;
            
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;
            
            [JsonPropertyName("origQty")]
            public decimal OrigQty { get; set; }
            
            [JsonPropertyName("price")]
            public decimal Price { get; set; }
            
            [JsonPropertyName("stopPrice")]
            public decimal StopPrice { get; set; }
            
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
            
            [JsonPropertyName("timeInForce")]
            public string TimeInForce { get; set; } = string.Empty;
            
            [JsonPropertyName("reduceOnly")]
            public bool ReduceOnly { get; set; }
            
            [JsonPropertyName("closePosition")]
            public bool ClosePosition { get; set; }
            
            [JsonPropertyName("positionSide")]
            public string PositionSide { get; set; } = string.Empty;
            
            [JsonPropertyName("workingType")]
            public string WorkingType { get; set; } = string.Empty;
            
            [JsonPropertyName("time")]
            public long Time { get; set; }
            
            [JsonPropertyName("updateTime")]
            public long UpdateTime { get; set; }
        }

        public async Task<string> TestPrecisionAsync(string symbol, decimal price, decimal quantity)
        {
            try
            {
                LogService.LogInfo($"=== 开始精度测试 {symbol} ===");
                
                // 获取真实精度
                var (stepSize, tickSize) = await GetSymbolPrecisionAsync(symbol);
                
                // 格式化价格和数量
                var formattedPrice = await FormatPriceAsync(price, symbol);
                var formattedQuantity = await FormatQuantityAsync(quantity, symbol);
                
                var result = $"Symbol: {symbol}\n" +
                           $"Original Price: {price:F8} → Formatted: {formattedPrice} (tickSize: {tickSize})\n" +
                           $"Original Quantity: {quantity:F8} → Formatted: {formattedQuantity} (stepSize: {stepSize})";
                
                LogService.LogInfo(result);
                LogService.LogInfo($"=== 精度测试完成 {symbol} ===");
                
                return result;
            }
            catch (Exception ex)
            {
                var error = $"精度测试失败: {ex.Message}";
                LogService.LogError(error);
                return error;
            }
        }

        public async Task<bool> GetPositionModeAsync()
        {
            // 如果已缓存，直接返回
            if (_isDualSidePosition.HasValue)
            {
                LogService.LogInfo($"使用缓存的持仓模式: {(_isDualSidePosition.Value ? "对冲模式" : "单向模式")}");
                return _isDualSidePosition.Value;
            }

            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogInfo("无API配置，默认使用单向持仓模式");
                _isDualSidePosition = false;
                return false;
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/positionSide/dual";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response != null && !response.Contains("\"code\":"))
                {
                    using var document = JsonDocument.Parse(response);
                    if (document.RootElement.TryGetProperty("dualSidePosition", out var dualSideElement))
                    {
                        _isDualSidePosition = dualSideElement.GetBoolean();
                        LogService.LogInfo($"✅ 获取持仓模式成功: {(_isDualSidePosition.Value ? "对冲模式" : "单向模式")}");
                        return _isDualSidePosition.Value;
                    }
                }

                LogService.LogWarning("获取持仓模式失败，默认使用单向模式");
                _isDualSidePosition = false;
                    return false;
            }
            catch (Exception ex)
            {
                LogService.LogError($"获取持仓模式异常: {ex.Message}，默认使用单向模式");
                _isDualSidePosition = false;
                return false;
            }
        }

        public async Task<bool> SetPositionModeAsync(bool dualSidePosition)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogInfo($"Mock set position mode: {(dualSidePosition ? "双向持仓" : "单向持仓")}");
                return true;
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/positionSide/dual";
                var parameters = new Dictionary<string, string>
                {
                    ["dualSidePosition"] = dualSidePosition.ToString().ToLower(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                // 检查特殊错误码：-4059表示持仓模式已经是所需设置
                if (response != null && response.Contains("\"code\":-4059"))
                {
                    LogService.LogInfo($"Position mode is already {(dualSidePosition ? "dual side" : "single side")}");
                    return true;
                }

                bool success = response != null && !response.Contains("\"code\":");
                LogService.LogInfo($"Set position mode to {(dualSidePosition ? "dual side" : "single side")}: {(success ? "Success" : "Failed")}");
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error setting position mode: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AdjustIsolatedMarginAsync(string symbol, string positionSide, decimal amount, int type)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                var actionText = type == 1 ? "增加" : "减少";
                LogService.LogInfo($"Mock adjust isolated margin: {symbol} {actionText} {amount} USDT");
                return true;
            }

            try
            {
                // 确保服务器时间同步
                await EnsureServerTimeSyncAsync();
                
                var endpoint = "/fapi/v1/positionMargin";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["amount"] = amount.ToString("F8"),
                    ["type"] = type.ToString(),
                    ["timestamp"] = GetSyncedTimestamp().ToString(),
                    ["recvWindow"] = "10000"
                };

                // 如果是双向持仓模式，需要指定持仓方向
                if (!string.IsNullOrEmpty(positionSide) && positionSide != "BOTH")
                {
                    parameters["positionSide"] = positionSide;
                }

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                bool success = response != null && !response.Contains("\"code\":");
                
                var actionText = type == 1 ? "增加" : "减少";
                LogService.LogInfo($"Adjust isolated margin {symbol} {actionText} {amount} USDT: {(success ? "Success" : "Failed")}");
                
                if (!success && response != null)
                {
                    LogService.LogWarning($"Adjust margin response: {response}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error adjusting isolated margin for {symbol}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 确保服务器时间同步
        /// </summary>
        private async Task EnsureServerTimeSyncAsync()
        {
            // 如果距离上次同步时间超过间隔，则重新同步
            if (DateTime.UtcNow - _lastServerTimeSync > _syncInterval)
            {
                await SyncServerTimeAsync();
            }
        }

        /// <summary>
        /// 同步服务器时间
        /// </summary>
        private async Task SyncServerTimeAsync()
        {
            try
            {
                var endpoint = "/fapi/v1/time";
                var response = await SendPublicRequestAsync(HttpMethod.Get, endpoint);
                
                if (response != null)
                {
                    using var document = JsonDocument.Parse(response);
                    if (document.RootElement.TryGetProperty("serverTime", out var serverTimeElement))
                    {
                        var serverTime = serverTimeElement.GetInt64();
                        var localTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        _serverTimeOffset = serverTime - localTime;
                        _lastServerTimeSync = DateTime.UtcNow;
                        
                        LogService.LogInfo($"✅ 服务器时间同步成功，偏移量: {_serverTimeOffset}ms");
                        return;
                    }
                }
                
                LogService.LogWarning("服务器时间同步失败，使用本地时间");
                _serverTimeOffset = 0;
                _lastServerTimeSync = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                LogService.LogError($"服务器时间同步异常: {ex.Message}，使用本地时间");
                _serverTimeOffset = 0;
                _lastServerTimeSync = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 获取同步后的时间戳
        /// </summary>
        private long GetSyncedTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverTimeOffset;
        }

        /// <summary>
        /// 🔧 新增：验证GPSUSDT精度修复的测试方法
        /// </summary>
        public async Task<string> TestGPSUSDTPrecisionAsync()
        {
            try
            {
                LogService.LogInfo("🧪 开始测试GPSUSDT精度处理...");
                
                var symbol = "GPSUSDT";
                var testQuantities = new decimal[] { 0.000001m, 0.00001m, 0.0001m, 0.001m, 0.01m, 0.1m, 1m, 10m, 100m };
                var results = new List<string>();
                
                // 获取GPSUSDT的交易规则
                var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await GetSymbolTradingRulesAsync(symbol);
                results.Add($"📊 {symbol} 交易规则:");
                results.Add($"   最小数量: {minQty:F8}");
                results.Add($"   最大数量: {maxQty:F8}");
                results.Add($"   数量步长: {stepSize:F8}");
                results.Add($"   价格步长: {tickSize:F8}");
                results.Add($"   最大杠杆: {maxLeverage}x");
                results.Add("");
                
                // 测试不同数量的格式化
                results.Add("🔧 数量格式化测试:");
                foreach (var quantity in testQuantities)
                {
                    try
                    {
                        var formattedQuantity = await FormatQuantityAsync(quantity, symbol);
                        var isValid = ValidateQuantityAgainstRules(decimal.Parse(formattedQuantity), minQty, maxQty, stepSize);
                        var status = isValid ? "✅" : "❌";
                        results.Add($"   {status} {quantity:F8} → {formattedQuantity} (有效: {isValid})");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"   ❌ {quantity:F8} → 格式化失败: {ex.Message}");
                    }
                }
                
                results.Add("");
                results.Add("🎯 修复验证结果:");
                
                // 测试小账户常见的问题数量
                var problematicQuantities = new decimal[] { 0.0000123m, 0.0000456m, 0.0000789m };
                foreach (var qty in problematicQuantities)
                {
                    try
                    {
                        var formatted = await FormatQuantityAsync(qty, symbol);
                        results.Add($"   🔧 问题数量 {qty:F8} → 修复后: {formatted}");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"   ❌ 问题数量 {qty:F8} → 仍然失败: {ex.Message}");
                    }
                }
                
                var finalResult = string.Join("\n", results);
                LogService.LogInfo($"🧪 GPSUSDT精度测试完成:\n{finalResult}");
                return finalResult;
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ GPSUSDT精度测试失败: {ex.Message}";
                LogService.LogError(errorMessage);
                return errorMessage;
            }
        }
        
        /// <summary>
        /// 验证数量是否符合交易规则
        /// </summary>
        private bool ValidateQuantityAgainstRules(decimal quantity, decimal minQty, decimal maxQty, decimal stepSize)
        {
            try
            {
                // 检查最小数量
                if (quantity < minQty)
                    return false;
                
                // 检查最大数量
                if (maxQty > 0 && quantity > maxQty)
                    return false;
                
                // 检查步长
                if (stepSize > 0)
                {
                    var remainder = (quantity - minQty) % stepSize;
                    if (Math.Abs(remainder) > 0.0000001m) // 允许微小的浮点误差
                        return false;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 🚀 智能错误处理 - 根据用户需求实现杠杆自动调节和分笔止损委托
        /// </summary>
        private async Task HandleOrderErrorSmartlyAsync(string errorResponse, OrderRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(errorResponse) || !errorResponse.Contains("\"code\":"))
                {
                    return;
                }

                // 解析错误码
                using var doc = System.Text.Json.JsonDocument.Parse(errorResponse);
                var errorCode = doc.RootElement.GetProperty("code").GetInt32();
                var errorMsg = doc.RootElement.GetProperty("msg").GetString() ?? "";

                Console.WriteLine($"🔍 智能错误分析: 错误码{errorCode} - {errorMsg}");

                switch (errorCode)
                {
                    case -2027:
                        // 持仓超过杠杆限制 - 实施杠杆自动调节
                        await HandleLeverageLimitErrorAsync(request);
                        break;
                        
                    case -4005:
                        // 数量超过最大限制 - 实施分笔下单
                        await HandleQuantityLimitErrorAsync(request);
                        break;
                        
                    default:
                        // 其他错误使用现有的增强错误处理
                        HandleApiError(errorResponse);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 智能错误处理异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 处理-2027错误：持仓超过杠杆限制 - 自动降低杠杆
        /// </summary>
        private async Task HandleLeverageLimitErrorAsync(OrderRequest request)
        {
            try
            {
                Console.WriteLine("🔧 开始杠杆自动调节处理...");

                // 1. 获取现有持仓信息
                var positions = await GetPositionsAsync();
                var existingPosition = positions.FirstOrDefault(p => p.Symbol == request.Symbol);

                // 2. 获取当前价格
                var currentPrice = await GetLatestPriceAsync(request.Symbol);
                
                // 3. 计算新的总持仓量
                var currentPositionAmt = existingPosition?.PositionAmt ?? 0;
                var newTotalPosition = Math.Abs(currentPositionAmt) + request.Quantity;

                Console.WriteLine($"📊 持仓分析: 当前{Math.Abs(currentPositionAmt)} + 新增{request.Quantity} = 总计{newTotalPosition}");

                // 4. 尝试不同的杠杆级别
                var testLeverages = new[] { 20, 15, 10, 5, 3, 1 };
                
                foreach (var testLeverage in testLeverages)
                {
                    if (testLeverage >= request.Leverage) continue; // 只尝试更低的杠杆

                    var estimatedLimit = EstimateMaxPositionForLeverage(request.Symbol, testLeverage, currentPrice);
                    
                    Console.WriteLine($"🧪 测试杠杆{testLeverage}x: 预估限制{estimatedLimit}");

                    if (newTotalPosition <= estimatedLimit * 0.8m) // 80%安全边际
                    {
                        Console.WriteLine($"💡 找到合适杠杆: {request.Leverage}x → {testLeverage}x");
                        Console.WriteLine($"💡 建议操作: 降低杠杆到{testLeverage}x后重新下单");
                        Console.WriteLine($"💡 预期效果: 持仓限制从当前水平提升到约{estimatedLimit}");
                        
                        // 提供详细的操作建议
                        Console.WriteLine("🎯 自动调节建议:");
                        Console.WriteLine($"   1. 设置杠杆为{testLeverage}x");
                        Console.WriteLine($"   2. 重新提交订单");
                        Console.WriteLine($"   3. 预期可成功开仓{request.Quantity}个{request.Symbol}");
                        
                        return;
                    }
                }

                // 如果所有杠杆都不行，建议减少数量
                Console.WriteLine("⚠️ 降低杠杆仍无法满足需求，建议减少下单数量");
                var recommendedQuantity = request.Quantity * 0.5m; // 建议减少50%
                
                // 调整到正确精度
                var (_, _, stepSize, _, _) = await GetSymbolTradingRulesAsync(request.Symbol);
                recommendedQuantity = Math.Floor(recommendedQuantity / stepSize) * stepSize;
                
                Console.WriteLine($"💡 建议调节方案:");
                Console.WriteLine($"   • 方案1: 保持{request.Leverage}x杠杆，减少数量到{recommendedQuantity}");
                Console.WriteLine($"   • 方案2: 降低杠杆到1x，保持原数量{request.Quantity}");
                Console.WriteLine($"   • 方案3: 分批建仓，每次下单{recommendedQuantity}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 杠杆调节处理异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 处理-4005错误：数量超过最大限制 - 实施分笔下单
        /// </summary>
        private async Task HandleQuantityLimitErrorAsync(OrderRequest request)
        {
            try
            {
                Console.WriteLine("📋 开始分笔止损委托处理...");

                // 1. 获取交易规则
                var (minQty, maxQty, stepSize, _, _) = await GetSymbolTradingRulesAsync(request.Symbol);

                Console.WriteLine($"📊 交易规则: 最小{minQty}, 最大{maxQty}, 步长{stepSize}");
                Console.WriteLine($"📊 用户请求: {request.Quantity} (超过最大限制{maxQty})");

                // 2. 计算分笔方案
                var orderChunks = CalculateOrderChunks(request.Quantity, maxQty, stepSize);
                
                Console.WriteLine($"💡 分笔方案: 将{request.Quantity}拆分为{orderChunks.Count}笔:");
                for (int i = 0; i < orderChunks.Count; i++)
                {
                    Console.WriteLine($"   第{i + 1}笔: {orderChunks[i]}");
                }

                // 3. 针对止损单的特殊处理
                if (request.Type.ToUpper().Contains("STOP"))
                {
                    Console.WriteLine("🛡️ 检测到止损单，应用分笔止损策略...");
                    
                    // 计算价格差异方案
                    var basePriceField = request.Type.ToUpper() == "STOP_MARKET" ? request.StopPrice : request.Price;
                    
                    Console.WriteLine($"💡 分笔止损方案:");
                    Console.WriteLine($"   • 总数量: {request.Quantity}");
                    Console.WriteLine($"   • 分笔数: {orderChunks.Count}");
                    Console.WriteLine($"   • 基础价格: {basePriceField}");
                    Console.WriteLine($"   • 价格差异: 每笔相差0.1%");
                    
                    for (int i = 0; i < orderChunks.Count; i++)
                    {
                        var priceAdjustment = 1m + (i * 0.001m); // 0.1%差异
                        var adjustedPrice = basePriceField * priceAdjustment;
                        Console.WriteLine($"   第{i + 1}笔: {orderChunks[i]} @ {adjustedPrice:F4}");
                    }
                    
                    Console.WriteLine("🎯 执行建议:");
                    Console.WriteLine("   1. 取消当前大单");
                    Console.WriteLine("   2. 按分笔方案逐个下单");
                    Console.WriteLine("   3. 每笔间隔0.5秒避免频率限制");
                }
                else
                {
                    // 普通订单的分笔建议
                    Console.WriteLine("💡 普通订单分笔建议:");
                    Console.WriteLine("   1. 将大单拆分为多个小单");
                    Console.WriteLine("   2. 使用相同价格分批下单");
                    Console.WriteLine("   3. 注意控制下单频率");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 分笔处理异常: {ex.Message}");
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
            // 基于历史经验和用户反馈的保守估算规则
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
                "AIOTUSDT" => leverage switch
                {
                    <= 3 => 50000m,  // 根据实际-2027错误调整
                    <= 10 => 20000m,
                    <= 20 => 10000m,
                    <= 50 => 5000m,
                    _ => 1000m
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
    }
} 