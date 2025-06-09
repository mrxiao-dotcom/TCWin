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

namespace BinanceFuturesTrader.Services
{
    public class BinanceService : IBinanceService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private AccountConfig? _currentAccount;
        private string _baseUrl = "https://fapi.binance.com";
        
        // 时间偏移量用于同步服务器时间
        private long _serverTimeOffset = 0;
        private DateTime _lastServerTimeSync = DateTime.MinValue;
        private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5); // 每5分钟同步一次服务器时间
        
        // 精度缓存：存储每个合约的stepSize和tickSize
        private readonly Dictionary<string, (decimal stepSize, decimal tickSize)> _precisionCache = new();
        
        // 完整交易规则缓存：存储每个合约的完整交易规则
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
            Console.WriteLine("\n" + "=".PadLeft(80, '='));
            Console.WriteLine("🚀 开始币安期货下单流程");
            Console.WriteLine("=".PadLeft(80, '='));
            
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                Console.WriteLine("⚠️ 使用模拟下单: 无API配置");
                Console.WriteLine($"📋 模拟订单参数: {request.Symbol} {request.Type} {request.Side} 数量:{request.Quantity:F8} 止损价:{request.StopPrice:F4}");
                
                // 模拟下单验证
                bool isValidMockOrder = !string.IsNullOrEmpty(request.Symbol) && 
                                       request.Quantity > 0 && 
                                       (request.Type != "STOP_MARKET" || request.StopPrice > 0);
                                       
                if (isValidMockOrder)
                {
                    // 创建模拟订单并添加到列表
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
                    Console.WriteLine($"✅ 模拟订单创建成功: #{mockOrder.OrderId} {request.Symbol} {request.Type} @{request.StopPrice:F4}");
                }
                                       
                Console.WriteLine($"📋 模拟下单结果: {(isValidMockOrder ? "成功" : "失败")}");
                await Task.Delay(800);
                return isValidMockOrder;
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
                        // 根据订单方向自动设置
                        positionSideToUse = request.Side.ToUpper() == "BUY" ? "LONG" : "SHORT";
                        Console.WriteLine($"🔄 对冲模式下自动设置positionSide: {request.Side} → {positionSideToUse}");
                }
                else
                {
                        positionSideToUse = request.PositionSide.ToUpper();
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
            // 首先检查缓存
            if (_tradingRulesCache.TryGetValue(symbol, out var cachedRules))
            {
                // 检查缓存是否过期
                if (DateTime.Now - cachedRules.cacheTime < _tradingRulesCacheExpiry)
                {
                    // 静默使用缓存，不输出日志
                    return (cachedRules.minQty, cachedRules.maxQty, cachedRules.stepSize, cachedRules.tickSize, cachedRules.maxLeverage);
                }
                else
                {
                    // 缓存过期，删除旧缓存
                    _tradingRulesCache.Remove(symbol);
                }
            }

            try
            {
                // 仅在首次获取时输出日志
                LogService.LogInfo($"获取 {symbol} 交易规则...");
                
                // 获取交易所信息
                var exchangeInfoJson = await GetRealExchangeInfoAsync();
                if (string.IsNullOrEmpty(exchangeInfoJson))
                {
                    LogService.LogWarning("无法获取交易所信息，使用默认规则");
                    return GetDefaultTradingRules(symbol);
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
                        decimal minQty = 0, maxQty = 0, stepSize = 0, tickSize = 0;
                        int maxLeverage = 125; // 默认杠杆
                        
                        foreach (var filter in filters.EnumerateArray())
                        {
                            var filterType = filter.GetProperty("filterType").GetString();
                            
                            if (filterType == "LOT_SIZE")
                            {
                                // 获取数量相关限制
                                if (filter.TryGetProperty("minQty", out var minQtyElement))
                                    decimal.TryParse(minQtyElement.GetString(), out minQty);
                                if (filter.TryGetProperty("maxQty", out var maxQtyElement))
                                    decimal.TryParse(maxQtyElement.GetString(), out maxQty);
                                if (filter.TryGetProperty("stepSize", out var stepSizeElement))
                                    decimal.TryParse(stepSizeElement.GetString(), out stepSize);
                            }
                            else if (filterType == "PRICE_FILTER")
                            {
                                // 获取价格精度
                                if (filter.TryGetProperty("tickSize", out var tickSizeElement))
                                    decimal.TryParse(tickSizeElement.GetString(), out tickSize);
                            }
                        }
                        
                        if (minQty > 0 && maxQty > 0 && stepSize > 0 && tickSize > 0)
                        {
                            // 缓存结果
                            var tradingRules = (minQty, maxQty, stepSize, tickSize, maxLeverage, DateTime.Now);
                            _tradingRulesCache[symbol] = tradingRules;
                            
                            // 同时更新精度缓存
                            _precisionCache[symbol] = (stepSize, tickSize);
                            
                            // 仅在首次获取时输出详细日志
                            LogService.LogInfo($"✅ {symbol} 规则已缓存");
                            return (minQty, maxQty, stepSize, tickSize, maxLeverage);
                        }
                    }
                }
                
                LogService.LogWarning($"未找到 {symbol} 的交易规则，使用默认规则");
                return GetDefaultTradingRules(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"获取 {symbol} 交易规则失败: {ex.Message}，使用默认规则");
                return GetDefaultTradingRules(symbol);
            }
        }

        public async Task<(decimal stepSize, decimal tickSize)> GetSymbolPrecisionAsync(string symbol)
        {
            // 首先检查缓存
            if (_precisionCache.TryGetValue(symbol, out var cachedPrecision))
            {
                // 静默使用缓存，不输出日志
                return cachedPrecision;
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
                            _precisionCache[symbol] = precision;
                            LogService.LogInfo($"✅ {symbol} 精度已缓存");
                            return precision;
                        }
                    }
                }
                
                LogService.LogWarning($"未找到 {symbol} 的精度信息，使用默认精度");
                return GetDefaultPrecision(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"获取 {symbol} 精度失败: {ex.Message}，使用默认精度");
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
                var request = new HttpRequestMessage(method, _baseUrl + endpoint);
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
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
                    return await response.Content.ReadAsStringAsync();
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                LogService.LogError($"API request failed: {response.StatusCode}, Response: {errorContent}");
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
                var (stepSize, tickSize) = await GetSymbolPrecisionAsync(symbol);
                
                // 根据stepSize调整数量精度
                var adjustedQuantity = RoundToStepSize(quantity, stepSize);
                var decimalPlaces = GetDecimalPlaces(stepSize);
                
                LogService.LogInfo($"数量格式化: {symbol} {quantity:F8} → {adjustedQuantity} (stepSize: {stepSize})");
                return adjustedQuantity.ToString($"F{decimalPlaces}");
            }
            catch (Exception ex)
            {
                LogService.LogError($"数量格式化失败: {ex.Message}，使用默认格式");
                return Math.Round(quantity, 3).ToString("F3");
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
    }
} 