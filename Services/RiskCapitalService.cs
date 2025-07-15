using System;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 风险金计算服务
    /// </summary>
    public class RiskCapitalService
    {
        private readonly ILogger<RiskCapitalService> _logger;
        private readonly MainViewModel _mainViewModel;
        
        public RiskCapitalService(ILogger<RiskCapitalService> logger, MainViewModel mainViewModel)
        {
            _logger = logger;
            _mainViewModel = mainViewModel;
        }
        
        /// <summary>
        /// 计算风险金
        /// </summary>
        /// <param name="accountEquity">账户权益</param>
        /// <param name="riskCapitalTimes">风险次数</param>
        /// <returns>风险金金额</returns>
        public decimal CalculateRiskCapital(decimal accountEquity, int riskCapitalTimes)
        {
            try
            {
                if (accountEquity <= 0)
                {
                    throw new ArgumentException("账户权益必须大于0", nameof(accountEquity));
                }
                
                if (riskCapitalTimes <= 0)
                {
                    throw new ArgumentException("风险次数必须大于0", nameof(riskCapitalTimes));
                }
                
                var riskCapital = accountEquity / riskCapitalTimes;
                
                _logger.LogDebug($"计算风险金: 账户权益={accountEquity:F2}U, 风险次数={riskCapitalTimes}, 风险金={riskCapital:F2}U");
                
                return riskCapital;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算风险金失败: 账户权益={accountEquity}, 风险次数={riskCapitalTimes}");
                throw;
            }
        }
        
        /// <summary>
        /// 从当前账户配置计算风险金
        /// </summary>
        /// <param name="accountEquity">账户权益</param>
        /// <returns>风险金金额</returns>
        public decimal CalculateRiskCapitalFromCurrentAccount(decimal accountEquity)
        {
            try
            {
                var selectedAccount = _mainViewModel.SelectedAccount;
                if (selectedAccount == null)
                {
                    throw new InvalidOperationException("未选择账户");
                }
                
                var riskCapitalTimes = selectedAccount.RiskCapitalTimes;
                return CalculateRiskCapital(accountEquity, riskCapitalTimes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从当前账户配置计算风险金失败");
                throw;
            }
        }
        
        /// <summary>
        /// 从当前账户信息计算风险金
        /// </summary>
        /// <returns>风险金金额</returns>
        public decimal CalculateRiskCapitalFromCurrentAccountInfo()
        {
            try
            {
                var selectedAccount = _mainViewModel.SelectedAccount;
                if (selectedAccount == null)
                {
                    throw new InvalidOperationException("未选择账户");
                }
                
                var accountInfo = _mainViewModel.AccountInfo;
                if (accountInfo == null)
                {
                    throw new InvalidOperationException("账户信息未加载");
                }
                
                var accountEquity = accountInfo.TotalEquity;
                var riskCapitalTimes = selectedAccount.RiskCapitalTimes;
                
                return CalculateRiskCapital(accountEquity, riskCapitalTimes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从当前账户信息计算风险金失败");
                throw;
            }
        }
        
        /// <summary>
        /// 获取当前账户的风险次数
        /// </summary>
        /// <returns>风险次数</returns>
        public int GetCurrentRiskCapitalTimes()
        {
            try
            {
                var selectedAccount = _mainViewModel.SelectedAccount;
                if (selectedAccount == null)
                {
                    throw new InvalidOperationException("未选择账户");
                }
                
                return selectedAccount.RiskCapitalTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前账户风险次数失败");
                throw;
            }
        }
        
        /// <summary>
        /// 获取当前账户权益
        /// </summary>
        /// <returns>账户权益</returns>
        public decimal GetCurrentAccountEquity()
        {
            try
            {
                var accountInfo = _mainViewModel.AccountInfo;
                if (accountInfo == null)
                {
                    throw new InvalidOperationException("账户信息未加载");
                }
                
                return accountInfo.TotalEquity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前账户权益失败");
                throw;
            }
        }
        
        /// <summary>
        /// 验证风险金参数
        /// </summary>
        /// <param name="accountEquity">账户权益</param>
        /// <param name="riskCapitalTimes">风险次数</param>
        /// <returns>验证结果</returns>
        public RiskCapitalValidationResult ValidateRiskCapitalParameters(decimal accountEquity, int riskCapitalTimes)
        {
            var result = new RiskCapitalValidationResult();
            
            if (accountEquity <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "账户权益必须大于0";
                return result;
            }
            
            if (riskCapitalTimes <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "风险次数必须大于0";
                return result;
            }
            
            if (riskCapitalTimes > 100)
            {
                result.IsValid = false;
                result.ErrorMessage = "风险次数不能超过100";
                return result;
            }
            
            var riskCapital = accountEquity / riskCapitalTimes;
            if (riskCapital < 1.0m)
            {
                result.IsValid = false;
                result.ErrorMessage = "计算出的风险金过小（小于1USDT），请调整风险次数";
                return result;
            }
            
            result.IsValid = true;
            result.RiskCapital = riskCapital;
            return result;
        }
        
        /// <summary>
        /// 计算建议的风险次数
        /// </summary>
        /// <param name="accountEquity">账户权益</param>
        /// <param name="targetRiskCapital">目标风险金</param>
        /// <returns>建议的风险次数</returns>
        public int CalculateSuggestedRiskTimes(decimal accountEquity, decimal targetRiskCapital)
        {
            try
            {
                if (accountEquity <= 0 || targetRiskCapital <= 0)
                {
                    throw new ArgumentException("账户权益和目标风险金必须大于0");
                }
                
                var suggestedTimes = (int)Math.Round(accountEquity / targetRiskCapital, 0, MidpointRounding.AwayFromZero);
                
                // 确保在合理范围内
                suggestedTimes = Math.Max(1, Math.Min(100, suggestedTimes));
                
                _logger.LogDebug($"计算建议风险次数: 账户权益={accountEquity:F2}U, 目标风险金={targetRiskCapital:F2}U, 建议次数={suggestedTimes}");
                
                return suggestedTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算建议风险次数失败: 账户权益={accountEquity}, 目标风险金={targetRiskCapital}");
                throw;
            }
        }
    }
    
    /// <summary>
    /// 风险金验证结果
    /// </summary>
    public class RiskCapitalValidationResult
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
        /// 计算出的风险金
        /// </summary>
        public decimal RiskCapital { get; set; }
    }
}