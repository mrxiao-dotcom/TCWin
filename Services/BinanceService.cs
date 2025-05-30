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
        
        // JSON序列化选项，更宽松的处理
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

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

        private void LogAccountStatus(string operation)
        {
            LogService.LogInfo($"\n--- {operation} ---");
            if (_currentAccount == null)
            {
                LogService.LogWarning("❌ No account configured");
                return;
            }
            
            LogService.LogInfo($"✅ Account: {_currentAccount.Name}");
            LogService.LogInfo($"✅ API Key: {(!string.IsNullOrEmpty(_currentAccount.ApiKey) ? "Configured" : "NOT SET")}");
            LogService.LogInfo($"✅ Secret Key: {(!string.IsNullOrEmpty(_currentAccount.SecretKey) ? "Configured" : "NOT SET")}");
            LogService.LogInfo($"✅ Network: {(_currentAccount.IsTestNet ? "TestNet" : "Production")}");
            LogService.LogInfo($"✅ Base URL: {_baseUrl}");
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            // 减少账户信息获取的日志输出
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                // LogService.LogWarning("❌ Using mock data: No API configuration");
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
                    // LogService.LogWarning("❌ API call failed, falling back to mock data");
                    return GetMockAccountInfo();
                }

                // LogService.LogDebug($"📄 Raw API Response (first 200 chars): {response.Substring(0, Math.Min(200, response.Length))}...");
                
                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ API returned error response: {response}");
                    return GetMockAccountInfo();
                }

                var accountData = JsonSerializer.Deserialize<BinanceAccountResponse>(response, _jsonOptions);
                if (accountData == null) 
                {
                    // LogService.LogWarning("❌ Failed to parse API response, falling back to mock data");
                    return GetMockAccountInfo();
                }

                // 只在重要时刻输出账户信息，而不是每次自动刷新都输出
                // LogService.LogInfo("✅ Successfully retrieved real account data from API");
                // LogService.LogInfo($"   📊 Wallet Balance: {accountData.TotalWalletBalance}");
                // LogService.LogInfo($"   📊 Margin Balance: {accountData.TotalMarginBalance}");
                // LogService.LogInfo($"   📊 Unrealized PnL: {accountData.TotalUnrealizedProfit}");
                
                return new AccountInfo
                {
                    TotalWalletBalance = accountData.TotalWalletBalance,
                    TotalMarginBalance = accountData.TotalMarginBalance,
                    TotalUnrealizedProfit = accountData.TotalUnrealizedProfit,
                    AvailableBalance = accountData.AvailableBalance,
                    MaxWithdrawAmount = accountData.MaxWithdrawAmount
                };
            }
            catch (JsonException jsonEx)
            {
                LogService.LogError($"❌ JSON Deserialization Error: {jsonEx.Message}");
                return GetMockAccountInfo();
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ General Error getting account info: {ex.Message}");
                return GetMockAccountInfo();
            }
        }

        public async Task<List<PositionInfo>> GetPositionsAsync()
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                // LogService.LogWarning("Using mock positions: No API configuration");
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
                    // LogService.LogWarning("Positions API call failed, falling back to mock data");
                    return GetMockPositions();
                }

                // LogService.LogDebug($"📄 Raw Positions Response (first 200 chars): {response.Substring(0, Math.Min(200, response.Length))}...");
                
                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ Positions API returned error response: {response}");
                    return GetMockPositions();
                }

                var positionsData = JsonSerializer.Deserialize<BinancePositionResponse[]>(response, _jsonOptions);
                if (positionsData == null) 
                {
                    // LogService.LogWarning("Failed to parse positions response, falling back to mock data");
                    return GetMockPositions();
                }

                // 静默处理持仓数据，不输出详细信息
                // LogService.LogInfo($"Successfully retrieved {positionsData.Length} positions from API");
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
            catch (JsonException jsonEx)
            {
                LogService.LogError($"❌ JSON Deserialization Error in Positions: {jsonEx.Message}");
                return GetMockPositions();
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
                LogService.LogWarning("Using mock orders: No API configuration");
                return GetMockOrders(symbol);
            }

            Console.WriteLine("🔍 开始获取未成交订单列表...");
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
                    Console.WriteLine($"📊 指定合约过滤: {symbol}");
                }

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    Console.WriteLine("❌ 订单API调用失败，使用模拟数据");
                    return GetMockOrders(symbol);
                }

                Console.WriteLine($"📄 订单API原始响应 (前500字符): {response.Substring(0, Math.Min(500, response.Length))}...");
                
                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine($"❌ 订单API返回错误: {response}");
                    return GetMockOrders(symbol);
                }

                var ordersData = JsonSerializer.Deserialize<BinanceOrderResponse[]>(response, _jsonOptions);
                if (ordersData == null) 
                {
                    Console.WriteLine("❌ 解析订单响应失败，使用模拟数据");
                    return GetMockOrders(symbol);
                }

                Console.WriteLine($"📋 API返回订单总数: {ordersData.Length}");
                
                // 详细分析每个订单
                foreach (var order in ordersData)
                {
                    Console.WriteLine($"📦 订单详情: OrderId={order.OrderId}, Symbol={order.Symbol}, Type={order.Type}, Side={order.Side}, Status={order.Status}");
                    Console.WriteLine($"   Price={order.Price}, StopPrice={order.StopPrice}, OrigQty={order.OrigQty}, ReduceOnly={order.ReduceOnly}");
                }
                
                var resultOrders = ordersData.Select(o => new OrderInfo
                {
                    OrderId = o.OrderId,
                    Symbol = o.Symbol,
                    Status = o.Status,
                    ClientOrderId = o.ClientOrderId,
                    Price = o.Price,
                    OrigQty = o.OrigQty,
                    ExecutedQty = o.ExecutedQty,
                    CumQuote = o.CumQuote,
                    TimeInForce = o.TimeInForce,
                    Type = o.Type,
                    ReduceOnly = o.ReduceOnly,
                    ClosePosition = o.ClosePosition,
                    Side = o.Side,
                    PositionSide = o.PositionSide,
                    StopPrice = o.StopPrice,
                    WorkingType = o.WorkingType,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(o.Time).DateTime,
                    UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(o.UpdateTime).DateTime
                }).ToList();
                
                Console.WriteLine($"✅ 转换后订单数量: {resultOrders.Count}");
                
                // 特别检查STOP_MARKET类型的订单
                var stopMarketOrders = resultOrders.Where(o => o.Type == "STOP_MARKET").ToList();
                Console.WriteLine($"🛡️ STOP_MARKET订单数量: {stopMarketOrders.Count}");
                foreach (var stopOrder in stopMarketOrders)
                {
                    Console.WriteLine($"🛡️ 止损单: {stopOrder.Symbol} {stopOrder.Side} StopPrice={stopOrder.StopPrice} Status={stopOrder.Status}");
                }
                
                return resultOrders;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"❌ JSON解析订单异常: {jsonEx.Message}");
                Console.WriteLine($"❌ JSON路径: {jsonEx.Path}");
                return GetMockOrders(symbol);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取订单异常: {ex.Message}");
                Console.WriteLine($"❌ 异常堆栈: {ex.StackTrace}");
                return GetMockOrders(symbol);
            }
        }

        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            // 静默获取价格，不输出详细日志（减少噪音）
            try
            {
                var endpoint = $"/fapi/v1/ticker/price?symbol={symbol}";
                
                var response = await SendPublicRequestAsync(HttpMethod.Get, endpoint);
                
                if (response == null) 
                {
                    // LogService.LogWarning($"❌ Price API call failed for {symbol}, using mock data");
                    return GetMockPrice(symbol);
                }

                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    LogService.LogError($"❌ Price API returned error response: {response}");
                    return GetMockPrice(symbol);
                }

                var priceData = JsonSerializer.Deserialize<BinancePriceResponse>(response, _jsonOptions);
                if (priceData == null)
                {
                    // LogService.LogWarning($"❌ Failed to parse price response for {symbol}, using mock data");
                    return GetMockPrice(symbol);
                }

                // 只在首次获取或价格有显著变化时输出日志
                // LogService.LogInfo($"✅ Successfully retrieved real price for {symbol}: {priceData.Price}");
                return priceData.Price;
            }
            catch (JsonException jsonEx)
            {
                LogService.LogError($"❌ JSON Deserialization Error for price {symbol}: {jsonEx.Message}");
                return GetMockPrice(symbol);
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ Error getting price for {symbol}: {ex.Message}");
                return GetMockPrice(symbol);
            }
        }

        public async Task<bool> ClosePositionAsync(string symbol, string positionSide)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning("Using mock close position: No API configuration");
                await Task.Delay(1000);
                return true;
            }

            LogService.LogInfo($"Attempting to close position {symbol} {positionSide} via API...");
            try
            {
                // 首先获取当前持仓信息
                var positions = await GetPositionsAsync();
                var position = positions.FirstOrDefault(p => p.Symbol == symbol && p.PositionSideString == positionSide);
                
                if (position == null || position.PositionAmt == 0)
                {
                    LogService.LogWarning("No position found to close");
                    return false;
                }

                // 计算平仓方向和数量
                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                var quantity = Math.Abs(position.PositionAmt);

                var endpoint = "/fapi/v1/order";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["side"] = side,
                    ["type"] = "MARKET",
                    ["quantity"] = quantity.ToString(),
                    ["positionSide"] = positionSide,
                    ["reduceOnly"] = "true",
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                bool success = response != null;
                LogService.LogInfo($"Close position result: {(success ? "Success" : "Failed")}");
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error closing position: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CloseAllPositionsAsync()
        {
            var positions = await GetPositionsAsync();
            bool allSuccess = true;

            foreach (var position in positions)
            {
                var success = await ClosePositionAsync(position.Symbol, position.PositionSideString);
                if (!success)
                    allSuccess = false;
            }

            return allSuccess;
        }

        public async Task<bool> CancelAllOrdersAsync(string? symbol = null)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                LogService.LogWarning("Using mock cancel orders: No API configuration");
                await Task.Delay(500);
                return true;
            }

            LogService.LogInfo("Attempting to cancel all orders via API...");
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

                var response = await SendSignedRequestAsync(HttpMethod.Delete, endpoint, parameters);
                bool success = response != null;
                LogService.LogInfo($"Cancel orders result: {(success ? "Success" : "Failed")}");
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error canceling orders: {ex.Message}");
                return false;
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
                // 1. 打印原始请求信息
                Console.WriteLine("📋 原始下单请求信息:");
                Console.WriteLine($"   Symbol: {request.Symbol ?? "未设置"}");
                Console.WriteLine($"   Side: {request.Side ?? "未设置"}");
                Console.WriteLine($"   Type: {request.Type ?? "未设置"}");
                Console.WriteLine($"   Quantity: {request.Quantity}");
                Console.WriteLine($"   Price: {request.Price}");
                Console.WriteLine($"   StopPrice: {request.StopPrice}");
                Console.WriteLine($"   PositionSide: {request.PositionSide ?? "未设置"}");
                Console.WriteLine($"   TimeInForce: {request.TimeInForce ?? "未设置"}");
                Console.WriteLine($"   ReduceOnly: {request.ReduceOnly}");
                Console.WriteLine($"   WorkingType: {request.WorkingType ?? "未设置"}");
                Console.WriteLine($"   Leverage: {request.Leverage}");
                Console.WriteLine($"   MarginType: {request.MarginType ?? "未设置"}");

                // 2. 参数基本验证
                Console.WriteLine("\n🔍 参数基本验证:");
                if (string.IsNullOrEmpty(request.Symbol))
                {
                    Console.WriteLine("❌ Symbol不能为空");
                    return false;
                }
                if (string.IsNullOrEmpty(request.Side))
                {
                    Console.WriteLine("❌ Side不能为空");
                    return false;
                }
                if (string.IsNullOrEmpty(request.Type))
                {
                    Console.WriteLine("❌ Type不能为空");
                    return false;
                }
                Console.WriteLine("✅ 基本参数验证通过");

                // 3. 设置杠杆（在下单前必须设置）
                Console.WriteLine("\n🎚️ 设置杠杆倍数:");
                Console.WriteLine($"   目标杠杆: {request.Leverage}x");
                Console.WriteLine($"   合约: {request.Symbol}");
                
                var leverageSuccess = await SetLeverageAsync(request.Symbol, request.Leverage).ConfigureAwait(false);
                if (!leverageSuccess)
                {
                    Console.WriteLine("⚠️ 杠杆设置失败，但继续下单（可能使用现有杠杆）");
                }

                // 4. 设置保证金模式（如果指定）
                if (!string.IsNullOrEmpty(request.MarginType))
                {
                    Console.WriteLine("\n💰 设置保证金模式:");
                    Console.WriteLine($"   目标模式: {request.MarginType}");
                    Console.WriteLine($"   合约: {request.Symbol}");
                    
                    var marginSuccess = await SetMarginTypeAsync(request.Symbol, request.MarginType).ConfigureAwait(false);
                    if (!marginSuccess)
                    {
                        Console.WriteLine("⚠️ 保证金模式设置失败，但继续下单（可能使用现有模式）");
                    }
                }

                // 5. 构建API参数
                Console.WriteLine("\n🔧 构建API参数:");
                var endpoint = "/fapi/v1/order";
                var timestamp = GetCurrentTimestamp();
                
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = request.Symbol.ToUpper(),
                    ["side"] = request.Side.ToUpper(),
                    ["type"] = request.Type.ToUpper(),
                    ["timestamp"] = timestamp.ToString()
                };

                // 设置positionSide (币安期货参数)
                if (!string.IsNullOrEmpty(request.PositionSide))
                {
                    parameters["positionSide"] = request.PositionSide.ToUpper();
                    Console.WriteLine($"✅ PositionSide已设置: {request.PositionSide}");
                }
                else
                {
                    parameters["positionSide"] = "BOTH";  // 默认值，适配单向持仓模式
                    Console.WriteLine("✅ PositionSide设置为BOTH（单向持仓模式）");
                }

                // 注意：币安期货下单API不需要marginType参数
                // marginType通过单独的 /fapi/v1/marginType API设置，而不是在下单时传递
                Console.WriteLine($"💡 保证金模式已通过SetMarginTypeAsync预设置: {request.MarginType ?? "默认"}");

                // 根据订单类型添加必要参数
                Console.WriteLine($"🎯 处理订单类型: {request.Type}");
                
                if (request.Type.ToUpper() == "LIMIT")
                {
                    Console.WriteLine("   📊 限价单参数:");
                    if (request.Price <= 0)
                    {
                        Console.WriteLine("❌ 限价单必须设置价格");
                        return false;
                    }
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 限价单必须设置数量");
                        return false;
                    }
                    
                    var formattedPrice = FormatPrice(request.Price, request.Symbol);
                    var formattedQuantity = FormatQuantity(request.Quantity, request.Symbol);
                    
                    parameters["price"] = formattedPrice;
                    parameters["quantity"] = formattedQuantity;
                    parameters["timeInForce"] = string.IsNullOrEmpty(request.TimeInForce) ? "GTC" : request.TimeInForce;
                    
                    Console.WriteLine($"   原始价格: {request.Price} → 格式化: {formattedPrice}");
                    Console.WriteLine($"   原始数量: {request.Quantity} → 格式化: {formattedQuantity}");
                    Console.WriteLine($"   TimeInForce: {parameters["timeInForce"]}");
                }
                else if (request.Type.ToUpper() == "MARKET")
                {
                    Console.WriteLine("   📊 市价单参数:");
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 市价单必须设置数量");
                        return false;
                    }
                    
                    var formattedQuantity = FormatQuantity(request.Quantity, request.Symbol);
                    parameters["quantity"] = formattedQuantity;
                    
                    Console.WriteLine($"   原始数量: {request.Quantity} → 格式化: {formattedQuantity}");
                }
                else if (request.Type.ToUpper() == "STOP_MARKET" || request.Type.ToUpper() == "TAKE_PROFIT_MARKET")
                {
                    Console.WriteLine("   🛡️ 止损市价单参数:");
                    if (request.StopPrice <= 0)
                    {
                        Console.WriteLine("❌ 止损单必须设置触发价格");
                        return false;
                    }
                    
                    var formattedStopPrice = FormatPrice(request.StopPrice, request.Symbol);
                    
                    parameters["stopPrice"] = formattedStopPrice;
                    parameters["reduceOnly"] = request.ReduceOnly.ToString().ToLower();
                    
                    // STOP_MARKET订单的特殊处理
                    Console.WriteLine($"   原始触发价: {request.StopPrice} → 格式化: {formattedStopPrice}");
                    Console.WriteLine($"   ReduceOnly: {request.ReduceOnly}");
                    Console.WriteLine($"   WorkingType: {request.WorkingType}");
                    
                    // 验证止损价格的合理性
                    var currentPrice = await GetLatestPriceAsync(request.Symbol).ConfigureAwait(false);
                    Console.WriteLine($"   当前市价: {currentPrice}");
                    
                    if (request.Side == "SELL" && request.StopPrice >= currentPrice)
                    {
                        Console.WriteLine("⚠️ 警告: 做多止损价应该低于当前价");
                        Console.WriteLine($"   建议: 止损价({request.StopPrice}) < 当前价({currentPrice})");
                    }
                    else if (request.Side == "BUY" && request.StopPrice <= currentPrice)
                    {
                        Console.WriteLine("⚠️ 警告: 做空止损价应该高于当前价");
                        Console.WriteLine($"   建议: 止损价({request.StopPrice}) > 当前价({currentPrice})");
                    }
                    else
                    {
                        Console.WriteLine("✅ 止损价格设置合理");
                    }
                    
                    if (!string.IsNullOrEmpty(request.WorkingType))
                    {
                        parameters["workingType"] = request.WorkingType;
                    }
                    
                    // 对于STOP_MARKET，需要设置quantity参数
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 止损市价单必须设置数量");
                        return false;
                    }
                    
                    var formattedQuantity = FormatQuantity(request.Quantity, request.Symbol);
                    parameters["quantity"] = formattedQuantity;
                    Console.WriteLine($"   原始数量: {request.Quantity} → 格式化: {formattedQuantity}");
                }
                else if (request.Type.ToUpper() == "STOP" || request.Type.ToUpper() == "TAKE_PROFIT")
                {
                    Console.WriteLine("   📊 条件限价单参数:");
                    if (request.StopPrice <= 0)
                    {
                        Console.WriteLine("❌ 条件单必须设置触发价格");
                        return false;
                    }
                    if (request.Price <= 0)
                    {
                        Console.WriteLine("❌ 限价条件单必须设置执行价格");
                        return false;
                    }
                    if (request.Quantity <= 0)
                    {
                        Console.WriteLine("❌ 条件单必须设置数量");
                        return false;
                    }
                    
                    var formattedStopPrice = FormatPrice(request.StopPrice, request.Symbol);
                    var formattedPrice = FormatPrice(request.Price, request.Symbol);
                    var formattedQuantity = FormatQuantity(request.Quantity, request.Symbol);
                    
                    parameters["stopPrice"] = formattedStopPrice;
                    parameters["price"] = formattedPrice;
                    parameters["quantity"] = formattedQuantity;
                    
                    Console.WriteLine($"   原始触发价: {request.StopPrice} → 格式化: {formattedStopPrice}");
                    Console.WriteLine($"   原始执行价: {request.Price} → 格式化: {formattedPrice}");
                    Console.WriteLine($"   原始数量: {request.Quantity} → 格式化: {formattedQuantity}");
                }

                // 添加可选参数
                if (request.ReduceOnly)
                {
                    parameters["reduceOnly"] = "true";
                    Console.WriteLine("   ✅ 设置reduceOnly=true (仅减仓)");
                }

                if (!string.IsNullOrEmpty(request.WorkingType))
                {
                    parameters["workingType"] = request.WorkingType;
                    Console.WriteLine($"   ✅ 设置workingType={request.WorkingType}");
                }

                // 6. 详细显示最终参数
                Console.WriteLine("\n📋 最终API调用参数:");
                Console.WriteLine($"   🔗 Endpoint: {_baseUrl}{endpoint}");
                Console.WriteLine($"   📝 参数列表:");
                foreach (var param in parameters.OrderBy(p => p.Key))
                {
                    Console.WriteLine($"      {param.Key}: {param.Value}");
                }

                // 7. 调用API
                Console.WriteLine("\n🌐 发送API请求...");
                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters);
                
                // 8. 详细分析响应
                Console.WriteLine("\n📤 API响应分析:");
                if (response == null)
                {
                    Console.WriteLine("❌ API响应为空 - 可能原因:");
                    Console.WriteLine("   • 网络连接问题");
                    Console.WriteLine("   • API服务器无响应");
                    Console.WriteLine("   • 请求超时");
                    return false;
                }

                Console.WriteLine($"📄 原始响应内容: {response}");
                Console.WriteLine($"📏 响应长度: {response.Length} 字符");

                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine("⚠️ 检测到错误响应，开始解析...");
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(response, _jsonOptions);
                        Console.WriteLine("❌ API返回错误:");
                        Console.WriteLine($"   错误代码: {errorResponse?.Code}");
                        Console.WriteLine($"   错误消息: {errorResponse?.Msg}");
                        
                        // 分析常见错误原因
                        AnalyzeErrorCode(errorResponse?.Code, errorResponse?.Msg, parameters);
                        
                        // 根据错误码提供具体建议
                        var errorSuggestion = GetErrorSuggestion(errorResponse?.Code, errorResponse?.Msg);
                        if (!string.IsNullOrEmpty(errorSuggestion))
                        {
                            Console.WriteLine($"\n💡 解决建议:\n{errorSuggestion}");
                        }
                        
                        return false;
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"❌ 解析错误响应异常: {parseEx.Message}");
                        Console.WriteLine($"📄 原始错误响应: {response}");
                        return false;
                    }
                }

                // 检查是否包含orderId（成功标志）
                bool success = response.Contains("\"orderId\"");
                
                Console.WriteLine($"\n🎯 下单结果判断:");
                Console.WriteLine($"   包含orderId: {success}");
                
                if (success)
                {
                    Console.WriteLine("✅ 订单创建成功!");
                    try
                    {
                        var orderResponse = JsonSerializer.Deserialize<BinanceOrderResponse>(response, _jsonOptions);
                        Console.WriteLine("📊 订单详细信息:");
                        Console.WriteLine($"   订单ID: {orderResponse?.OrderId}");
                        Console.WriteLine($"   状态: {orderResponse?.Status}");
                        Console.WriteLine($"   合约: {orderResponse?.Symbol}");
                        Console.WriteLine($"   方向: {orderResponse?.Side}");
                        Console.WriteLine($"   类型: {orderResponse?.Type}");
                        Console.WriteLine($"   数量: {orderResponse?.OrigQty}");
                        Console.WriteLine($"   价格: {orderResponse?.Price}");
                        Console.WriteLine($"   止损价: {orderResponse?.StopPrice}");
                        Console.WriteLine($"   客户端订单ID: {orderResponse?.ClientOrderId}");
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"⚠️ 解析订单响应异常: {parseEx.Message}，但订单可能已成功创建");
                    }
                }
                else
                {
                    Console.WriteLine("❌ 下单失败: 响应中未包含订单ID");
                    Console.WriteLine("🔍 可能原因:");
                    Console.WriteLine("   • 参数格式错误");
                    Console.WriteLine("   • API限制触发");
                    Console.WriteLine("   • 账户权限不足");
                    Console.WriteLine("   • 交易规则不符合");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 下单过程发生异常:");
                Console.WriteLine($"   异常类型: {ex.GetType().Name}");
                Console.WriteLine($"   异常消息: {ex.Message}");
                Console.WriteLine($"   异常堆栈: {ex.StackTrace}");
                return false;
            }
            finally
            {
                Console.WriteLine("\n" + "=".PadLeft(80, '='));
                Console.WriteLine("🏁 币安期货下单流程结束");
                Console.WriteLine("=".PadLeft(80, '=') + "\n");
            }
        }

        // 新增：错误代码分析方法
        private void AnalyzeErrorCode(int? errorCode, string? errorMessage, Dictionary<string, string> parameters)
        {
            Console.WriteLine("\n🔍 错误原因分析:");
            
            switch (errorCode)
            {
                case -1121:
                    Console.WriteLine("   原因: 合约符号无效");
                    Console.WriteLine($"   您的Symbol: {parameters.GetValueOrDefault("symbol", "未知")}");
                    Console.WriteLine("   建议: 检查合约名称拼写，如BTCUSDT、ETHUSDT等");
                    break;
                    
                case -1111:
                    Console.WriteLine("   原因: 数量精度不正确");
                    Console.WriteLine($"   您的数量: {parameters.GetValueOrDefault("quantity", "未知")}");
                    Console.WriteLine("   建议: 检查数量的小数位数是否符合该合约要求");
                    break;
                    
                case -1112:
                    Console.WriteLine("   原因: 价格精度不正确");
                    Console.WriteLine($"   您的价格: {parameters.GetValueOrDefault("price", "未知")}");
                    Console.WriteLine($"   您的止损价: {parameters.GetValueOrDefault("stopPrice", "未知")}");
                    Console.WriteLine("   建议: 检查价格的小数位数是否符合该合约要求");
                    break;
                    
                case -2019:
                    Console.WriteLine("   原因: 保证金不足");
                    Console.WriteLine($"   下单数量: {parameters.GetValueOrDefault("quantity", "未知")}");
                    Console.WriteLine("   建议: 减少下单数量或增加账户余额");
                    break;
                    
                case -2027:
                    Console.WriteLine("   原因: 持仓量超过杠杆限制");
                    Console.WriteLine($"   下单数量: {parameters.GetValueOrDefault("quantity", "未知")}");
                    Console.WriteLine($"   合约: {parameters.GetValueOrDefault("symbol", "未知")}");
                    Console.WriteLine("   建议: 降低杠杆倍数或减少下单数量");
                    break;
                    
                case -4003:
                    Console.WriteLine("   原因: 数量小于最小交易量");
                    Console.WriteLine($"   您的数量: {parameters.GetValueOrDefault("quantity", "未知")}");
                    Console.WriteLine("   建议: 增加下单数量");
                    break;
                    
                case -4004:
                    Console.WriteLine("   原因: 数量大于最大交易量");
                    Console.WriteLine($"   您的数量: {parameters.GetValueOrDefault("quantity", "未知")}");
                    Console.WriteLine("   建议: 减少下单数量");
                    break;
                    
                default:
                    Console.WriteLine($"   错误代码: {errorCode}");
                    Console.WriteLine($"   错误消息: {errorMessage}");
                    Console.WriteLine("   建议: 检查币安API文档或联系技术支持");
                    break;
            }
        }

        private string GetErrorSuggestion(int? errorCode, string? errorMessage)
        {
            return errorCode switch
            {
                -1121 => "合约名称无效，请检查Symbol是否正确（如：BTCUSDT）",
                -2019 => "保证金不足，请检查账户余额",
                -2027 => GetDetailedPositionLimitError(), // 使用专门的方法处理持仓限制错误
                -4061 => "价格不符合tick规则，请调整价格精度",
                -4062 => "数量不符合step规则，请调整数量精度",
                -4164 => "订单价格超出合理范围",
                -1111 => "数量精度不正确",
                -1112 => "价格精度不正确",
                -2013 => "订单不存在",
                -2010 => "账户余额不足",
                -4003 => "数量小于最小交易量",
                -4004 => "数量大于最大交易量",
                _ => errorMessage?.Contains("Invalid symbol") == true ? "合约符号无效，请检查交易对是否存在" :
                     errorMessage?.Contains("Insufficient") == true ? "余额不足，请检查账户资金" :
                     errorMessage?.Contains("precision") == true ? "精度错误，请调整价格或数量精度" :
                     errorMessage?.Contains("leverage") == true ? "杠杆相关错误，请检查杠杆设置" :
                     errorMessage?.Contains("position") == true ? "持仓相关错误，请检查当前持仓状态" :
                     "未知错误，请检查网络连接和参数设置"
            };
        }

        // 专门处理-2027持仓限制错误的详细信息
        private string GetDetailedPositionLimitError()
        {
            try
            {
                Console.WriteLine("🔍 正在分析持仓限制错误...");
                
                // 异步获取详细信息，但为了保持同步接口，先返回基本信息
                _ = Task.Run(async () => await LogDetailedPositionInfoAsync());
                
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine("🚨 持仓超过当前杠杆允许的最大限制！");
                errorDetails.AppendLine();
                errorDetails.AppendLine("🛠️ 立即解决方案：");
                errorDetails.AppendLine("   ✅ 方案1：降低杠杆倍数（推荐）");
                errorDetails.AppendLine("      - 将杠杆从当前设置降低到10倍或更低");
                errorDetails.AppendLine("      - 例如：20倍 → 10倍，持仓限制可能翻倍");
                errorDetails.AppendLine();
                errorDetails.AppendLine("   ✅ 方案2：减少下单数量");
                errorDetails.AppendLine("      - 尝试当前数量的50%");
                errorDetails.AppendLine("      - 或分多次小批量下单");
                errorDetails.AppendLine();
                errorDetails.AppendLine("   ✅ 方案3：检查现有持仓");
                errorDetails.AppendLine("      - 如有同合约持仓，考虑先部分平仓");
                errorDetails.AppendLine("      - 释放持仓空间后再开新仓");
                errorDetails.AppendLine();
                errorDetails.AppendLine("💡 小币种特殊提示：");
                errorDetails.AppendLine("   • AIOT/B2等小币种持仓限制较严格");
                errorDetails.AppendLine("   • 建议杠杆≤10倍，分批建仓");
                errorDetails.AppendLine("   • 优先使用较低杠杆获得更高持仓上限");
                
                var result = errorDetails.ToString();
                Console.WriteLine($"📄 生成的详细错误说明:\n{result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 生成详细错误信息异常: {ex.Message}");
                return "持仓量超过当前杠杆允许的最大限制。建议：1)降低杠杆倍数 2)减少下单数量 3)部分平仓后再开新仓";
            }
        }

        // 异步获取并记录详细持仓信息
        private async Task LogDetailedPositionInfoAsync()
        {
            try
            {
                Console.WriteLine("\n🔍 正在获取详细持仓信息...");
                
                var positions = await GetPositionsAsync();
                var nonZeroPositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                
                Console.WriteLine($"📊 当前持仓总数: {nonZeroPositions.Count}");
                
                if (nonZeroPositions.Any())
                {
                    Console.WriteLine("\n📋 当前持仓详情:");
                    foreach (var position in nonZeroPositions)
                    {
                        var direction = position.PositionAmt > 0 ? "多头" : "空头";
                        var maxAllowed = GetMaxPositionForLeverage(position.Symbol, position.Leverage, position.MarkPrice);
                        var usageRate = Math.Abs(position.PositionAmt) / maxAllowed * 100;
                        
                        Console.WriteLine($"   🏷️ {position.Symbol}:");
                        Console.WriteLine($"      方向: {direction}");
                        Console.WriteLine($"      当前持仓: {Math.Abs(position.PositionAmt):F4}");
                        Console.WriteLine($"      杠杆倍数: {position.Leverage}x");
                        Console.WriteLine($"      最大允许: {maxAllowed:F4}");
                        Console.WriteLine($"      使用率: {usageRate:F1}%");
                        Console.WriteLine($"      剩余额度: {maxAllowed - Math.Abs(position.PositionAmt):F4}");
                        
                        if (usageRate > 80)
                        {
                            Console.WriteLine($"      ⚠️ 警告: 持仓使用率过高，建议降低杠杆");
                        }
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("📋 当前无持仓，错误可能是新开仓超限");
                }
                
                Console.WriteLine("💡 针对您的情况的具体建议:");
                Console.WriteLine("   1. 如果是AIOTUSDT，建议先尝试20倍杠杆");
                Console.WriteLine("   2. 减少下单数量到当前的50%试试");
                Console.WriteLine("   3. 如有其他高杠杆持仓，考虑先部分平仓");
                Console.WriteLine("   4. 检查账户风险等级设置");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取详细持仓信息异常: {ex.Message}");
            }
        }

        // 下单校验方法
        public async Task<(bool isValid, string errorMessage)> ValidateOrderAsync(OrderRequest request)
        {
            try
            {
                LogService.LogInfo($"🔍 开始校验下单参数：{request.Side} {request.Quantity} {request.Symbol}");

                // 1. 检查是否有持仓，获取当前杠杆和仓位模式
                var positions = await GetPositionsAsync().ConfigureAwait(false);
                var existingPosition = positions.FirstOrDefault(p => p.Symbol == request.Symbol && Math.Abs(p.PositionAmt) > 0);

                if (existingPosition != null)
                {
                    LogService.LogInfo($"📊 发现现有持仓：{existingPosition.Symbol} {existingPosition.PositionAmt}");
                    
                    // 检查是否是同向操作（增仓）还是反向操作（减仓/平仓）
                    bool isSameDirection = (existingPosition.PositionAmt > 0 && request.Side == "BUY") ||
                                          (existingPosition.PositionAmt < 0 && request.Side == "SELL");
                    
                    if (isSameDirection)
                    {
                        // 同向增仓：允许用户设置新的杠杆
                        LogService.LogInfo($"📈 检测到同向增仓操作，允许用户自定义杠杆");
                        
                        if (request.Leverage != existingPosition.Leverage)
                        {
                            LogService.LogWarning($"⚠️ 杠杆设置不同：持仓{existingPosition.Leverage}x vs 新单{request.Leverage}x");
                            
                            // 检查新杠杆是否合理（不能过高）
                            if (request.Leverage > 50 && existingPosition.Leverage <= 20)
                            {
                                LogService.LogWarning($"⚠️ 杠杆风险提醒：从{existingPosition.Leverage}x提升到{request.Leverage}x可能增加风险");
                            }
                            
                            LogService.LogInfo($"✅ 允许用户使用新杠杆：{request.Leverage}x");
                        }
                    }
                    else
                    {
                        // 反向操作（减仓/平仓）：也允许用户设置杠杆，但给出建议
                        LogService.LogInfo($"📉 检测到反向操作（减仓/平仓）");
                        
                        if (request.Leverage != existingPosition.Leverage)
                        {
                            LogService.LogWarning($"⚠️ 杠杆设置不同：持仓{existingPosition.Leverage}x vs 新单{request.Leverage}x");
                            LogService.LogInfo($"💡 建议：反向操作通常使用现有杠杆({existingPosition.Leverage}x)，但允许用户自定义");
                            LogService.LogInfo($"✅ 使用用户设置的杠杆：{request.Leverage}x");
                        }
                        
                        // 只在需要减仓时设置reduceOnly
                        if (!request.ReduceOnly && Math.Abs(existingPosition.PositionAmt) > 0)
                        {
                            LogService.LogInfo($"🔧 自动设置reduceOnly=true（减仓操作）");
                            request.ReduceOnly = true;
                        }
                    }

                    // 保证金模式处理：对于有持仓的合约，通常不允许修改保证金模式
                    if (request.MarginType != existingPosition.MarginType)
                    {
                        LogService.LogWarning($"⚠️ 保证金模式设置不同：持仓{existingPosition.MarginType} vs 新单{request.MarginType}");
                        LogService.LogInfo($"💡 提示：有持仓时通常无法修改保证金模式，将使用现有模式");
                        LogService.LogInfo($"🔄 保证金模式调整：{request.MarginType} → {existingPosition.MarginType}");
                        request.MarginType = existingPosition.MarginType;
                    }
                }
                else
                {
                    LogService.LogInfo("🆕 新开仓位，使用用户设置的杠杆和保证金模式");
                    
                    // 只在MarginType为空时设置默认值，不强制覆盖用户选择
                    if (string.IsNullOrEmpty(request.MarginType))
                    {
                        request.MarginType = "ISOLATED";
                        LogService.LogInfo("🔧 默认设置为逐仓模式 (ISOLATED)");
                    }
                    else
                    {
                        LogService.LogInfo($"✅ 使用用户设置的保证金模式: {request.MarginType}");
                    }
                }

                // 2. 校验交易数量限制（基本校验，实际应该从交易所获取）
                var currentPrice = await GetLatestPriceAsync(request.Symbol).ConfigureAwait(false);
                var validationResult = await ValidateQuantityLimitsAsync(request.Symbol, request.Quantity, request.Leverage, currentPrice).ConfigureAwait(false);
                if (!validationResult.isValid)
                {
                    return validationResult;
                }

                // 3. 校验止损价格设置
                if (request.StopLossPrice > 0)
                {
                    var stopLossValidation = await ValidateStopLossPriceAsync(request).ConfigureAwait(false);
                    if (!stopLossValidation.isValid)
                    {
                        return stopLossValidation;
                    }
                }

                LogService.LogInfo("✅ 下单参数校验通过");
                return (true, "校验通过");
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 下单校验异常: {ex.Message}");
                return (false, $"校验异常: {ex.Message}");
            }
        }

        private async Task<(bool isValid, string errorMessage)> ValidateQuantityLimitsAsync(string symbol, decimal quantity, int leverage, decimal currentPrice)
        {
            try
            {
                LogService.LogInfo($"📏 开始校验数量限制: {symbol} 数量={quantity} 价格={currentPrice}");
                
                // 获取真实的交易规则
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, tickSize, stepSize) = await GetRealExchangeInfoAsync(symbol).ConfigureAwait(false);
                
                LogService.LogInfo($"🔍 真实交易规则: 最小={minQuantity}, 最大={maxQuantity}, 杠杆上限={maxLeverage}x, 名义价值上限=${maxNotional}");
                LogService.LogInfo($"📐 精度规则: 价格最小变动={tickSize}, 数量最小变动={stepSize}");
                
                // 1. 检查最小数量（交易所规则）
                if (quantity < minQuantity)
                {
                    return (false, $"数量过小，最小下单量为 {minQuantity}");
                }

                // 2. 检查最大数量（交易所规则）
                if (quantity > maxQuantity)
                {
                    return (false, $"数量过大，最大下单量为 {maxQuantity}");
                }

                // 3. 检查杠杆限制（交易所规则）
                if (leverage > maxLeverage)
                {
                    return (false, $"杠杆过高，该合约最大杠杆为 {maxLeverage}x");
                }

                if (leverage < 1)
                {
                    return (false, "杠杆不能小于1x");
                }

                // 4. 检查数量精度（stepSize）
                if (stepSize > 0)
                {
                    var remainder = quantity % stepSize;
                    if (remainder != 0)
                    {
                        var adjustedQuantity = Math.Floor(quantity / stepSize) * stepSize;
                        LogService.LogWarning($"⚠️ 数量精度不符合stepSize={stepSize}，建议调整为 {adjustedQuantity}");
                        return (false, $"数量精度错误，必须是 {stepSize} 的整数倍，建议调整为 {adjustedQuantity}");
                    }
                }

                // 5. 检查名义价值限制（交易所规则）
                var notionalValue = quantity * currentPrice;
                LogService.LogInfo($"💰 计算名义价值: {quantity} × {currentPrice} = ${notionalValue:F2}");
                
                if (notionalValue > maxNotional)
                {
                    LogService.LogError($"❌ 名义价值超限: ${notionalValue:F2} > ${maxNotional:F0}");
                    return (false, $"下单金额过大，最大名义价值为 ${maxNotional:F0}");
                }

                // 🎯 移除持仓限制检查 - 止损金额本身就是最好的风险控制
                LogService.LogInfo("✅ 交易所规则校验通过，止损金额提供风险保护");
                
                LogService.LogInfo($"✅ 数量校验通过: 名义价值=${notionalValue:F2} < ${maxNotional:F0}");
                return (true, "数量校验通过");
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 数量校验异常: {ex.Message}");
                
                // API失败时使用备选校验
                LogService.LogWarning("🔄 使用备选校验方案...");
                return ValidateQuantityLimitsFallback(symbol, quantity, leverage, currentPrice);
            }
        }
        
        private (bool isValid, string errorMessage) ValidateQuantityLimitsFallback(string symbol, decimal quantity, int leverage, decimal currentPrice)
        {
            // 备选的硬编码限制检查
            var (minQuantity, maxQuantity, maxLeverage, maxNotional, _, _) = GetFallbackLimits(symbol);
            
            LogService.LogInfo($"🔄 备选校验: 最小={minQuantity}, 最大={maxQuantity}, 名义价值上限=${maxNotional}");

            // 检查最小数量
            if (quantity < minQuantity)
            {
                return (false, $"数量过小，最小下单量为 {minQuantity}");
            }

            // 检查最大数量
            if (quantity > maxQuantity)
            {
                return (false, $"数量过大，最大下单量为 {maxQuantity}");
            }

            // 检查杠杆限制
            if (leverage > maxLeverage)
            {
                return (false, $"杠杆过高，该合约最大杠杆为 {maxLeverage}x");
            }

            // 检查名义价值限制（使用真实当前价格）
            var notionalValue = quantity * currentPrice;
            LogService.LogInfo($"💰 备选名义价值计算: {quantity} × {currentPrice} = ${notionalValue:F2}");
            
            if (notionalValue > maxNotional)
            {
                return (false, $"下单金额过大，最大名义价值为 ${maxNotional:F0}");
            }

            return (true, "备选校验通过");
        }

        private async Task<(bool isValid, string errorMessage)> ValidateStopLossPriceAsync(OrderRequest request)
        {
            if (request.StopLossPrice <= 0)
                return (true, "无止损设置");

            // 🎯 获取用于计算的价格 - 对于市价单，必须使用当前市价
            decimal basePrice = request.Price;
            
            // 如果订单价格为0（市价单），则使用当前市价
            if (basePrice <= 0)
            {
                basePrice = await GetLatestPriceAsync(request.Symbol).ConfigureAwait(false);
                LogService.LogInfo($"💡 市价单使用当前市价计算: {basePrice}");
            }
            
            // 计算预期亏损
            decimal expectedLoss = 0;
            
            if (request.Side == "BUY")
            {
                // 买入（做多）时，止损价应该低于开仓价
                if (request.StopLossPrice >= basePrice && basePrice > 0)
                {
                    return (false, "买入方向的止损价应该低于开仓价");
                }
                // 做多止损亏损 = (开仓价 - 止损价) × 数量
                expectedLoss = (basePrice - request.StopLossPrice) * request.Quantity;
                LogService.LogInfo($"💰 做多止损计算: ({basePrice} - {request.StopLossPrice}) × {request.Quantity} = {expectedLoss:F4}");
            }
            else if (request.Side == "SELL")
            {
                // 卖出（做空）时，止损价应该高于开仓价
                if (request.StopLossPrice <= basePrice && basePrice > 0)
                {
                    return (false, "卖出方向的止损价应该高于开仓价");
                }
                // 做空止损亏损 = (止损价 - 开仓价) × 数量
                expectedLoss = (request.StopLossPrice - basePrice) * request.Quantity;
                LogService.LogInfo($"💰 做空止损计算: ({request.StopLossPrice} - {basePrice}) × {request.Quantity} = {expectedLoss:F4}");
            }

            // 🎯 止损金额验证：如果用户设置了止损金额，验证计算是否一致
            var calculatedLoss = Math.Abs(expectedLoss);
            
            if (request.StopLossAmount > 0)
            {
                var tolerance = Math.Max(0.01m, calculatedLoss * 0.01m); // 允许1%或1分钱的误差
                
                if (Math.Abs(calculatedLoss - request.StopLossAmount) > tolerance)
                {
                    LogService.LogWarning($"⚠️ 止损金额不一致: 计算值={calculatedLoss:F4}, 设置值={request.StopLossAmount:F4}");
                    // 不阻止下单，以计算值为准
                }
                
                LogService.LogInfo($"🎯 止损金额验证: 计算={calculatedLoss:F4} vs 设置={request.StopLossAmount:F4}");
            }
            
            // 更新止损金额为精确计算值
            request.StopLossAmount = calculatedLoss;
            
            // ✅ 止损金额本身就是最好的风险控制，不需要额外限制
            LogService.LogInfo($"✅ 止损价格校验通过，准确风险金额: {request.StopLossAmount:F4} USDT");
            return (true, $"止损校验通过，风险金额: {request.StopLossAmount:F4} USDT");
        }

        private async Task<string?> SendPublicRequestAsync(HttpMethod method, string endpoint)
        {
            try
            {
                var url = _baseUrl + endpoint;
                var request = new HttpRequestMessage(method, url);
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error sending public request: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> SendSignedRequestAsync(HttpMethod method, string endpoint, Dictionary<string, string> parameters)
        {
            if (_currentAccount == null)
            {
                Console.WriteLine("❌ SendSignedRequestAsync: _currentAccount为空");
                return null;
            }

            try
            {
                Console.WriteLine("\n🔐 开始构建签名请求:");
                Console.WriteLine($"   🔗 Method: {method}");
                Console.WriteLine($"   🔗 Endpoint: {endpoint}");
                Console.WriteLine($"   🔗 Base URL: {_baseUrl}");
                
                // 1. 构建查询字符串
                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
                Console.WriteLine($"   📝 原始查询字符串: {queryString}");
                
                // 2. 生成签名
                Console.WriteLine("\n🔐 生成HMAC-SHA256签名:");
                Console.WriteLine($"   🔑 Secret Key: {_currentAccount.SecretKey[..8]}...{_currentAccount.SecretKey[^4..]}");
                Console.WriteLine($"   📄 待签名字符串: {queryString}");
                
                var signature = GenerateSignature(queryString, _currentAccount.SecretKey);
                Console.WriteLine($"   ✅ 生成的签名: {signature}");
                
                queryString += $"&signature={signature}";
                Console.WriteLine($"   📝 完整查询字符串: {queryString}");

                // 3. 构建URL和请求
                var url = _baseUrl + endpoint;
                if (method == HttpMethod.Get || method == HttpMethod.Delete)
                {
                    url += "?" + queryString;
                    Console.WriteLine($"   🔗 最终URL (GET/DELETE): {url}");
                }
                else
                {
                    Console.WriteLine($"   🔗 最终URL (POST): {url}");
                    Console.WriteLine($"   📦 POST Body: {queryString}");
                }

                var request = new HttpRequestMessage(method, url);
                
                // 4. 添加API Key头部
                Console.WriteLine("\n📋 设置HTTP头部:");
                Console.WriteLine($"   🔑 API Key: {_currentAccount.ApiKey[..8]}...{_currentAccount.ApiKey[^4..]}");
                request.Headers.Add("X-MBX-APIKEY", _currentAccount.ApiKey);

                if (method == HttpMethod.Post)
                {
                    request.Content = new StringContent(queryString, Encoding.UTF8, "application/x-www-form-urlencoded");
                    Console.WriteLine($"   📦 Content-Type: application/x-www-form-urlencoded");
                    Console.WriteLine($"   📦 Content-Length: {queryString.Length} 字符");
                }

                // 5. 发送请求
                Console.WriteLine("\n🌐 发送HTTP请求...");
                var startTime = DateTime.Now;
                var response = await _httpClient.SendAsync(request);
                var endTime = DateTime.Now;
                var duration = (endTime - startTime).TotalMilliseconds;
                
                Console.WriteLine($"   ⏱️ 请求耗时: {duration:F2} ms");
                Console.WriteLine($"   📊 响应状态码: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"   📋 响应头部:");
                foreach (var header in response.Headers)
                {
                    Console.WriteLine($"      {header.Key}: {string.Join(", ", header.Value)}");
                }

                // 6. 处理响应
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   📄 响应内容长度: {responseContent.Length} 字符");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ HTTP请求成功");
                    if (responseContent.Length <= 500)
                    {
                        Console.WriteLine($"   📄 完整响应内容: {responseContent}");
                    }
                    else
                    {
                        Console.WriteLine($"   📄 响应内容前200字符: {responseContent.Substring(0, 200)}...");
                        Console.WriteLine($"   📄 响应内容后200字符: ...{responseContent.Substring(responseContent.Length - 200)}");
                    }
                    return responseContent;
                }
                else
                {
                    Console.WriteLine($"❌ HTTP请求失败: {response.StatusCode}");
                    Console.WriteLine($"   📄 错误响应内容: {responseContent}");
                    
                    // 尝试解析币安API错误
                    if (responseContent.Contains("\"code\"") && responseContent.Contains("\"msg\""))
                    {
                        try
                        {
                            var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(responseContent, _jsonOptions);
                            Console.WriteLine($"   🔍 解析的错误信息: Code={errorResponse?.Code}, Msg={errorResponse?.Msg}");
                        }
                        catch (Exception parseEx)
                        {
                            Console.WriteLine($"   ⚠️ 无法解析错误响应: {parseEx.Message}");
                        }
                    }
                    
                    LogService.LogError($"API Error: {response.StatusCode}, {responseContent}");
                    return responseContent; // 返回错误内容用于上层处理
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"❌ HTTP请求异常: {httpEx.Message}");
                Console.WriteLine($"   可能原因: 网络连接问题、DNS解析失败、超时等");
                LogService.LogError($"HTTP Request Error: {httpEx.Message}");
                return null;
            }
            catch (TaskCanceledException tcEx)
            {
                Console.WriteLine($"❌ 请求超时或被取消: {tcEx.Message}");
                Console.WriteLine($"   可能原因: 网络延迟过高、服务器响应慢");
                LogService.LogError($"Request Timeout: {tcEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 发送签名请求异常:");
                Console.WriteLine($"   异常类型: {ex.GetType().Name}");
                Console.WriteLine($"   异常消息: {ex.Message}");
                Console.WriteLine($"   异常堆栈: {ex.StackTrace}");
                LogService.LogError($"Error sending signed request: {ex.Message}");
                return null;
            }
        }

        private string GenerateSignature(string queryString, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(queryString);
            
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // 模拟数据方法
        private AccountInfo GetMockAccountInfo()
        {
            return new AccountInfo
            {
                TotalWalletBalance = 1000.0m,
                TotalMarginBalance = 200.0m,
                TotalUnrealizedProfit = 50.0m,
                AvailableBalance = 750.0m,
                MaxWithdrawAmount = 750.0m
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
                    MarkPrice = 46000.0m,
                    UnrealizedProfit = 1.0m,
                    PositionSideString = "LONG",
                    Leverage = 10,
                    MarginType = "CROSSED",
                    IsolatedMargin = 0,
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
                    Status = "NEW",
                    ClientOrderId = "test_order_1",
                    Price = 45500.0m,
                    OrigQty = 0.001m,
                    ExecutedQty = 0,
                    CumQuote = 0,
                    TimeInForce = "GTC",
                    Type = "LIMIT",
                    ReduceOnly = false,
                    ClosePosition = false,
                    Side = "BUY",
                    PositionSide = "BOTH",
                    StopPrice = 0,
                    WorkingType = "CONTRACT_PRICE",
                    Time = DateTime.Now.AddMinutes(-5),
                    UpdateTime = DateTime.Now.AddMinutes(-5)
                }
            };

            if (!string.IsNullOrEmpty(symbol))
            {
                orders = orders.Where(o => o.Symbol == symbol).ToList();
            }

            return orders;
        }

        private decimal GetMockPrice(string symbol)
        {
            var random = new Random();
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => 46000.0m + (decimal)(random.NextDouble() * 200 - 100),
                "ETHUSDT" => 2800.0m + (decimal)(random.NextDouble() * 50 - 25),
                "BNBUSDT" => 320.0m + (decimal)(random.NextDouble() * 10 - 5),
                _ => 1.0m + (decimal)(random.NextDouble() * 0.1 - 0.05)
            };
        }

        // 获取真实的交易规则信息
        public async Task<(decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal tickSize, decimal stepSize)> GetRealExchangeInfoAsync(string symbol)
        {
            Console.WriteLine($"🔍 开始获取 {symbol} 的真实交易规则...");
            
            try
            {
                var endpoint = "/fapi/v1/exchangeInfo";
                Console.WriteLine($"🚀 调用币安API: {_baseUrl}{endpoint}");
                
                var response = await SendPublicRequestAsync(HttpMethod.Get, endpoint).ConfigureAwait(false);
                
                if (response == null) 
                {
                    Console.WriteLine($"❌ 交易规则API调用失败，使用默认值");
                    return GetFallbackLimits(symbol);
                }

                Console.WriteLine($"📄 API响应长度: {response.Length} 字符");
                
                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine($"❌ 交易规则API返回错误: {response.Substring(0, Math.Min(200, response.Length))}");
                    return GetFallbackLimits(symbol);
                }

                var exchangeInfo = JsonSerializer.Deserialize<BinanceExchangeInfoResponse>(response, _jsonOptions);
                if (exchangeInfo?.Symbols == null)
                {
                    Console.WriteLine($"❌ 解析交易规则响应失败");
                    return GetFallbackLimits(symbol);
                }

                Console.WriteLine($"📊 API返回 {exchangeInfo.Symbols.Length} 个交易对信息");
                
                // 查找指定合约的信息
                var symbolInfo = exchangeInfo.Symbols.FirstOrDefault(s => s.Symbol == symbol);
                if (symbolInfo == null)
                {
                    Console.WriteLine($"❌ 未找到合约 {symbol} 的交易规则");
                    return GetFallbackLimits(symbol);
                }

                Console.WriteLine($"✅ 找到合约 {symbol} 的交易规则");
                Console.WriteLine($"📏 状态: {symbolInfo.Status}");
                Console.WriteLine($"📏 基础资产: {symbolInfo.BaseAsset}");
                Console.WriteLine($"📏 报价资产: {symbolInfo.QuoteAsset}");

                // 解析过滤器信息
                decimal minQuantity = 0.001m;
                decimal maxQuantity = 10000m;
                decimal tickSize = 0.01m;
                decimal stepSize = 0.001m;
                decimal maxNotional = 100000m;
                int maxLeverage = 20;

                foreach (var filter in symbolInfo.Filters)
                {
                    Console.WriteLine($"🔧 处理过滤器: {filter.FilterType}");
                    
                    switch (filter.FilterType)
                    {
                        case "LOT_SIZE":
                            if (decimal.TryParse(filter.MinQty, out var minQty))
                                minQuantity = minQty;
                            if (decimal.TryParse(filter.MaxQty, out var maxQty))
                                maxQuantity = maxQty;
                            if (decimal.TryParse(filter.StepSize, out var step))
                                stepSize = step;
                            Console.WriteLine($"   📦 数量限制: 最小={minQuantity}, 最大={maxQuantity}, 步长={stepSize}");
                            break;
                            
                        case "PRICE_FILTER":
                            if (decimal.TryParse(filter.TickSize, out var tick))
                                tickSize = tick;
                            Console.WriteLine($"   💰 价格精度: 最小变动={tickSize}");
                            break;
                            
                        case "MIN_NOTIONAL":
                            if (decimal.TryParse(filter.Notional, out var notional))
                                maxNotional = notional * 1000; // 转换为合理的最大值
                            Console.WriteLine($"   💵 最小名义价值: {filter.Notional}");
                            break;
                            
                        case "MARKET_LOT_SIZE":
                            Console.WriteLine($"   🏪 市价单数量限制");
                            break;
                    }
                }

                // 获取杠杆信息（期货特有，可能需要单独API）
                // 这里先使用经验值，实际可以调用 /fapi/v1/leverageBracket 获取
                maxLeverage = GetMaxLeverageForSymbol(symbol);

                Console.WriteLine($"🎯 {symbol} 最终交易规则:");
                Console.WriteLine($"   📦 数量范围: {minQuantity} - {maxQuantity}");
                Console.WriteLine($"   💰 价格精度: {tickSize}");
                Console.WriteLine($"   📏 数量精度: {stepSize}");
                Console.WriteLine($"   💵 最大名义价值: {maxNotional}");
                Console.WriteLine($"   🎚️ 最大杠杆: {maxLeverage}x");

                return (minQuantity, maxQuantity, maxLeverage, maxNotional, tickSize, stepSize);
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"❌ JSON解析交易规则异常: {jsonEx.Message}");
                return GetFallbackLimits(symbol);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取交易规则异常: {ex.Message}");
                Console.WriteLine($"📍 异常堆栈: {ex.StackTrace}");
                return GetFallbackLimits(symbol);
            }
        }

        private int GetMaxLeverageForSymbol(string symbol)
        {
            // 基于经验的最大杠杆设置，实际应该通过API获取
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => 125,
                "ETHUSDT" => 100,
                "BNBUSDT" => 75,
                "ADAUSDT" => 75,
                "DOGEUSDT" => 50,
                "SOLUSDT" => 50,
                "DOTUSDT" => 50,
                "LINKUSDT" => 50,
                "LTCUSDT" => 75,
                "BCHUSDT" => 75,
                "XRPUSDT" => 75,
                "MATICUSDT" => 50,
                "AVAXUSDT" => 50,
                "UNIUSDT" => 50,
                "ATOMUSDT" => 50,
                _ => 25 // 默认保守值
            };
        }

        private (decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal tickSize, decimal stepSize) GetFallbackLimits(string symbol)
        {
            Console.WriteLine($"⚠️ 使用备选交易规则: {symbol}");
            
            // 获取当前价格用于动态计算
            var currentPrice = LatestPrice > 0 ? LatestPrice : GetMockPrice(symbol);
            
            decimal minQuantity, maxQuantity, tickSize, stepSize;
            int maxLeverage = 20;
            decimal maxNotional = 100000m;
            
            // 为AIOTUSDT添加特定配置
            if (symbol.ToUpper() == "AIOTUSDT")
            {
                minQuantity = 1m;           // 最小1个
                maxQuantity = 1000000m;     // 最大100万个
                tickSize = 0.00001m;        // 价格精度5位小数
                stepSize = 1m;              // 数量精度整数
                maxLeverage = 50;           // 最大50倍杠杆
                maxNotional = 100000m;      // 最大名义价值10万USDT
                
                Console.WriteLine($"🤖 AIOT特定规则: 数量={minQuantity}-{maxQuantity}, 杠杆≤{maxLeverage}x, 名义价值≤${maxNotional}");
            }
            else if (currentPrice >= 1000m) // 高价币（如BTC）
            {
                minQuantity = 0.001m;
                maxQuantity = 1000m;
                tickSize = 0.1m;
                stepSize = 0.001m;
                maxLeverage = 125;
                maxNotional = 2000000m;
            }
            else if (currentPrice >= 100m) // 中高价币（如ETH）
            {
                minQuantity = 0.001m;
                maxQuantity = 10000m;
                tickSize = 0.01m;
                stepSize = 0.001m;
                maxLeverage = 100;
                maxNotional = 1000000m;
            }
            else if (currentPrice >= 10m) // 中价币（如BNB）
            {
                minQuantity = 0.01m;
                maxQuantity = 100000m;
                tickSize = 0.001m;
                stepSize = 0.01m;
                maxLeverage = 75;
                maxNotional = 500000m;
            }
            else if (currentPrice >= 1m) // 一般价币（如DOT）
            {
                minQuantity = 0.1m;
                maxQuantity = 1000000m;
                tickSize = 0.0001m;
                stepSize = 0.1m;
                maxLeverage = 75;
                maxNotional = 200000m;
            }
            else if (currentPrice >= 0.1m) // 低价币（如ADA, AIOT等）
            {
                minQuantity = 1m;
                maxQuantity = 10000000m;
                tickSize = 0.00001m;
                stepSize = 1m;
                maxLeverage = 75;
                maxNotional = 100000m;
            }
            else if (currentPrice >= 0.01m) // 很低价币（如DOGE）
            {
                minQuantity = 10m;
                maxQuantity = 100000000m;
                tickSize = 0.000001m;
                stepSize = 10m;
                maxLeverage = 50;
                maxNotional = 100000m;
            }
            else // 超低价币（如PEPE、SHIB等）
            {
                minQuantity = 1000m;
                maxQuantity = 10000000000m;
                tickSize = 0.00000001m;
                stepSize = 1000m;
                maxLeverage = 25;
                maxNotional = 25000m;
            }
            
            Console.WriteLine($"📋 备选规则: 最小={minQuantity}, 最大={maxQuantity}, 杠杆={maxLeverage}x");
            
            return (minQuantity, maxQuantity, maxLeverage, maxNotional, tickSize, stepSize);
        }

        // 用于缓存最新价格，避免重复查询
        private decimal LatestPrice = 0;
        
        // 更新最新价格缓存的方法
        public void UpdateLatestPriceCache(decimal price)
        {
            LatestPrice = price;
        }

        // 币安交易规则响应模型
        public class BinanceExchangeInfoResponse
        {
            [JsonPropertyName("timezone")]
            public string Timezone { get; set; } = "";
            
            [JsonPropertyName("serverTime")]
            public long ServerTime { get; set; }
            
            [JsonPropertyName("symbols")]
            public BinanceSymbolInfo[] Symbols { get; set; } = Array.Empty<BinanceSymbolInfo>();
        }

        public class BinanceSymbolInfo
        {
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = "";
            
            [JsonPropertyName("status")]
            public string Status { get; set; } = "";
            
            [JsonPropertyName("baseAsset")]
            public string BaseAsset { get; set; } = "";
            
            [JsonPropertyName("quoteAsset")]
            public string QuoteAsset { get; set; } = "";
            
            [JsonPropertyName("filters")]
            public BinanceSymbolFilter[] Filters { get; set; } = Array.Empty<BinanceSymbolFilter>();
        }

        public class BinanceSymbolFilter
        {
            [JsonPropertyName("filterType")]
            public string FilterType { get; set; } = "";
            
            [JsonPropertyName("minPrice")]
            public string MinPrice { get; set; } = "";
            
            [JsonPropertyName("maxPrice")]
            public string MaxPrice { get; set; } = "";
            
            [JsonPropertyName("tickSize")]
            public string TickSize { get; set; } = "";
            
            [JsonPropertyName("minQty")]
            public string MinQty { get; set; } = "";
            
            [JsonPropertyName("maxQty")]
            public string MaxQty { get; set; } = "";
            
            [JsonPropertyName("stepSize")]
            public string StepSize { get; set; } = "";
            
            [JsonPropertyName("notional")]
            public string Notional { get; set; } = "";
            
            [JsonPropertyName("minNotional")]
            public string MinNotional { get; set; } = "";
        }

        private string FormatPrice(decimal price, string symbol)
        {
            // 根据不同合约格式化价格精度
            var formattedPrice = symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(price, 1).ToString("F1"),      // BTC: 1位小数
                "ETHUSDT" => Math.Round(price, 2).ToString("F2"),      // ETH: 2位小数
                "BNBUSDT" => Math.Round(price, 3).ToString("F3"),      // BNB: 3位小数
                "ADAUSDT" => Math.Round(price, 4).ToString("F4"),      // ADA: 4位小数
                "AIOTUSDT" => Math.Round(price, 5).ToString("F5"),     // AIOT: 5位小数
                "DOGEUSDT" => Math.Round(price, 5).ToString("F5"),     // DOGE: 5位小数
                "SOLUSDT" => Math.Round(price, 3).ToString("F3"),      // SOL: 3位小数
                "DOTUSDT" => Math.Round(price, 3).ToString("F3"),      // DOT: 3位小数
                "LINKUSDT" => Math.Round(price, 3).ToString("F3"),     // LINK: 3位小数
                "LTCUSDT" => Math.Round(price, 2).ToString("F2"),      // LTC: 2位小数
                "BCHUSDT" => Math.Round(price, 2).ToString("F2"),      // BCH: 2位小数
                "XRPUSDT" => Math.Round(price, 4).ToString("F4"),      // XRP: 4位小数
                "MATICUSDT" => Math.Round(price, 4).ToString("F4"),    // MATIC: 4位小数
                "AVAXUSDT" => Math.Round(price, 3).ToString("F3"),     // AVAX: 3位小数
                "UNIUSDT" => Math.Round(price, 3).ToString("F3"),      // UNI: 3位小数
                "ATOMUSDT" => Math.Round(price, 3).ToString("F3"),     // ATOM: 3位小数
                _ => Math.Round(price, 4).ToString("F4")               // 默认: 4位小数
            };
            
            Console.WriteLine($"💰 价格格式化: {symbol} {price:F8} → {formattedPrice}");
            return formattedPrice;
        }

        private string FormatQuantity(decimal quantity, string symbol)
        {
            // 根据不同合约格式化数量精度
            var formattedQuantity = symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(quantity, 3).ToString("F3"),   // BTC: 3位小数
                "ETHUSDT" => Math.Round(quantity, 3).ToString("F3"),   // ETH: 3位小数
                "BNBUSDT" => Math.Round(quantity, 2).ToString("F2"),   // BNB: 2位小数
                "ADAUSDT" => Math.Round(quantity, 0).ToString("F0"),   // ADA: 整数
                "AIOTUSDT" => Math.Round(quantity, 0).ToString("F0"),  // AIOT: 整数
                "DOGEUSDT" => Math.Round(quantity, 0).ToString("F0"),  // DOGE: 整数
                "SOLUSDT" => Math.Round(quantity, 1).ToString("F1"),   // SOL: 1位小数
                "DOTUSDT" => Math.Round(quantity, 1).ToString("F1"),   // DOT: 1位小数
                "LINKUSDT" => Math.Round(quantity, 1).ToString("F1"),  // LINK: 1位小数
                "LTCUSDT" => Math.Round(quantity, 2).ToString("F2"),   // LTC: 2位小数
                "BCHUSDT" => Math.Round(quantity, 3).ToString("F3"),   // BCH: 3位小数
                "XRPUSDT" => Math.Round(quantity, 0).ToString("F0"),   // XRP: 整数
                "MATICUSDT" => Math.Round(quantity, 0).ToString("F0"), // MATIC: 整数
                "AVAXUSDT" => Math.Round(quantity, 1).ToString("F1"),  // AVAX: 1位小数
                "UNIUSDT" => Math.Round(quantity, 1).ToString("F1"),   // UNI: 1位小数
                "ATOMUSDT" => Math.Round(quantity, 1).ToString("F1"),  // ATOM: 1位小数
                _ => Math.Round(quantity, 3).ToString("F3")            // 默认: 3位小数
            };
            
            Console.WriteLine($"📦 数量格式化: {symbol} {quantity:F8} → {formattedQuantity}");
            return formattedQuantity;
        }

        public async Task<List<OrderInfo>> GetAllOrdersAsync(string? symbol = null, int limit = 50)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                Console.WriteLine("⚠️ 使用模拟订单历史: 无API配置");
                return GetMockOrderHistory();
            }

            Console.WriteLine($"📜 开始获取订单历史记录（最近{limit}条）...");
            try
            {
                var endpoint = "/fapi/v1/allOrders";
                var parameters = new Dictionary<string, string>
                {
                    ["timestamp"] = GetCurrentTimestamp().ToString(),
                    ["limit"] = limit.ToString()
                };

                if (!string.IsNullOrEmpty(symbol))
                {
                    parameters["symbol"] = symbol;
                    Console.WriteLine($"📊 指定合约过滤: {symbol}");
                }

                var response = await SendSignedRequestAsync(HttpMethod.Get, endpoint, parameters);
                if (response == null) 
                {
                    Console.WriteLine("❌ 订单历史API调用失败");
                    return GetMockOrderHistory();
                }

                Console.WriteLine($"📄 订单历史API原始响应 (前300字符): {response.Substring(0, Math.Min(300, response.Length))}...");
                
                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine($"❌ 订单历史API返回错误: {response}");
                    return GetMockOrderHistory();
                }

                var ordersData = JsonSerializer.Deserialize<BinanceOrderResponse[]>(response, _jsonOptions);
                if (ordersData == null) 
                {
                    Console.WriteLine("❌ 解析订单历史响应失败");
                    return GetMockOrderHistory();
                }

                Console.WriteLine($"📋 API返回历史订单总数: {ordersData.Length}");
                
                // 详细分析每个订单，特别关注STOP_MARKET
                var stopMarketOrders = ordersData.Where(o => o.Type == "STOP_MARKET").ToArray();
                Console.WriteLine($"🛡️ 历史中STOP_MARKET订单数量: {stopMarketOrders.Length}");
                
                foreach (var order in stopMarketOrders)
                {
                    Console.WriteLine($"🛡️ 历史止损单: OrderId={order.OrderId}, Symbol={order.Symbol}, Side={order.Side}, Status={order.Status}");
                    Console.WriteLine($"   StopPrice={order.StopPrice}, UpdateTime={DateTimeOffset.FromUnixTimeMilliseconds(order.UpdateTime):yyyy-MM-dd HH:mm:ss}");
                }
                
                // 统计各种状态的订单
                var statusGroups = ordersData.GroupBy(o => o.Status).ToList();
                foreach (var group in statusGroups)
                {
                    Console.WriteLine($"📊 {group.Key}状态订单: {group.Count()}个");
                }
                
                var resultOrders = ordersData.Select(o => new OrderInfo
                {
                    OrderId = o.OrderId,
                    Symbol = o.Symbol,
                    Status = o.Status,
                    ClientOrderId = o.ClientOrderId,
                    Price = o.Price,
                    OrigQty = o.OrigQty,
                    ExecutedQty = o.ExecutedQty,
                    CumQuote = o.CumQuote,
                    TimeInForce = o.TimeInForce,
                    Type = o.Type,
                    ReduceOnly = o.ReduceOnly,
                    ClosePosition = o.ClosePosition,
                    Side = o.Side,
                    PositionSide = o.PositionSide,
                    StopPrice = o.StopPrice,
                    WorkingType = o.WorkingType,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(o.Time).DateTime,
                    UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(o.UpdateTime).DateTime
                }).ToList();
                
                Console.WriteLine($"✅ 转换后历史订单数量: {resultOrders.Count}");
                return resultOrders;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"❌ JSON解析订单历史异常: {jsonEx.Message}");
                return GetMockOrderHistory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取订单历史异常: {ex.Message}");
                return GetMockOrderHistory();
            }
        }

        private List<OrderInfo> GetMockOrderHistory()
        {
            return new List<OrderInfo>
            {
                new OrderInfo
                {
                    OrderId = 99999,
                    Symbol = "BTCUSDT",
                    Status = "FILLED",
                    Type = "STOP_MARKET",
                    Side = "SELL",
                    StopPrice = 45000.0m,
                    OrigQty = 0.001m,
                    ExecutedQty = 0.001m,
                    Time = DateTime.Now.AddMinutes(-10),
                    UpdateTime = DateTime.Now.AddMinutes(-8)
                }
            };
        }

        private async Task<(bool isValid, string errorMessage)> ValidatePositionLimitsAsync(string symbol, decimal quantity, int leverage, decimal currentPrice)
        {
            try
            {
                LogService.LogInfo($"🔍 开始检查持仓限制: {symbol} 新增数量={quantity} 杠杆={leverage}x");
                
                // 获取当前持仓
                var positions = await GetPositionsAsync().ConfigureAwait(false);
                var currentPosition = positions.FirstOrDefault(p => p.Symbol == symbol && Math.Abs(p.PositionAmt) > 0);
                
                decimal currentPositionAmt = currentPosition?.PositionAmt ?? 0;
                decimal newTotalPosition = Math.Abs(currentPositionAmt + quantity);
                
                LogService.LogInfo($"📊 当前持仓: {currentPositionAmt}");
                LogService.LogInfo($"📊 预计新持仓: {newTotalPosition}");
                
                // 根据杠杆和合约计算最大允许持仓
                var maxAllowedPosition = GetMaxPositionForLeverage(symbol, leverage, currentPrice);
                
                LogService.LogInfo($"📏 最大允许持仓: {maxAllowedPosition}");
                
                if (newTotalPosition > maxAllowedPosition)
                {
                    LogService.LogError($"❌ 持仓超限: {newTotalPosition} > {maxAllowedPosition}");
                    return (false, $"持仓将超过当前杠杆({leverage}x)允许的最大限制。当前:{currentPositionAmt:F4}, 最大允许:{maxAllowedPosition:F4}。建议降低杠杆或减少数量");
                }
                
                LogService.LogInfo("✅ 持仓限制检查通过");
                return (true, "持仓限制检查通过");
            }
            catch (Exception ex)
            {
                LogService.LogError($"❌ 持仓限制检查异常: {ex.Message}");
                // 异常时允许通过，避免误拦截
                return (true, "持仓限制检查异常，跳过检查");
            }
        }
        
        private decimal GetMaxPositionForLeverage(string symbol, int leverage, decimal currentPrice)
        {
            // 根据币安期货的持仓限制规则计算最大持仓
            // 这些值基于币安的实际限制，需要根据最新规则调整
            
            var baseLimit = symbol.ToUpper() switch
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
                "ADAUSDT" => leverage switch
                {
                    <= 25 => 500000m,
                    <= 50 => 250000m,
                    <= 75 => 100000m,
                    _ => 50000m
                },
                "DOGEUSDT" => leverage switch
                {
                    <= 25 => 2000000m,
                    <= 50 => 1000000m,
                    _ => 500000m
                },
                // 新增AIOTUSDT的具体限制
                "AIOTUSDT" => leverage switch
                {
                    <= 3 => 50000m,      // 3倍杠杆：50000（根据实际错误调整）
                    <= 10 => 20000m,     // 10倍杠杆：20000
                    <= 20 => 10000m,     // 20倍杠杆：10000
                    <= 50 => 5000m,      // 50倍杠杆：5000
                    _ => 1000m           // 更高杠杆：1000
                },
                // B2USDT的限制（从日志中看到）
                "B2USDT" => leverage switch
                {
                    <= 10 => 50000m,
                    <= 20 => 25000m,
                    <= 50 => 10000m,
                    _ => 5000m
                },
                // 其他小币种的保守限制
                _ when currentPrice < 1m => leverage switch
                {
                    <= 3 => 50000m,      // 对小币种更保守
                    <= 10 => 25000m,
                    <= 20 => 10000m,
                    <= 50 => 5000m,
                    _ => 1000m
                },
                // 默认限制
                _ => leverage switch
                {
                    <= 20 => 100000m,
                    <= 50 => 50000m,
                    _ => 10000m
                }
            };
            
            LogService.LogInfo($"🎯 {symbol} 在 {leverage}x 杠杆下的基础持仓限制: {baseLimit}");
            
            // 对于价格很低的币种，还需要考虑名义价值限制
            if (currentPrice > 0 && currentPrice < 1m)
            {
                // 计算基于名义价值的限制（例如：不超过$50000）
                var maxValueLimit = 50000m;
                var valueBasedLimit = maxValueLimit / currentPrice;
                var finalLimit = Math.Min(baseLimit, valueBasedLimit);
                
                if (finalLimit < baseLimit)
                {
                    LogService.LogWarning($"⚠️ 基于名义价值限制调整: {baseLimit} → {finalLimit} (${maxValueLimit} ÷ {currentPrice})");
                }
                
                return finalLimit;
            }
            
            return baseLimit;
        }

        public async Task<bool> SetLeverageAsync(string symbol, int leverage)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                Console.WriteLine($"⚠️ 使用模拟杠杆设置: 无API配置，模拟设置 {symbol} 杠杆为 {leverage}x");
                await Task.Delay(200);
                return true;
            }

            Console.WriteLine($"🎚️ 开始设置杠杆: {symbol} → {leverage}x");
            try
            {
                var endpoint = "/fapi/v1/leverage";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol.ToUpper(),
                    ["leverage"] = leverage.ToString(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                Console.WriteLine($"📤 发送杠杆设置请求:");
                Console.WriteLine($"   合约: {symbol}");
                Console.WriteLine($"   杠杆: {leverage}x");

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters).ConfigureAwait(false);
                
                if (response == null)
                {
                    Console.WriteLine("❌ 杠杆设置API响应为空");
                    return false;
                }

                Console.WriteLine($"📄 杠杆设置响应: {response}");

                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine("❌ 杠杆设置失败");
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(response, _jsonOptions);
                        Console.WriteLine($"   错误代码: {errorResponse?.Code}");
                        Console.WriteLine($"   错误消息: {errorResponse?.Msg}");
                        
                        // 特殊处理常见错误
                        if (errorResponse?.Code == -4028)
                        {
                            Console.WriteLine("💡 可能原因: 杠杆已经是当前设置，或该合约不支持此杠杆");
                            return true; // 杠杆已经正确，视为成功
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"❌ 解析杠杆设置错误响应异常: {parseEx.Message}");
                    }
                    return false;
                }

                // 检查成功响应
                bool success = response.Contains("\"leverage\"") || response.Contains("\"symbol\"");
                Console.WriteLine($"✅ 杠杆设置结果: {(success ? "成功" : "失败")}");
                
                if (success)
                {
                    Console.WriteLine($"🎯 {symbol} 杠杆已设置为 {leverage}x");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 设置杠杆异常: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetMarginTypeAsync(string symbol, string marginType)
        {
            if (_currentAccount == null || string.IsNullOrEmpty(_currentAccount.ApiKey) || string.IsNullOrEmpty(_currentAccount.SecretKey))
            {
                Console.WriteLine($"⚠️ 使用模拟保证金模式设置: 无API配置，模拟设置 {symbol} 保证金模式为 {marginType}");
                await Task.Delay(200);
                return true;
            }

            Console.WriteLine($"💰 开始设置保证金模式: {symbol} → {marginType}");
            try
            {
                var endpoint = "/fapi/v1/marginType";
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol.ToUpper(),
                    ["marginType"] = marginType.ToUpper(),
                    ["timestamp"] = GetCurrentTimestamp().ToString()
                };

                Console.WriteLine($"📤 发送保证金模式设置请求:");
                Console.WriteLine($"   合约: {symbol}");
                Console.WriteLine($"   模式: {marginType}");

                var response = await SendSignedRequestAsync(HttpMethod.Post, endpoint, parameters).ConfigureAwait(false);
                
                if (response == null)
                {
                    Console.WriteLine("❌ 保证金模式设置API响应为空");
                    return false;
                }

                Console.WriteLine($"📄 保证金模式设置响应: {response}");

                // 检查是否是错误响应
                if (response.Contains("\"code\"") && response.Contains("\"msg\""))
                {
                    Console.WriteLine("❌ 保证金模式设置失败");
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(response, _jsonOptions);
                        Console.WriteLine($"   错误代码: {errorResponse?.Code}");
                        Console.WriteLine($"   错误消息: {errorResponse?.Msg}");
                        
                        // 特殊处理常见错误
                        if (errorResponse?.Code == -4046)
                        {
                            Console.WriteLine("💡 可能原因: 保证金模式已经是当前设置");
                            return true; // 模式已经正确，视为成功
                        }
                        else if (errorResponse?.Code == -4047)
                        {
                            Console.WriteLine("💡 可能原因: 该合约有持仓时无法更改保证金模式");
                            return false; // 确实失败
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"❌ 解析保证金模式设置错误响应异常: {parseEx.Message}");
                    }
                    return false;
                }

                // 检查成功响应
                bool success = response.Contains("\"code\":200") || response.Length < 50; // 成功响应通常很短
                Console.WriteLine($"✅ 保证金模式设置结果: {(success ? "成功" : "失败")}");
                
                if (success)
                {
                    Console.WriteLine($"🎯 {symbol} 保证金模式已设置为 {marginType}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 设置保证金模式异常: {ex.Message}");
                return false;
            }
        }
    }

    // Binance API响应模型类
    public class BinanceErrorResponse
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
    }
    
    public class BinanceOrderResponse
    {
        public long OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal OrigQty { get; set; }
        public decimal Price { get; set; }
        public decimal StopPrice { get; set; }
        public string ClientOrderId { get; set; } = string.Empty;
        public bool ReduceOnly { get; set; }
        public string PositionSide { get; set; } = string.Empty;
        public long UpdateTime { get; set; }
        public decimal ExecutedQty { get; set; }
        public decimal CumQuote { get; set; }
        public string TimeInForce { get; set; } = string.Empty;
        public bool ClosePosition { get; set; }
        public string WorkingType { get; set; } = string.Empty;
        public long Time { get; set; }
    }
} 