using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 状态调试服务 - 专门用于诊断状态记录和检查问题
    /// </summary>
    public class StateDebugService
    {
        private readonly SimpleStateManager? _stateManager;
        private readonly AutoMonitorPersistenceService _persistenceService;
        private readonly ILogger<StateDebugService> _logger;

        public StateDebugService(
            SimpleStateManager? stateManager,
            AutoMonitorPersistenceService persistenceService,
            ILogger<StateDebugService> logger)
        {
            _stateManager = stateManager;
            _persistenceService = persistenceService;
            _logger = logger;
        }

        /// <summary>
        /// 完整诊断指定合约的状态记录和检查过程
        /// </summary>
        public void DiagnoseContractState(string symbol, string positionSide, string operationType = "AddPosition", int? tierIndex = 1)
        {
            _logger.LogCritical($"🔍 ===== 开始状态诊断: {symbol}_{positionSide} =====");
            
            try
            {
                // 1. 检查内存中的状态管理器
                DiagnoseMemoryState(symbol, positionSide, operationType, tierIndex);
                
                // 2. 检查持久化文件
                DiagnosePersistentState(symbol, positionSide, operationType, tierIndex);
                
                // 3. 测试状态记录过程
                TestStateRecording(symbol, positionSide, operationType, tierIndex);
                
                // 4. 测试状态检查过程
                TestStateChecking(symbol, positionSide, operationType, tierIndex);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 状态诊断过程中出现异常");
            }
            
            _logger.LogCritical($"🔍 ===== 状态诊断完成: {symbol}_{positionSide} =====");
        }

        /// <summary>
        /// 诊断内存中的状态
        /// </summary>
        private void DiagnoseMemoryState(string symbol, string positionSide, string operationType, int? tierIndex)
        {
            _logger.LogCritical($"📊 【内存状态诊断】");
            
            if (_stateManager == null)
            {
                _logger.LogCritical($"   ❌ StateManager为空，无法检查内存状态");
                return;
            }

            // 构建预期的键值
            var expectedKey = $"{symbol}_{positionSide}_{operationType}";
            if (tierIndex.HasValue)
            {
                expectedKey += $"_{tierIndex}";
            }

            _logger.LogCritical($"   🔧 预期键值: {expectedKey}");

            // 检查状态管理器中的档案
            var profiles = _stateManager.GetAllPositionProfiles();
            var profileKey = $"{symbol}_{positionSide}";

            if (profiles.TryGetValue(profileKey, out var profile))
            {
                _logger.LogCritical($"   ✅ 找到档案: {profileKey}");
                _logger.LogCritical($"   📝 档案创建时间: {profile.CreateTime}");
                _logger.LogCritical($"   📝 档案更新时间: {profile.LastUpdateTime}");
                _logger.LogCritical($"   📝 触发记录数量: {profile.TriggerRecords.Count}");

                foreach (var trigger in profile.TriggerRecords)
                {
                    _logger.LogCritical($"      🎯 触发记录: {trigger.Key} -> 执行状态: {trigger.Value.IsExecuted}");
                }

                // 检查特定的触发记录
                if (profile.TriggerRecords.TryGetValue(expectedKey, out var targetRecord))
                {
                    _logger.LogCritical($"   ✅ 找到目标记录: {expectedKey}");
                    _logger.LogCritical($"      触发时间: {targetRecord.TriggerTime}");
                    _logger.LogCritical($"      执行状态: {targetRecord.IsExecuted}");
                    _logger.LogCritical($"      触发浮盈: {targetRecord.TriggerPnl}");
                }
                else
                {
                    _logger.LogCritical($"   ❌ 未找到目标记录: {expectedKey}");
                }
            }
            else
            {
                _logger.LogCritical($"   ❌ 未找到档案: {profileKey}");
            }
        }

        /// <summary>
        /// 诊断持久化文件状态
        /// </summary>
        private void DiagnosePersistentState(string symbol, string positionSide, string operationType, int? tierIndex)
        {
            _logger.LogCritical($"💾 【持久化状态诊断】");
            
            try
            {
                var profiles = _persistenceService.LoadPositionProfiles();
                var profileKey = $"{symbol}_{positionSide}";
                
                if (profiles.TryGetValue(profileKey, out var profile))
                {
                    _logger.LogCritical($"   ✅ 持久化文件中找到档案: {profileKey}");
                    _logger.LogCritical($"   📝 触发记录数量: {profile.TriggerRecords.Count}");
                    
                    foreach (var trigger in profile.TriggerRecords)
                    {
                        _logger.LogCritical($"      🎯 持久化记录: {trigger.Key} -> 执行状态: {trigger.Value.IsExecuted}");
                    }
                }
                else
                {
                    _logger.LogCritical($"   ❌ 持久化文件中未找到档案: {profileKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 读取持久化文件失败");
            }
        }

        /// <summary>
        /// 测试状态记录过程
        /// </summary>
        private void TestStateRecording(string symbol, string positionSide, string operationType, int? tierIndex)
        {
            _logger.LogCritical($"📝 【状态记录测试】");
            
            if (_stateManager == null)
            {
                _logger.LogCritical($"   ❌ StateManager为空，无法测试记录");
                return;
            }

            try
            {
                var testPnl = 150.00m;
                _logger.LogCritical($"   🎯 测试记录状态: {symbol}_{positionSide}_{operationType}_{tierIndex}");
                
                // 记录测试状态
                _stateManager.RecordOperationExecution(symbol, positionSide, operationType, testPnl, true, "测试记录", tierIndex);
                _logger.LogCritical($"   ✅ 记录完成");
                
                // 立即检查是否记录成功
                var isRecorded = _stateManager.IsOperationExecuted(symbol, positionSide, operationType, tierIndex);
                _logger.LogCritical($"   🔍 记录验证: {(isRecorded ? "成功" : "失败")}");
                
                // 保存到持久化
                _stateManager.SaveToPersistence();
                _logger.LogCritical($"   💾 已保存到持久化存储");
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 状态记录测试失败");
            }
        }

        /// <summary>
        /// 测试状态检查过程
        /// </summary>
        private void TestStateChecking(string symbol, string positionSide, string operationType, int? tierIndex)
        {
            _logger.LogCritical($"🔍 【状态检查测试】");
            
            if (_stateManager == null)
            {
                _logger.LogCritical($"   ❌ StateManager为空，无法测试检查");
                return;
            }

            try
            {
                // 测试状态检查
                var isExecuted = _stateManager.IsOperationExecuted(symbol, positionSide, operationType, tierIndex);
                _logger.LogCritical($"   🔍 状态检查结果: {(isExecuted ? "已执行" : "未执行")}");
                
                // 显示构建的键值过程
                var profiles = _stateManager.GetAllPositionProfiles();
                var profileKey = $"{symbol}_{positionSide}";
                
                if (profiles.TryGetValue(profileKey, out var profile))
                {
                    var triggerKey = $"{symbol}_{positionSide}_{operationType}";
                    if (tierIndex.HasValue)
                    {
                        triggerKey += $"_{tierIndex}";
                    }
                    
                    _logger.LogCritical($"   🔧 检查键值构建过程:");
                    _logger.LogCritical($"      档案键: {profileKey}");
                    _logger.LogCritical($"      触发键: {triggerKey}");
                    _logger.LogCritical($"      档案存在: {true}");
                    _logger.LogCritical($"      触发记录存在: {profile.TriggerRecords.ContainsKey(triggerKey)}");
                }
                else
                {
                    _logger.LogCritical($"   ❌ 档案不存在，检查失败: {profileKey}");
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 状态检查测试失败");
            }
        }

        /// <summary>
        /// 清理测试状态
        /// </summary>
        public void CleanupTestState(string symbol, string positionSide)
        {
            _logger.LogCritical($"🧹 【清理测试状态】 {symbol}_{positionSide}");
            
            try
            {
                _persistenceService.CleanupContractHistory(symbol, positionSide, "测试清理");
                _logger.LogCritical($"   ✅ 测试状态已清理");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清理测试状态失败");
            }
        }
    }
} 