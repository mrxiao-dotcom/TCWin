using System.Text.Json.Serialization;

namespace BinanceFuturesTrader.Models
{
    // 账户信息响应
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

    // 持仓信息响应
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

    // 订单信息响应
    public class BinanceOrderResponse
    {
        [JsonPropertyName("orderId")]
        public long OrderId { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("clientOrderId")]
        public string ClientOrderId { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("origQty")]
        public decimal OrigQty { get; set; }

        [JsonPropertyName("executedQty")]
        public decimal ExecutedQty { get; set; }

        [JsonPropertyName("cumQuote")]
        public decimal CumQuote { get; set; }

        [JsonPropertyName("timeInForce")]
        public string TimeInForce { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("reduceOnly")]
        public bool ReduceOnly { get; set; }

        [JsonPropertyName("closePosition")]
        public bool ClosePosition { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; } = string.Empty;

        [JsonPropertyName("positionSide")]
        public string PositionSide { get; set; } = string.Empty;

        [JsonPropertyName("stopPrice")]
        public decimal StopPrice { get; set; }

        [JsonPropertyName("workingType")]
        public string WorkingType { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("updateTime")]
        public long UpdateTime { get; set; }
    }

    // 价格信息响应
    public class BinancePriceResponse
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }

    // 下单响应
    public class BinancePlaceOrderResponse
    {
        [JsonPropertyName("orderId")]
        public long OrderId { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("clientOrderId")]
        public string ClientOrderId { get; set; } = string.Empty;
    }

    // 错误响应
    public class BinanceErrorResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
} 