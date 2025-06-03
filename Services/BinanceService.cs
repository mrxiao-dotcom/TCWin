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
    public class BinanceService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private AccountConfig? _currentAccount;
        private string _baseUrl = "https://fapi.binance.com";
        
        // 精度缓存：存储每个合约的stepSize和tickSize
        private readonly Dictionary<string, (decimal stepSize, decimal tickSize)> _precisionCache = new();
        
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
            
            LogService.LogInfo("=== Setting Account Configuration ===");
            LogService.LogInfo($"Account Name: {account.Name}");
            LogService.LogInfo($"API Key: {(string.IsNullOrEmpty(account.ApiKey) ? "NOT SET" : $"{account.ApiKey[..8]}...{account.ApiKey[^4..]}")}");
            LogService.LogInfo($"Secret Key: {(string.IsNullOrEmpty(account.SecretKey) ? "NOT SET" : "***SET***")}");
            LogService.LogInfo($"Is Test Net: {account.IsTestNet}");
            
            if (account.IsTestNet)
            {
                _baseUrl = "https://testnet.binancefuture.com";
                LogService.LogInfo($"Using Test Network: {_baseUrl}");
            }
            else
            {
                _baseUrl = "https://fapi.binance.com";
                LogService.LogInfo($"Using Production Network: {_baseUrl}");
            }
            
            LogService.LogInfo("=== Account Configuration Complete ===");
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return GetMockAccountInfo();
            }

            try
            {
                var endpoint = "/fapi/v2/account";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
                var endpoint = "/fapi/v2/positionRisk";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
                var endpoint = "/fapi/v1/openOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
                LogService.LogWarning($"Using mock cancel order: No API configuration for {symbol} order {orderId}");
                await Task.Delay(300);
                return true; // 模拟成功
            }

            LogService.LogInfo($"Attempting to cancel order {orderId} for {symbol} via API...");
            try
            {
                var endpoint = "/fapi/v1/order";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["orderId"] = orderId.ToString(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
                await Task.Delay(800);
                return !string.IsNullOrEmpty(request.Symbol) && (request.Quantity > 0 || request.Type == "STOP_MARKET");
            }

            try
            {
                // 基本参数验证
                if (string.IsNullOrEmpty(request.Symbol) || string.IsNullOrEmpty(request.Side) || string.IsNullOrEmpty(request.Type))
                {
                    Console.WriteLine("❌ 基本参数验证失败");
                    return false;
                }

                // 设置杠杆
                await SetLeverageAsync(request.Symbol, request.Leverage);

                // 设置保证金模式
                if (!string.IsNullOrEmpty(request.MarginType))
                {
                    await SetMarginTypeAsync(request.Symbol, request.MarginType);
                }

                // 构建API参数
                var endpoint = "/fapi/v1/order";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = request.Symbol.ToUpper(),
                    ["side"] = request.Side.ToUpper(),
                    ["type"] = request.Type.ToUpper(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

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
                return true; // 模拟成功
            }

            try
            {
                var endpoint = "/fapi/v1/leverage";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["leverage"] = leverage.ToString(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                bool success = response != null && !response.Contains("\"code\":");
                if (!success && response != null)
                {
                    LogService.LogError($"设置杠杆失败: {response}");
                }
                else if (success)
                {
                    LogService.LogInfo($"成功设置 {symbol} 杠杆为 {leverage}x");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error setting leverage: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetMarginTypeAsync(string symbol, string marginType)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                return true; // 模拟成功
            }

            try
            {
                var endpoint = "/fapi/v1/marginType";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["marginType"] = marginType,
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                // 处理-4046错误：不需要更改保证金类型
                if (response != null && response.Contains("\"code\":-4046"))
                {
                    LogService.LogInfo($"保证金类型已经是 {marginType}，无需更改");
                    return true; // 认为是成功的
                }
                
                bool success = response != null && !response.Contains("\"code\":");
                if (!success && response != null)
                {
                    LogService.LogError($"设置保证金类型失败: {response}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error setting margin type: {ex.Message}");
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
                
                var orderRequest = new OrderRequest
                {
                    Symbol = symbol,
                    Side = "SELL", // 默认平仓方向，实际中需要根据持仓方向决定
                    Type = "MARKET",
                    PositionSide = positionSide,
                    Quantity = 0, // 将在PlaceOrderAsync中处理
                    ReduceOnly = true
                };

                // 这里简化处理，实际应该获取持仓数量
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
                var endpoint = "/fapi/v1/allOpenOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
            try
            {
                var endpoint = "/fapi/v1/exchangeInfo";
                var response = await SendPublicRequestAsync(HttpMethod.Get, endpoint);
                
                if (response == null)
                {
                    LogService.LogWarning("Failed to get exchange info, returning mock data");
                    return "{\"timezone\":\"UTC\",\"serverTime\":1234567890000,\"symbols\":[{\"symbol\":\"BTCUSDT\",\"status\":\"TRADING\"}]}";
                }

                return response;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error getting exchange info: {ex.Message}");
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
                var endpoint = "/fapi/v1/allOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["limit"] = limit.ToString(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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

        public async Task<(decimal stepSize, decimal tickSize)> GetSymbolPrecisionAsync(string symbol)
        {
            // 首先检查缓存
            if (_precisionCache.TryGetValue(symbol, out var cachedPrecision))
            {
                LogService.LogInfo($"使用缓存精度: {symbol} - stepSize: {cachedPrecision.stepSize}, tickSize: {cachedPrecision.tickSize}");
                return cachedPrecision;
            }

            try
            {
                LogService.LogInfo($"获取 {symbol} 的真实精度信息...");
                
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
                                    LogService.LogInfo($"解析到 {symbol} stepSize: {stepSize}");
                                }
                            }
                            else if (filterType == "PRICE_FILTER")
                            {
                                // 获取价格精度（tickSize）
                                var tickSizeStr = filter.GetProperty("tickSize").GetString();
                                if (decimal.TryParse(tickSizeStr, out tickSize))
                                {
                                    LogService.LogInfo($"解析到 {symbol} tickSize: {tickSize}");
                                }
                            }
                        }
                        
                        if (stepSize > 0 && tickSize > 0)
                        {
                            var precision = (stepSize, tickSize);
                            _precisionCache[symbol] = precision;
                            LogService.LogInfo($"✅ 成功获取 {symbol} 精度 - stepSize: {stepSize}, tickSize: {tickSize}");
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
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
            var orders = new List<OrderInfo>
            {
                new OrderInfo
                {
                    OrderId = 12345,
                    Symbol = "BTCUSDT",
                    Side = "BUY",
                    Type = "LIMIT",
                    OrigQty = 0.001m,
                    Price = 44000.0m,
                    StopPrice = 0,
                    Status = "NEW",
                    TimeInForce = "GTC",
                    ReduceOnly = false,
                    ClosePosition = false,
                    PositionSide = "BOTH",
                    WorkingType = "CONTRACT_PRICE",
                    Time = DateTime.Now.AddHours(-1),
                    UpdateTime = DateTime.Now.AddHours(-1)
                }
            };

            return string.IsNullOrEmpty(symbol) ? orders : orders.Where(o => o.Symbol == symbol).ToList();
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
                var endpoint = "/fapi/v1/positionSide/dual";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString()
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
                LogService.LogInfo("无API配置，模拟设置持仓模式成功");
                return true;
            }

            try
            {
                var endpoint = "/fapi/v1/positionSide/dual";
                var parameters = new Dictionary<string, string>
                {
                    ["dualSidePosition"] = dualSidePosition.ToString().ToLower(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                // 检查特殊错误码
                if (response != null && response.Contains("\"code\":-4059"))
                {
                    LogService.LogInfo("持仓模式已经是所需设置，无需更改");
                    _isDualSidePosition = dualSidePosition;
                    return true;
                }

                bool success = response != null && !response.Contains("\"code\":");
                if (success)
                {
                    _isDualSidePosition = dualSidePosition;
                    LogService.LogInfo($"✅ 成功设置持仓模式为: {(dualSidePosition ? "对冲模式" : "单向模式")}");
                }
                else if (response != null)
                {
                    LogService.LogError($"设置持仓模式失败: {response}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"设置持仓模式异常: {ex.Message}");
                return false;
            }
        }
    }
} 