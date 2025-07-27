using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 合约档案 - 为每个合约建立独立的配置档案
    /// </summary>
    public class ContractProfile : INotifyPropertyChanged
    {
        #region 基本信息
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = "";
        
        /// <summary>
        /// 合约方向（LONG/SHORT）
        /// </summary>
        public string Side { get; set; } = "";
        
        /// <summary>
        /// 持仓数量
        /// </summary>
        public decimal PositionSize { get; set; } = 0;
        
        /// <summary>
        /// 入场价格
        /// </summary>
        public decimal EntryPrice { get; set; } = 0;
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; } = 0;
        
        /// <summary>
        /// 当前浮盈
        /// </summary>
        public decimal UnrealizedPnl { get; set; } = 0;
        
        /// <summary>
        /// 档案创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        
        #endregion
        
        #region 配置信息
        
        /// <summary>
        /// 是否启用独立配置（false则使用基础配置）
        /// </summary>
        public bool UseIndependentConfig { get; set; } = false;
        
        /// <summary>
        /// 基础配置名称（当使用基础配置时）
        /// </summary>
        public string BaseConfigName { get; set; } = "";
        
        /// <summary>
        /// 独立的保本配置
        /// </summary>
        public ContractBreakEvenConfig? IndependentBreakEvenConfig { get; set; }
        
        /// <summary>
        /// 独立的推仓配置
        /// </summary>
        public ContractAddPositionConfig? IndependentAddPositionConfig { get; set; }
        
        /// <summary>
        /// 独立的保盈配置
        /// </summary>
        public ContractProfitProtectionConfig? IndependentProfitProtectionConfig { get; set; }
        
        #endregion
        
        #region 状态信息
        
        /// <summary>
        /// 是否监控中
        /// </summary>
        public bool IsMonitoring { get; set; } = false;
        
        /// <summary>
        /// 保本状态
        /// </summary>
        public ContractTriggerState BreakEvenState { get; set; } = new ContractTriggerState();
        
        /// <summary>
        /// 推仓状态列表
        /// </summary>
        public List<ContractTierState> AddPositionStates { get; set; } = new List<ContractTierState>();
        
        /// <summary>
        /// 保盈状态列表
        /// </summary>
        public List<ContractTierState> ProfitProtectionStates { get; set; } = new List<ContractTierState>();
        
        /// <summary>
        /// 操作历史记录
        /// </summary>
        public List<ContractOperationHistory> OperationHistory { get; set; } = new List<ContractOperationHistory>();
        
        #endregion
        
        #region 计算属性
        
        /// <summary>
        /// 收益率
        /// </summary>
        public decimal ReturnRate => EntryPrice != 0 ? (CurrentPrice - EntryPrice) / EntryPrice * 100 : 0;
        
        /// <summary>
        /// 持仓价值
        /// </summary>
        public decimal PositionValue => Math.Abs(PositionSize) * CurrentPrice;
        
        /// <summary>
        /// 档案标识
        /// </summary>
        public string ProfileId => $"{Symbol}_{Side}_{CreateTime:yyyyMMdd_HHmmss}";
        
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"{Symbol} {Side} {Math.Abs(PositionSize)}";
        
        #endregion
        
        #region 方法
        
        /// <summary>
        /// 更新价格信息
        /// </summary>
        /// <param name="currentPrice">当前价格</param>
        /// <param name="unrealizedPnl">当前浮盈</param>
        public void UpdatePriceInfo(decimal currentPrice, decimal unrealizedPnl)
        {
            CurrentPrice = currentPrice;
            UnrealizedPnl = unrealizedPnl;
            LastUpdateTime = DateTime.Now;
            
            OnPropertyChanged(nameof(CurrentPrice));
            OnPropertyChanged(nameof(UnrealizedPnl));
            OnPropertyChanged(nameof(ReturnRate));
            OnPropertyChanged(nameof(PositionValue));
            OnPropertyChanged(nameof(LastUpdateTime));
        }
        
        /// <summary>
        /// 添加操作历史记录
        /// </summary>
        /// <param name="operation">操作类型</param>
        /// <param name="result">操作结果</param>
        /// <param name="details">详细信息</param>
        public void AddOperationHistory(string operation, string result, string details = "")
        {
            var history = new ContractOperationHistory
            {
                Timestamp = DateTime.Now,
                Operation = operation,
                Result = result,
                Details = details,
                PnlAtTime = UnrealizedPnl,
                PriceAtTime = CurrentPrice
            };
            
            OperationHistory.Add(history);
            
            // 保持历史记录数量在合理范围内
            if (OperationHistory.Count > 100)
            {
                OperationHistory.RemoveAt(0);
            }
            
            OnPropertyChanged(nameof(OperationHistory));
        }
        
        /// <summary>
        /// 重置状态
        /// </summary>
        public void ResetStates()
        {
            BreakEvenState = new ContractTriggerState();
            AddPositionStates.Clear();
            ProfitProtectionStates.Clear();
            
            OnPropertyChanged(nameof(BreakEvenState));
            OnPropertyChanged(nameof(AddPositionStates));
            OnPropertyChanged(nameof(ProfitProtectionStates));
        }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    // 注意：合约配置类已在 ContractConfig.cs 中定义，这里不再重复定义
    
    /// <summary>
    /// 合约触发状态
    /// </summary>
    public class ContractTriggerState
    {
        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;
        
        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime? TriggerTime { get; set; }
        
        /// <summary>
        /// 触发时的价格
        /// </summary>
        public decimal TriggerPrice { get; set; } = 0;
        
        /// <summary>
        /// 触发时的浮盈
        /// </summary>
        public decimal TriggerPnl { get; set; } = 0;
        
        /// <summary>
        /// 执行状态
        /// </summary>
        public string ExecutionStatus { get; set; } = StatusConstants.Waiting; // waiting、executing、executed、failed
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public string ExecutionResult { get; set; } = "";
    }
    
    /// <summary>
    /// 合约阶梯状态
    /// </summary>
    public class ContractTierState : ContractTriggerState
    {
        /// <summary>
        /// 阶梯序号
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 阶梯类型（AddPosition=推仓，ProfitProtection=保盈）
        /// </summary>
        public string TierType { get; set; } = "";
    }
    
    /// <summary>
    /// 合约操作历史记录
    /// </summary>
    public class ContractOperationHistory
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 操作类型
        /// </summary>
        public string Operation { get; set; } = "";
        
        /// <summary>
        /// 操作结果
        /// </summary>
        public string Result { get; set; } = "";
        
        /// <summary>
        /// 详细信息
        /// </summary>
        public string Details { get; set; } = "";
        
        /// <summary>
        /// 操作时的浮盈
        /// </summary>
        public decimal PnlAtTime { get; set; } = 0;
        
        /// <summary>
        /// 操作时的价格
        /// </summary>
        public decimal PriceAtTime { get; set; } = 0;
        
        /// <summary>
        /// 显示文本
        /// </summary>
        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Operation} - {Result} ({Details})";
    }
} 