using System;
using System.Collections.Generic;
using System.Linq;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 合约执行状态管理器 - 解决多合约状态冲突问题
    /// </summary>
    public class ContractExecutionState
    {
        /// <summary>
        /// 合约标识（Symbol_PositionSide）
        /// </summary>
        public string ContractKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓方向
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 执行阶梯状态字典 - 每个阶梯独立管理
        /// </summary>
        public Dictionary<int, TierExecutionState> TierStates { get; set; } = new();
        
        /// <summary>
        /// 自动保本执行状态
        /// </summary>
        public ExecutionRecord BreakEvenState { get; set; } = new();
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 是否活跃
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 检查指定类型和阶梯是否已触发
        /// </summary>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引（可选）</param>
        /// <returns>是否已触发</returns>
        public bool IsTriggered(ExecutionType executionType, int? tierIndex = null)
        {
            switch (executionType)
            {
                case ExecutionType.BreakEven:
                    return BreakEvenState.IsExecuted;
                    
                case ExecutionType.AddPosition:
                case ExecutionType.ProfitProtection:
                    if (tierIndex.HasValue && TierStates.TryGetValue(tierIndex.Value, out var tierState))
                    {
                        return tierState.GetExecutionRecord(executionType).IsExecuted;
                    }
                    return false;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// 标记指定执行为已触发
        /// </summary>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引（可选）</param>
        /// <param name="triggerPnl">触发时的浮盈</param>
        /// <param name="isSuccess">是否执行成功</param>
        /// <param name="message">执行消息</param>
        public void MarkAsTriggered(ExecutionType executionType, int? tierIndex, decimal triggerPnl, bool isSuccess, string message = "")
        {
            LastUpdateTime = DateTime.Now;
            
            switch (executionType)
            {
                case ExecutionType.BreakEven:
                    BreakEvenState = new ExecutionRecord
                    {
                        ExecutionType = executionType,
                        IsExecuted = true,
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = triggerPnl,
                        IsSuccess = isSuccess,
                        Message = message
                    };
                    break;
                    
                case ExecutionType.AddPosition:
                case ExecutionType.ProfitProtection:
                    if (tierIndex.HasValue)
                    {
                        if (!TierStates.ContainsKey(tierIndex.Value))
                        {
                            TierStates[tierIndex.Value] = new TierExecutionState { TierIndex = tierIndex.Value };
                        }
                        
                        TierStates[tierIndex.Value].SetExecutionRecord(executionType, new ExecutionRecord
                        {
                            ExecutionType = executionType,
                            TierIndex = tierIndex,
                            IsExecuted = true,
                            ExecutionTime = DateTime.Now,
                            TriggerPnl = triggerPnl,
                            IsSuccess = isSuccess,
                            Message = message
                        });
                    }
                    break;
            }
        }

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        /// <returns>执行统计</returns>
        public ContractExecutionStats GetExecutionStats()
        {
            var stats = new ContractExecutionStats
            {
                ContractKey = ContractKey,
                Symbol = Symbol,
                PositionSide = PositionSide,
                BreakEvenExecuted = BreakEvenState.IsExecuted
            };

            foreach (var tierState in TierStates.Values)
            {
                if (tierState.AddPositionRecord.IsExecuted)
                    stats.AddPositionTiersExecuted++;
                
                if (tierState.ProfitProtectionRecord.IsExecuted)
                    stats.ProfitProtectionTiersExecuted++;
            }

            stats.TotalExecutions = (stats.BreakEvenExecuted ? 1 : 0) + 
                                  stats.AddPositionTiersExecuted + 
                                  stats.ProfitProtectionTiersExecuted;

            return stats;
        }

        /// <summary>
        /// 重置执行状态（用于重新开仓）
        /// </summary>
        public void ResetExecutionState()
        {
            BreakEvenState = new ExecutionRecord();
            TierStates.Clear();
            LastUpdateTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 阶梯执行状态
    /// </summary>
    public class TierExecutionState
    {
        /// <summary>
        /// 阶梯索引
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 推仓执行记录
        /// </summary>
        public ExecutionRecord AddPositionRecord { get; set; } = new();
        
        /// <summary>
        /// 保盈止损执行记录
        /// </summary>
        public ExecutionRecord ProfitProtectionRecord { get; set; } = new();

        /// <summary>
        /// 获取指定类型的执行记录
        /// </summary>
        /// <param name="type">执行类型</param>
        /// <returns>执行记录</returns>
        public ExecutionRecord GetExecutionRecord(ExecutionType type)
        {
            return type switch
            {
                ExecutionType.AddPosition => AddPositionRecord,
                ExecutionType.ProfitProtection => ProfitProtectionRecord,
                _ => new ExecutionRecord()
            };
        }

        /// <summary>
        /// 设置指定类型的执行记录
        /// </summary>
        /// <param name="type">执行类型</param>
        /// <param name="record">执行记录</param>
        public void SetExecutionRecord(ExecutionType type, ExecutionRecord record)
        {
            switch (type)
            {
                case ExecutionType.AddPosition:
                    AddPositionRecord = record;
                    break;
                case ExecutionType.ProfitProtection:
                    ProfitProtectionRecord = record;
                    break;
            }
        }
    }

    /// <summary>
    /// 执行记录
    /// </summary>
    public class ExecutionRecord
    {
        /// <summary>
        /// 执行类型
        /// </summary>
        public ExecutionType ExecutionType { get; set; }
        
        /// <summary>
        /// 阶梯索引（如果适用）
        /// </summary>
        public int? TierIndex { get; set; }
        
        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }
        
        /// <summary>
        /// 触发时的浮盈
        /// </summary>
        public decimal TriggerPnl { get; set; }
        
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool IsSuccess { get; set; } = false;
        
        /// <summary>
        /// 执行消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 0;
    }

    /// <summary>
    /// 执行类型枚举
    /// </summary>
    public enum ExecutionType
    {
        BreakEven,           // 自动保本
        AddPosition,         // 推仓
        ProfitProtection     // 保盈止损
    }

    /// <summary>
    /// 合约执行统计
    /// </summary>
    public class ContractExecutionStats
    {
        public string ContractKey { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public bool BreakEvenExecuted { get; set; }
        public int AddPositionTiersExecuted { get; set; }
        public int ProfitProtectionTiersExecuted { get; set; }
        public int TotalExecutions { get; set; }
        
        /// <summary>
        /// 执行进度百分比
        /// </summary>
        public double ExecutionProgress => TotalExecutions > 0 ? 
            (double)TotalExecutions / (1 + 4 + 3) * 100 : 0; // 1个保本+4个推仓+3个保盈
    }

    /// <summary>
    /// 合约状态管理器 - 统一管理所有合约的执行状态
    /// </summary>
    public class ContractStateManager
    {
        private readonly Dictionary<string, ContractExecutionState> _contractStates = new();
        private readonly object _lock = new();

        /// <summary>
        /// 获取或创建合约状态
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <returns>合约执行状态</returns>
        public ContractExecutionState GetOrCreateContractState(string symbol, string positionSide)
        {
            var contractKey = $"{symbol}_{positionSide}";
            
            lock (_lock)
            {
                if (!_contractStates.TryGetValue(contractKey, out var state))
                {
                    state = new ContractExecutionState
                    {
                        ContractKey = contractKey,
                        Symbol = symbol,
                        PositionSide = positionSide
                    };
                    _contractStates[contractKey] = state;
                }
                
                state.LastUpdateTime = DateTime.Now;
                return state;
            }
        }

        /// <summary>
        /// 检查是否已触发
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引</param>
        /// <returns>是否已触发</returns>
        public bool IsTriggered(string symbol, string positionSide, ExecutionType executionType, int? tierIndex = null)
        {
            var state = GetOrCreateContractState(symbol, positionSide);
            return state.IsTriggered(executionType, tierIndex);
        }

        /// <summary>
        /// 标记为已触发
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引</param>
        /// <param name="triggerPnl">触发浮盈</param>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="message">消息</param>
        public void MarkAsTriggered(string symbol, string positionSide, ExecutionType executionType, 
            int? tierIndex, decimal triggerPnl, bool isSuccess, string message = "")
        {
            var state = GetOrCreateContractState(symbol, positionSide);
            state.MarkAsTriggered(executionType, tierIndex, triggerPnl, isSuccess, message);
        }

        /// <summary>
        /// 清理指定合约的状态
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        public void CleanupContractState(string symbol, string positionSide)
        {
            var contractKey = $"{symbol}_{positionSide}";
            
            lock (_lock)
            {
                if (_contractStates.TryGetValue(contractKey, out var state))
                {
                    state.ResetExecutionState();
                }
            }
        }

        /// <summary>
        /// 移除指定合约的状态
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        public void RemoveContractState(string symbol, string positionSide)
        {
            var contractKey = $"{symbol}_{positionSide}";
            
            lock (_lock)
            {
                _contractStates.Remove(contractKey);
            }
        }

        /// <summary>
        /// 获取所有合约的执行统计
        /// </summary>
        /// <returns>执行统计列表</returns>
        public List<ContractExecutionStats> GetAllExecutionStats()
        {
            lock (_lock)
            {
                return _contractStates.Values
                    .Where(s => s.IsActive)
                    .Select(s => s.GetExecutionStats())
                    .OrderBy(s => s.Symbol)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取活跃合约数量
        /// </summary>
        /// <returns>活跃合约数量</returns>
        public int GetActiveContractCount()
        {
            lock (_lock)
            {
                return _contractStates.Values.Count(s => s.IsActive);
            }
        }
    }
} 