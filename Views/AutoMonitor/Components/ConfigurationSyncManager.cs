using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 配置同步管理器
    /// 负责处理基础配置和合约配置之间的同步
    /// </summary>
    public class ConfigurationSyncManager
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private readonly UIComponentManager _uiComponentManager;
        
        public ConfigurationSyncManager(
            AutoMonitorDataModel dataModel,
            UIComponentManager uiComponentManager,
            ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiComponentManager = uiComponentManager ?? throw new ArgumentNullException(nameof(uiComponentManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 处理基础配置变化的同步
        /// </summary>
        /// <param name="newAddPositionTiers">新的推仓阶梯数</param>
        /// <param name="newProfitProtectionTiers">新的止盈阶梯数</param>
        /// <param name="showConfirmation">是否显示确认对话框</param>
        /// <returns>是否成功同步</returns>
        public bool HandleBaseConfigurationChange(int newAddPositionTiers, int newProfitProtectionTiers, bool showConfirmation = true)
        {
            try
            {
                _logger.LogInformation($"🔄 开始处理基础配置变化：推仓阶梯 {newAddPositionTiers}，止盈阶梯 {newProfitProtectionTiers}");
                
                // 获取当前配置
                var currentAddPositionTiers = GetCurrentMaxAddPositionTiers();
                var currentProfitProtectionTiers = GetCurrentMaxProfitProtectionTiers();
                
                // 检查是否有变化
                if (currentAddPositionTiers == newAddPositionTiers && 
                    currentProfitProtectionTiers == newProfitProtectionTiers)
                {
                    _logger.LogInformation("⚠️ 配置没有变化，无需同步");
                    return true;
                }
                
                // 检查是否有合约需要同步
                var contractCount = _dataModel.ContractMonitors.Count;
                if (contractCount == 0)
                {
                    _logger.LogInformation("⚠️ 没有合约需要同步");
                    return true;
                }
                
                // 显示确认对话框
                if (showConfirmation)
                {
                    var confirmationResult = ShowConfigurationSyncConfirmation(
                        currentAddPositionTiers, newAddPositionTiers,
                        currentProfitProtectionTiers, newProfitProtectionTiers,
                        contractCount);
                    
                    if (confirmationResult != MessageBoxResult.Yes)
                    {
                        _logger.LogInformation("❌ 用户取消了配置同步");
                        return false;
                    }
                }
                
                // 执行同步
                var success = ExecuteConfigurationSync(newAddPositionTiers, newProfitProtectionTiers);
                
                if (success)
                {
                    _logger.LogInformation("✅ 配置同步完成");
                    if (showConfirmation)
                    {
                        ShowSyncSuccessMessage(contractCount, newAddPositionTiers, newProfitProtectionTiers);
                    }
                }
                else
                {
                    _logger.LogError("❌ 配置同步失败");
                    if (showConfirmation)
                    {
                        MessageBox.Show("❌ 配置同步失败，请检查日志了解详情", "同步失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理基础配置变化时发生异常");
                if (showConfirmation)
                {
                    MessageBox.Show($"❌ 配置同步异常：{ex.Message}", "同步异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return false;
            }
        }
        
        /// <summary>
        /// 显示配置同步确认对话框
        /// </summary>
        private MessageBoxResult ShowConfigurationSyncConfirmation(
            int currentAddPositionTiers, int newAddPositionTiers,
            int currentProfitProtectionTiers, int newProfitProtectionTiers,
            int contractCount)
        {
            var message = "🔄 检测到基础配置变化，需要同步合约配置\n\n";
            
            message += "📊 配置变化详情：\n";
            message += $"• 推仓阶梯：{currentAddPositionTiers} → {newAddPositionTiers}\n";
            message += $"• 止盈阶梯：{currentProfitProtectionTiers} → {newProfitProtectionTiers}\n";
            message += $"• 影响合约数量：{contractCount} 个\n\n";
            
            message += "🔧 同步操作内容：\n";
            
            if (newAddPositionTiers > currentAddPositionTiers)
            {
                message += $"• 为每个合约增加 {newAddPositionTiers - currentAddPositionTiers} 个推仓阶梯\n";
            }
            else if (newAddPositionTiers < currentAddPositionTiers)
            {
                message += $"• 为每个合约移除 {currentAddPositionTiers - newAddPositionTiers} 个推仓阶梯\n";
            }
            
            if (newProfitProtectionTiers > currentProfitProtectionTiers)
            {
                message += $"• 为每个合约增加 {newProfitProtectionTiers - currentProfitProtectionTiers} 个止盈阶梯\n";
            }
            else if (newProfitProtectionTiers < currentProfitProtectionTiers)
            {
                message += $"• 为每个合约移除 {currentProfitProtectionTiers - newProfitProtectionTiers} 个止盈阶梯\n";
            }
            
            message += "• 自动调整表格列结构\n\n";
            message += "💡 提示：新增的阶梯需要手动设置触发条件\n\n";
            message += "是否继续同步？";
            
            return MessageBox.Show(message, "配置同步确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        }
        
        /// <summary>
        /// 显示同步成功消息
        /// </summary>
        private void ShowSyncSuccessMessage(int contractCount, int addPositionTiers, int profitProtectionTiers)
        {
            var message = $"✅ 配置同步完成！\n\n";
            message += $"📊 同步结果：\n";
            message += $"• 处理合约数量：{contractCount} 个\n";
            message += $"• 推仓阶梯：{addPositionTiers} 个\n";
            message += $"• 止盈阶梯：{profitProtectionTiers} 个\n\n";
            message += $"💡 表格列结构已自动调整\n";
            message += $"🔧 请检查并设置新增阶梯的触发条件";
            
            MessageBox.Show(message, "配置同步", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// 执行配置同步
        /// </summary>
        private bool ExecuteConfigurationSync(int targetAddPositionTiers, int targetProfitProtectionTiers)
        {
            try
            {
                // 使用UIComponentManager执行同步
                _uiComponentManager.HandleBaseConfigurationChange(targetAddPositionTiers, targetProfitProtectionTiers);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 执行配置同步时发生异常");
                return false;
            }
        }
        
        /// <summary>
        /// 获取当前最大推仓阶梯数
        /// </summary>
        private int GetCurrentMaxAddPositionTiers()
        {
            if (_dataModel.ContractMonitors.Count == 0)
                return 0;
            
            return _dataModel.ContractMonitors
                .SelectMany(c => c.TriggerConditions.Where(tc => tc.Type == TriggerConditionType.AddPosition))
                .Select(tc => (tc.TierIndex ?? 0) + 1)
                .DefaultIfEmpty(0)
                .Max();
        }
        
        /// <summary>
        /// 获取当前最大止盈阶梯数
        /// </summary>
        private int GetCurrentMaxProfitProtectionTiers()
        {
            if (_dataModel.ContractMonitors.Count == 0)
                return 0;
            
            return _dataModel.ContractMonitors
                .SelectMany(c => c.TriggerConditions.Where(tc => tc.Type == TriggerConditionType.ProfitProtection))
                .Select(tc => (tc.TierIndex ?? 0) + 1)
                .DefaultIfEmpty(0)
                .Max();
        }
        
        /// <summary>
        /// 分析配置变化
        /// </summary>
        /// <param name="newAddPositionTiers">新的推仓阶梯数</param>
        /// <param name="newProfitProtectionTiers">新的止盈阶梯数</param>
        /// <returns>配置变化分析结果</returns>
        public ConfigurationChangeAnalysis AnalyzeConfigurationChange(int newAddPositionTiers, int newProfitProtectionTiers)
        {
            var currentAddPositionTiers = GetCurrentMaxAddPositionTiers();
            var currentProfitProtectionTiers = GetCurrentMaxProfitProtectionTiers();
            
            return new ConfigurationChangeAnalysis
            {
                CurrentAddPositionTiers = currentAddPositionTiers,
                NewAddPositionTiers = newAddPositionTiers,
                CurrentProfitProtectionTiers = currentProfitProtectionTiers,
                NewProfitProtectionTiers = newProfitProtectionTiers,
                AffectedContractCount = _dataModel.ContractMonitors.Count,
                HasChanges = currentAddPositionTiers != newAddPositionTiers || 
                           currentProfitProtectionTiers != newProfitProtectionTiers
            };
        }
        
        /// <summary>
        /// 生成配置变化描述
        /// </summary>
        /// <param name="analysis">配置变化分析</param>
        /// <returns>变化描述</returns>
        public string GenerateChangeDescription(ConfigurationChangeAnalysis analysis)
        {
            if (!analysis.HasChanges)
            {
                return "📋 配置没有变化";
            }
            
            var description = new List<string>();
            
            if (analysis.CurrentAddPositionTiers != analysis.NewAddPositionTiers)
            {
                if (analysis.NewAddPositionTiers > analysis.CurrentAddPositionTiers)
                {
                    description.Add($"📈 推仓阶梯增加：{analysis.CurrentAddPositionTiers} → {analysis.NewAddPositionTiers}");
                }
                else
                {
                    description.Add($"📉 推仓阶梯减少：{analysis.CurrentAddPositionTiers} → {analysis.NewAddPositionTiers}");
                }
            }
            
            if (analysis.CurrentProfitProtectionTiers != analysis.NewProfitProtectionTiers)
            {
                if (analysis.NewProfitProtectionTiers > analysis.CurrentProfitProtectionTiers)
                {
                    description.Add($"📈 止盈阶梯增加：{analysis.CurrentProfitProtectionTiers} → {analysis.NewProfitProtectionTiers}");
                }
                else
                {
                    description.Add($"📉 止盈阶梯减少：{analysis.CurrentProfitProtectionTiers} → {analysis.NewProfitProtectionTiers}");
                }
            }
            
            if (analysis.AffectedContractCount > 0)
            {
                description.Add($"🎯 影响合约：{analysis.AffectedContractCount} 个");
            }
            
            return string.Join("\n", description);
        }
        
        /// <summary>
        /// 检查是否可以进行配置同步
        /// </summary>
        /// <returns>是否可以同步</returns>
        public bool CanSyncConfiguration()
        {
            // 检查监控状态 - 只有在停止状态时才能同步
            var isRunning = _dataModel.MonitorStatus == "运行中";
            var reason = isRunning ? "监控运行中，无法同步配置" : "可以同步配置";
            
            _logger.LogDebug($"📋 配置同步状态检查: {reason}");
            
            return !isRunning;
        }
        
        /// <summary>
        /// 强制刷新所有配置
        /// </summary>
        public void ForceRefreshAllConfigurations()
        {
            try
            {
                _logger.LogInformation("🔄 强制刷新所有配置");
                
                _uiComponentManager.ForceRefreshColumnStructure();
                
                _logger.LogInformation("✅ 所有配置刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 强制刷新配置时发生异常");
                throw;
            }
        }
    }
    
    /// <summary>
    /// 配置变化分析结果
    /// </summary>
    public class ConfigurationChangeAnalysis
    {
        public int CurrentAddPositionTiers { get; set; }
        public int NewAddPositionTiers { get; set; }
        public int CurrentProfitProtectionTiers { get; set; }
        public int NewProfitProtectionTiers { get; set; }
        public int AffectedContractCount { get; set; }
        public bool HasChanges { get; set; }
        
        public int AddPositionTierChange => NewAddPositionTiers - CurrentAddPositionTiers;
        public int ProfitProtectionTierChange => NewProfitProtectionTiers - CurrentProfitProtectionTiers;
        
        public bool IsAddPositionTierIncreasing => AddPositionTierChange > 0;
        public bool IsAddPositionTierDecreasing => AddPositionTierChange < 0;
        public bool IsProfitProtectionTierIncreasing => ProfitProtectionTierChange > 0;
        public bool IsProfitProtectionTierDecreasing => ProfitProtectionTierChange < 0;
    }
} 