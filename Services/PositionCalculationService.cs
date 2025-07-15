using System;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 加仓数量计算服务
    /// </summary>
    public class PositionCalculationService
    {
        private readonly ILogger<PositionCalculationService> _logger;
        private readonly RiskCapitalService _riskCapitalService;
        
        public PositionCalculationService(
            ILogger<PositionCalculationService> logger, 
            RiskCapitalService riskCapitalService)
        {
            _logger = logger;
            _riskCapitalService = riskCapitalService;
        }
        
        /// <summary>
        /// 计算加仓数量
        /// </summary>
        /// <param name="riskCapital">风险金</param>
        /// <param name="addPositionMultiplier">加仓倍数</param>
        /// <param name="stopLossRatio">止损比例</param>
        /// <param name="price">价格</param>
        /// <returns>加仓数量</returns>
        public decimal CalculateAddPositionQuantity(
            decimal riskCapital, 
            decimal addPositionMultiplier, 
            decimal stopLossRatio, 
            decimal price)
        {
            try
            {
                // 验证参数
                ValidateParameters(riskCapital, addPositionMultiplier, stopLossRatio, price);
                
                // 计算公式：加仓数量 = (风险金 × 加仓倍数 ÷ 止损比例) ÷ 价格
                var addPositionQuantity = (riskCapital * addPositionMultiplier / stopLossRatio) / price;
                
                _logger.LogDebug($"计算加仓数量: 风险金={riskCapital:F2}U, 加仓倍数={addPositionMultiplier:F2}, " +
                    $"止损比例={stopLossRatio:F4}, 价格={price:F4}, 加仓数量={addPositionQuantity:F8}");
                
                return addPositionQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算加仓数量失败: 风险金={riskCapital}, 加仓倍数={addPositionMultiplier}, " +
                    $"止损比例={stopLossRatio}, 价格={price}");
                throw;
            }
        }
        
        /// <summary>
        /// 根据阶梯配置计算加仓数量
        /// </summary>
        /// <param name="tier">推仓阶梯</param>
        /// <param name="riskCapital">风险金</param>
        /// <param name="price">价格</param>
        /// <returns>加仓数量</returns>
        public decimal CalculateAddPositionQuantityFromTier(
            AddPositionTier tier, 
            decimal riskCapital, 
            decimal price)
        {
            try
            {
                if (tier == null)
                {
                    throw new ArgumentNullException(nameof(tier));
                }
                
                return CalculateAddPositionQuantity(
                    riskCapital, 
                    tier.RiskMultiplier, 
                    tier.StopLossRatio, 
                    price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"根据阶梯配置计算加仓数量失败: 阶梯={tier?.TierIndex}, 价格={price}");
                throw;
            }
        }
        
        /// <summary>
        /// 从当前账户信息计算加仓数量
        /// </summary>
        /// <param name="tier">推仓阶梯</param>
        /// <param name="price">价格</param>
        /// <returns>加仓数量</returns>
        public decimal CalculateAddPositionQuantityFromCurrentAccount(
            AddPositionTier tier, 
            decimal price)
        {
            try
            {
                var riskCapital = _riskCapitalService.CalculateRiskCapitalFromCurrentAccountInfo();
                return CalculateAddPositionQuantityFromTier(tier, riskCapital, price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从当前账户信息计算加仓数量失败: 阶梯={tier?.TierIndex}, 价格={price}");
                throw;
            }
        }
        
        /// <summary>
        /// 计算加仓后的总仓位数量
        /// </summary>
        /// <param name="currentPositionSize">当前仓位数量</param>
        /// <param name="addPositionQuantity">加仓数量</param>
        /// <returns>加仓后的总仓位数量</returns>
        public decimal CalculateNewPositionSize(decimal currentPositionSize, decimal addPositionQuantity)
        {
            try
            {
                var newPositionSize = Math.Abs(currentPositionSize) + Math.Abs(addPositionQuantity);
                
                // 保持原有的方向
                if (currentPositionSize < 0)
                {
                    newPositionSize = -newPositionSize;
                }
                
                _logger.LogDebug($"计算加仓后总仓位: 当前仓位={currentPositionSize:F8}, 加仓数量={addPositionQuantity:F8}, " +
                    $"新仓位={newPositionSize:F8}");
                
                return newPositionSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算加仓后总仓位失败: 当前仓位={currentPositionSize}, 加仓数量={addPositionQuantity}");
                throw;
            }
        }
        
        /// <summary>
        /// 计算加仓后的止损价格
        /// </summary>
        /// <param name="currentAveragePrice">当前持仓均价</param>
        /// <param name="currentPositionSize">当前仓位数量</param>
        /// <param name="addPositionPrice">加仓价格</param>
        /// <param name="addPositionQuantity">加仓数量</param>
        /// <param name="stopLossRatio">止损比例</param>
        /// <returns>止损价格</returns>
        public decimal CalculateNewStopLossPrice(
            decimal currentAveragePrice,
            decimal currentPositionSize,
            decimal addPositionPrice,
            decimal addPositionQuantity,
            decimal stopLossRatio)
        {
            try
            {
                // 计算新的平均价格
                var totalValue = Math.Abs(currentPositionSize) * currentAveragePrice + 
                                Math.Abs(addPositionQuantity) * addPositionPrice;
                var totalQuantity = Math.Abs(currentPositionSize) + Math.Abs(addPositionQuantity);
                var newAveragePrice = totalValue / totalQuantity;
                
                // 计算止损价格
                var stopLossPrice = currentPositionSize > 0 
                    ? newAveragePrice * (1 - stopLossRatio)  // 多头止损
                    : newAveragePrice * (1 + stopLossRatio); // 空头止损
                
                _logger.LogDebug($"计算新止损价格: 当前均价={currentAveragePrice:F4}, 当前仓位={currentPositionSize:F8}, " +
                    $"加仓价格={addPositionPrice:F4}, 加仓数量={addPositionQuantity:F8}, " +
                    $"止损比例={stopLossRatio:F4}, 新均价={newAveragePrice:F4}, 止损价格={stopLossPrice:F4}");
                
                return stopLossPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算新止损价格失败: 当前均价={currentAveragePrice}, 当前仓位={currentPositionSize}, " +
                    $"加仓价格={addPositionPrice}, 加仓数量={addPositionQuantity}, 止损比例={stopLossRatio}");
                throw;
            }
        }
        
        /// <summary>
        /// 调整数量精度
        /// </summary>
        /// <param name="quantity">原始数量</param>
        /// <param name="stepSize">步进大小</param>
        /// <returns>调整后的数量</returns>
        public decimal AdjustQuantityPrecision(decimal quantity, decimal stepSize)
        {
            try
            {
                if (stepSize <= 0)
                {
                    throw new ArgumentException("步进大小必须大于0", nameof(stepSize));
                }
                
                var adjustedQuantity = Math.Floor(quantity / stepSize) * stepSize;
                
                _logger.LogDebug($"调整数量精度: 原始数量={quantity:F8}, 步进大小={stepSize:F8}, 调整后数量={adjustedQuantity:F8}");
                
                return adjustedQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"调整数量精度失败: 数量={quantity}, 步进大小={stepSize}");
                throw;
            }
        }
        
        /// <summary>
        /// 验证加仓数量计算参数
        /// </summary>
        /// <param name="riskCapital">风险金</param>
        /// <param name="addPositionMultiplier">加仓倍数</param>
        /// <param name="stopLossRatio">止损比例</param>
        /// <param name="price">价格</param>
        /// <returns>验证结果</returns>
        public PositionCalculationValidationResult ValidateAddPositionParameters(
            decimal riskCapital,
            decimal addPositionMultiplier,
            decimal stopLossRatio,
            decimal price)
        {
            var result = new PositionCalculationValidationResult();
            
            try
            {
                ValidateParameters(riskCapital, addPositionMultiplier, stopLossRatio, price);
                
                // 计算加仓数量
                var addPositionQuantity = CalculateAddPositionQuantity(
                    riskCapital, addPositionMultiplier, stopLossRatio, price);
                
                // 检查数量是否过小
                if (addPositionQuantity < 0.001m)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "计算出的加仓数量过小";
                    return result;
                }
                
                // 检查数量是否过大
                if (addPositionQuantity > 1000000m)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "计算出的加仓数量过大";
                    return result;
                }
                
                result.IsValid = true;
                result.AddPositionQuantity = addPositionQuantity;
                result.RiskAmount = riskCapital * addPositionMultiplier;
                
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
        
        /// <summary>
        /// 验证计算参数
        /// </summary>
        private void ValidateParameters(
            decimal riskCapital,
            decimal addPositionMultiplier,
            decimal stopLossRatio,
            decimal price)
        {
            if (riskCapital <= 0)
            {
                throw new ArgumentException("风险金必须大于0", nameof(riskCapital));
            }
            
            if (addPositionMultiplier <= 0)
            {
                throw new ArgumentException("加仓倍数必须大于0", nameof(addPositionMultiplier));
            }
            
            if (stopLossRatio <= 0 || stopLossRatio >= 1)
            {
                throw new ArgumentException("止损比例必须在0到1之间", nameof(stopLossRatio));
            }
            
            if (price <= 0)
            {
                throw new ArgumentException("价格必须大于0", nameof(price));
            }
        }
    }
    
    /// <summary>
    /// 加仓数量计算验证结果
    /// </summary>
    public class PositionCalculationValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; } = true;
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 计算出的加仓数量
        /// </summary>
        public decimal AddPositionQuantity { get; set; }
        
        /// <summary>
        /// 风险金额
        /// </summary>
        public decimal RiskAmount { get; set; }
    }
}