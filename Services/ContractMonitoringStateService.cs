using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 合约监控状态服务 - 管理统一监控状态文件（支持多账号隔离）
    /// 替代position_profiles.json，提供完整的配置+状态管理
    /// </summary>
    public class ContractMonitoringStateService
    {
        private readonly FilePathManager _filePathManager;
        private readonly ILogger<ContractMonitoringStateService> _logger;
        private readonly ContractMonitoringStateGenerator _stateGenerator;
        private readonly BaseConfigManager _configManager;
        private readonly string _currentAccountName;
        
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ContractMonitoringStateService(
            ILogger<ContractMonitoringStateService> logger,
            BaseConfigManager configManager,
            FilePathManager? filePathManager = null,
            string? accountName = null)
        {
            _logger = logger;
            _configManager = configManager;
            _filePathManager = filePathManager ?? new FilePathManager();
            _currentAccountName = accountName ?? _filePathManager.GetCurrentAccountName();
            
            _stateGenerator = new ContractMonitoringStateGenerator(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateGenerator>.Instance);
            
            _logger.LogDebug($"📁 统一监控状态服务初始化 - 账号: {_currentAccountName}");
            _logger.LogDebug($"📁 状态文件路径: {GetStateFilePath()}");
        }
        
        /// <summary>
        /// 获取当前账号的状态文件路径
        /// </summary>
        private string GetStateFilePath()
        {
            return _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);
        }

        /// <summary>
        /// 确保合约状态的唯一性，去除重复记录
        /// </summary>
        private Dictionary<string, ContractMonitoringState> EnsureUniqueContractStates(Dictionary<string, ContractMonitoringState> states)
        {
            var uniqueStates = new Dictionary<string, ContractMonitoringState>();
            var duplicateKeys = new List<string>();
            
            foreach (var kvp in states)
            {
                var contractKey = kvp.Key;
                var state = kvp.Value;
                
                // 🔧 【关键修复】完全重构标准化逻辑
                var symbol = state.Symbol.ToUpperInvariant();
                
                // 🔧 强制统一方向标准化：不依赖state.PositionSide，而是重新判断
                string side;
                if (state.CurrentQuantity != 0)
                {
                    // 根据持仓数量重新判断方向
                    side = state.CurrentQuantity > 0 ? "LONG" : "SHORT";
                }
                else
                {
                    // 如果没有持仓数量，尝试从PositionSide推断，但强制标准化
                    var originalSide = state.PositionSide?.ToUpperInvariant();
                    if (originalSide == "BOTH")
                    {
                        // BOTH类型需要额外信息判断，暂时使用LONG
                        side = "LONG";
                        _logger.LogWarning($"⚠️ 遇到BOTH类型持仓，自动设置为LONG: {symbol}");
                    }
                    else if (originalSide == "SHORT")
                    {
                        side = "SHORT";
                    }
                    else
                    {
                        side = "LONG"; // 默认LONG
                    }
                }
                
                var normalizedKey = $"{symbol}_{side}";
                
                if (uniqueStates.ContainsKey(normalizedKey))
                {
                    duplicateKeys.Add(contractKey);
                    _logger.LogCritical($"   ⚠️ 发现重复合约状态: {contractKey} → 标准化为 {normalizedKey}");
                    
                    // 保留更新时间较晚的状态
                    var existingState = uniqueStates[normalizedKey];
                    if (state.LastUpdateTime > existingState.LastUpdateTime)
                    {
                        // 🔧 【重要】更新保留状态的键名格式
                        state.Symbol = symbol;
                        state.PositionSide = side;
                        uniqueStates[normalizedKey] = state;
                        _logger.LogCritical($"   🔄 保留较新的状态: {state.LastUpdateTime:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                else
                {
                    // 🔧 【重要】标准化保存状态的字段
                    state.Symbol = symbol;
                    state.PositionSide = side;
                    uniqueStates[normalizedKey] = state;
                }
            }
            
            if (duplicateKeys.Count > 0)
            {
                _logger.LogCritical($"   🔄 去重完成: 移除了 {duplicateKeys.Count} 个重复记录");
                foreach (var key in duplicateKeys)
                {
                    _logger.LogCritical($"     - 移除重复: {key}");
                }
            }
            
            return uniqueStates;
        }

        /// <summary>
        /// 保存统一监控状态（主要方法）
        /// </summary>
        public void SaveMonitoringStates(Dictionary<string, ContractMonitoringState> states)
        {
            try
            {
                var stateFilePath = GetStateFilePath();
                _logger.LogCritical($"🔍【文件保存诊断】开始保存状态到: {stateFilePath}");
                _logger.LogCritical($"   📂 账号: {_currentAccountName}");
                _logger.LogCritical($"   📂 文件夹路径: {Path.GetDirectoryName(stateFilePath)}");
                _logger.LogCritical($"   📄 文件名: {Path.GetFileName(stateFilePath)}");
                _logger.LogCritical($"   📊 状态数量: {states?.Count ?? 0}");
                
                if (states == null || !states.Any())
                {
                    _logger.LogCritical("💡 没有监控状态需要保存，退出");
                    return;
                }
                
                // 🔧 【重要修复】去重处理：确保同一个合约不会有多条记录
                var originalCount = states.Count;
                states = EnsureUniqueContractStates(states);
                
                if (states.Count != originalCount)
                {
                    _logger.LogCritical($"   🔄 去重处理: 原始{originalCount}条 → 去重后{states.Count}条");
                }
                
                // 检查文件存在性和修改时间（保存前）
                var existsBefore = File.Exists(stateFilePath);
                var lastWriteBefore = existsBefore ? File.GetLastWriteTime(stateFilePath) : DateTime.MinValue;
                _logger.LogCritical($"   📋 保存前文件状态: 存在={existsBefore}, 最后修改={lastWriteBefore:yyyy-MM-dd HH:mm:ss}");
                
                // 🔧 【关键修复】保存所有状态，不只是活跃状态
                // 用户手动编辑的状态应该被保存，即使合约当前不活跃
                _logger.LogCritical($"   🎯 保存去重后状态数量: {states.Count}");
                
                // 🔍 详细检查每个状态的ExecutionState
                foreach (var kvp in states)
                {
                    var state = kvp.Value;
                    _logger.LogCritical($"   📋 合约 {kvp.Key}: 保本ExecutionState={(int)state.BreakEvenConfig.ExecutionState}({state.BreakEvenConfig.ExecutionState}), IsExecuted={state.BreakEvenConfig.IsExecuted}");
                    if (state.BreakEvenConfig.ExecutionState != ExecutionState.NotTriggered)
                    {
                        _logger.LogCritical($"   ⚡ 发现非默认状态: {kvp.Key} - {state.BreakEvenConfig.ExecutionState}");
                    }
                }
                
                // 检查是否有ExecutionState更新的状态
                var hasExecutedStates = states.Values.Any(s => 
                    s.BreakEvenConfig.ExecutionState != ExecutionState.NotTriggered ||
                    s.AddPositionConfig.Tiers.Any(t => t.ExecutionState != ExecutionState.NotTriggered) ||
                    s.ProfitProtectionConfig.Tiers.Any(t => t.ExecutionState != ExecutionState.NotTriggered));
                
                if (hasExecutedStates)
                {
                    _logger.LogCritical($"   ⚡ 检测到已执行状态，强制保存所有状态");
                }
                
                var json = JsonSerializer.Serialize(states, _jsonOptions);
                
                // 🔍 检查序列化后的JSON内容
                if (json.Contains("\"executionState\""))
                {
                    _logger.LogCritical($"   🔍 JSON中包含executionState字段");
                    // 搜索executionState的值
                    var executionStateMatches = System.Text.RegularExpressions.Regex.Matches(json, @"""executionState"":\s*(\d+)");
                    foreach (System.Text.RegularExpressions.Match match in executionStateMatches)
                    {
                        _logger.LogCritical($"   📊 JSON中发现executionState值: {match.Groups[1].Value}");
                    }
                }
                _logger.LogCritical($"   📝 JSON长度: {json.Length} 字符");
                _logger.LogCritical($"   📝 JSON前100字符: {(json.Length > 100 ? json.Substring(0, 100) + "..." : json)}");
                
                File.WriteAllText(stateFilePath, json);
                
                // 检查文件存在性和修改时间（保存后）
                var existsAfter = File.Exists(stateFilePath);
                var lastWriteAfter = existsAfter ? File.GetLastWriteTime(stateFilePath) : DateTime.MinValue;
                var fileSizeAfter = existsAfter ? new FileInfo(stateFilePath).Length : 0;
                
                _logger.LogCritical($"   ✅ 保存后文件状态: 存在={existsAfter}, 最后修改={lastWriteAfter:yyyy-MM-dd HH:mm:ss}, 大小={fileSizeAfter}字节");
                _logger.LogInformation($"💾 已保存统一监控状态: {states.Count} 个");
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"❌【文件保存失败】{ex.GetType().Name}: {ex.Message}");
                _logger.LogCritical($"   📍 堆栈跟踪: {ex.StackTrace}");
                _logger.LogError(ex, "❌ 保存统一监控状态失败");
            }
        }

        /// <summary>
        /// 加载统一监控状态
        /// </summary>
        public Dictionary<string, ContractMonitoringState> LoadMonitoringStates()
        {
            try
            {
                var stateFilePath = GetStateFilePath();
                _logger.LogCritical($"🔍【文件加载诊断】文件路径: {stateFilePath}");
                _logger.LogCritical($"🔍【文件加载诊断】当前账号: {_currentAccountName}");
                
                if (!File.Exists(stateFilePath))
                {
                    _logger.LogCritical($"🔍【文件加载诊断】❌ 文件不存在: {stateFilePath}");
                    return new Dictionary<string, ContractMonitoringState>();
                }
                
                _logger.LogCritical($"🔍【文件加载诊断】✅ 文件存在");
                var fileInfo = new FileInfo(stateFilePath);
                _logger.LogCritical($"🔍【文件加载诊断】文件大小: {fileInfo.Length} 字节");
                _logger.LogCritical($"🔍【文件加载诊断】最后修改: {fileInfo.LastWriteTime}");
                
                var json = File.ReadAllText(stateFilePath);
                _logger.LogCritical($"🔍【文件加载诊断】JSON长度: {json?.Length.ToString() ?? "null"}");
                _logger.LogCritical($"🔍【文件加载诊断】JSON前100字符: {(string.IsNullOrEmpty(json) ? "null" : json.Substring(0, Math.Min(100, json.Length)))}");
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogCritical($"🔍【文件加载诊断】❌ JSON为空");
                    return new Dictionary<string, ContractMonitoringState>();
                }
                
                _logger.LogCritical($"🔍【文件加载诊断】开始反序列化...");
                var states = JsonSerializer.Deserialize<Dictionary<string, ContractMonitoringState>>(json, _jsonOptions) 
                    ?? new Dictionary<string, ContractMonitoringState>();
                
                _logger.LogCritical($"🔍【文件加载诊断】✅ 反序列化成功，状态数量: {states.Count}");
                foreach (var kvp in states)
                {
                    _logger.LogCritical($"🔍【文件加载诊断】找到状态键: {kvp.Key}");
                }
                
                _logger.LogInformation($"📂 已加载统一监控状态: {states.Count} 个");
                return states;
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"🔍【文件加载诊断】❌ 加载异常: {ex.Message}");
                _logger.LogCritical($"🔍【文件加载诊断】异常类型: {ex.GetType().Name}");
                _logger.LogCritical($"🔍【文件加载诊断】异常堆栈: {ex.StackTrace}");
                _logger.LogError(ex, "❌ 加载统一监控状态失败");
                return new Dictionary<string, ContractMonitoringState>();
            }
        }

        /// <summary>
        /// 生成或更新合约监控状态
        /// </summary>
        public ContractMonitoringState GenerateOrUpdateState(
            PositionInfo position, 
            string baseConfigName,
            Dictionary<string, ContractMonitoringState> existingStates)
        {
            // 🔧 【统一修复】使用标准化键名生成
            var symbol = position.Symbol.ToUpperInvariant();
            var side = position.PositionAmt > 0 ? "LONG" : "SHORT";
            var key = $"{symbol}_{side}";
            
            // 获取基础配置
            var baseConfig = _configManager.GetConfiguration(baseConfigName);
            if (baseConfig == null)
            {
                _logger.LogWarning($"⚠️ 未找到基础配置: {baseConfigName}");
                // 使用默认配置
                baseConfig = _configManager.Configurations.FirstOrDefault() ?? new AutoMonitorConfig();
            }
            
            // 获取现有状态或创建新状态
            existingStates.TryGetValue(key, out var existingState);
            
            // 生成监控状态
            var state = _stateGenerator.GenerateMonitoringState(baseConfig, position, existingState);
            
            // 🔧 【关键】确保状态字段标准化
            state.Symbol = symbol;
            state.PositionSide = side;
            
            _logger.LogDebug($"🔄 已生成/更新监控状态: {key}");
            return state;
        }

        /// <summary>
        /// 从持仓列表生成完整的监控状态
        /// </summary>
        public Dictionary<string, ContractMonitoringState> GenerateMonitoringStatesFromPositions(
            List<PositionInfo> positions, 
            string defaultConfigName = "智能默认配置")
        {
            var existingStates = LoadMonitoringStates();
            var newStates = new Dictionary<string, ContractMonitoringState>();
            
            foreach (var position in positions.Where(p => Math.Abs(p.PositionAmt) > 0))
            {
                // 🔧 【强化修复】完全标准化键名生成
                var symbol = position.Symbol.ToUpperInvariant();
                
                // 🔧 【关键】不依赖API的PositionSide，完全基于PositionAmt重新判断
                string side;
                if (Math.Abs(position.PositionAmt) > 0)
                {
                    side = position.PositionAmt > 0 ? "LONG" : "SHORT";
                }
                else
                {
                    // 如果PositionAmt为0，不应该处理，跳过
                    _logger.LogWarning($"⚠️ 跳过零持仓: {symbol}");
                    continue;
                }
                
                var key = $"{symbol}_{side}";
                
                // 🔧 【加强检查】如果已存在相同键名，进行详细日志记录
                if (newStates.ContainsKey(key))
                {
                    _logger.LogWarning($"   ⚠️ 检测到重复合约生成: {key}");
                    _logger.LogWarning($"     - 原始Symbol: {position.Symbol}");
                    _logger.LogWarning($"     - 标准化Symbol: {symbol}");
                    _logger.LogWarning($"     - PositionAmt: {position.PositionAmt}");
                    _logger.LogWarning($"     - 推断Side: {side}");
                    continue;
                }
                
                var state = GenerateOrUpdateState(position, defaultConfigName, existingStates);
                
                // 🔧 【重要】确保生成的状态使用标准化字段
                state.Symbol = symbol;
                state.PositionSide = side;
                
                newStates[key] = state;
                
                _logger.LogDebug($"   ✅ 生成/更新状态: {key}");
            }
            
            // 标记不再存在的持仓为非活跃
            foreach (var kvp in existingStates)
            {
                if (!newStates.ContainsKey(kvp.Key))
                {
                    kvp.Value.IsActive = false;
                    kvp.Value.LastUpdateTime = DateTime.Now;
                    newStates[kvp.Key] = kvp.Value;
                    _logger.LogDebug($"❌ 持仓已平仓，标记为非活跃: {kvp.Key}");
                }
            }
            
            _logger.LogInformation($"🎯 已生成完整监控状态: {newStates.Count(s => s.Value.IsActive)} 个活跃状态");
            return newStates;
        }

        /// <summary>
        /// 更新执行状态
        /// </summary>
        public void UpdateExecutionStatus(
            string contractKey,
            string operationType,
            int? tierIndex,
            bool isSuccess,
            decimal triggerPnl,
            string result)
        {
            try
            {
                _logger.LogCritical($"🔍【状态更新开始】{contractKey} {operationType}_{tierIndex} = {isSuccess}");
                
                var states = LoadMonitoringStates();
                _logger.LogCritical($"   📂 加载到 {states.Count} 个状态");
                
                if (states.TryGetValue(contractKey, out var state))
                {
                    _logger.LogCritical($"   ✅ 找到合约状态: {contractKey}");
                    _logger.LogCritical($"   📊 更新前状态: BreakEven={state.BreakEvenConfig.IsExecuted}, AddPosition阶梯数={state.AddPositionConfig.Tiers.Count}, ProfitProtection阶梯数={state.ProfitProtectionConfig.Tiers.Count}");
                    
                    _stateGenerator.UpdateExecutionStatus(state, operationType, tierIndex, isSuccess, triggerPnl, result);
                    
                    _logger.LogCritical($"   📊 更新后状态: BreakEven={state.BreakEvenConfig.IsExecuted}");
                    if (operationType.ToLower() == "addposition" && tierIndex.HasValue)
                    {
                        var tier = state.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        _logger.LogCritical($"   📊 推仓阶梯{tierIndex}状态: {tier?.IsExecuted}");
                    }
                    if (operationType.ToLower() == "profitprotection" && tierIndex.HasValue)
                    {
                        var tier = state.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        _logger.LogCritical($"   📊 保盈阶梯{tierIndex}状态: {tier?.IsExecuted}");
                    }
                    
                    states[contractKey] = state;
                    
                    _logger.LogCritical($"   💾 开始保存到文件...");
                    SaveMonitoringStates(states);
                    _logger.LogCritical($"   ✅ 文件保存完成");
                    
                    // 🔧 【关键验证】立即重新读取文件验证状态是否真的被保存
                    _logger.LogCritical($"   🔍 立即验证文件保存结果...");
                    var verifyStates = LoadMonitoringStates();
                    if (verifyStates.TryGetValue(contractKey, out var verifyState))
                    {
                        bool finalStatus = false;
                        switch (operationType.ToLower())
                        {
                            case "breakeven":
                            case "保本":
                                finalStatus = verifyState.BreakEvenConfig.IsExecuted;
                                _logger.LogCritical($"   ✅ 验证结果 - 保本状态: {finalStatus}");
                                break;
                            case "addposition":
                            case "推仓":
                                if (tierIndex.HasValue)
                                {
                                    var verifyTier = verifyState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                                    finalStatus = verifyTier?.IsExecuted ?? false;
                                    _logger.LogCritical($"   ✅ 验证结果 - 推仓阶梯{tierIndex}状态: {finalStatus}");
                                }
                                break;
                            case "profitprotection":
                            case "保盈":
                                if (tierIndex.HasValue)
                                {
                                    var verifyTier = verifyState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                                    finalStatus = verifyTier?.IsExecuted ?? false;
                                    _logger.LogCritical($"   ✅ 验证结果 - 保盈阶梯{tierIndex}状态: {finalStatus}");
                                }
                                break;
                        }
                        
                        if (finalStatus == isSuccess)
                        {
                            _logger.LogCritical($"   🎉 状态更新验证成功！文件中的状态已正确更新为: {finalStatus}");
                        }
                        else
                        {
                            _logger.LogCritical($"   ❌ 状态更新验证失败！期望: {isSuccess}, 实际: {finalStatus}");
                        }
                    }
                    else
                    {
                        _logger.LogCritical($"   ❌ 验证失败：重新读取文件后找不到合约: {contractKey}");
                    }
                    
                    _logger.LogInformation($"✅ 已更新执行状态: {contractKey} {operationType}_{tierIndex}");
                }
                else
                {
                    _logger.LogCritical($"   ❌ 未找到合约状态: {contractKey}");
                    _logger.LogCritical($"   📝 可用的合约键: {string.Join(", ", states.Keys)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新执行状态失败: {contractKey} {operationType}_{tierIndex}");
            }
        }

        /// <summary>
        /// 更新执行状态为执行中
        /// </summary>
        public void UpdateExecutionStatusToExecuting(
            string contractKey,
            string operationType,
            int? tierIndex,
            decimal triggerPnl,
            string result)
        {
            try
            {
                _logger.LogCritical($"🔍【状态更新开始】{contractKey} {operationType}_{tierIndex} = Executing");
                
                var states = LoadMonitoringStates();
                _logger.LogCritical($"   📂 加载到 {states.Count} 个状态");
                
                if (states.TryGetValue(contractKey, out var state))
                {
                    _logger.LogCritical($"   ✅ 找到合约状态: {contractKey}");
                    
                    // 直接设置为Executing状态
                    var now = DateTime.Now;
                    switch (operationType.ToLower())
                    {
                        case "breakeven":
                        case "保本":
                            _logger.LogCritical($"   📊 保本状态更新: 更新前={state.BreakEvenConfig.ExecutionState}");
                            state.BreakEvenConfig.ExecutionState = ExecutionState.Executing;
                            state.BreakEvenConfig.ExecutionTime = now;
                            state.BreakEvenConfig.ExecutionPnl = triggerPnl;
                            state.BreakEvenConfig.ExecutionResult = result;
                            _logger.LogCritical($"   📊 保本状态更新: 更新后={state.BreakEvenConfig.ExecutionState}");
                            break;

                        case "addposition":
                        case "推仓":
                            if (tierIndex.HasValue)
                            {
                                var tier = state.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                                if (tier != null)
                                {
                                    _logger.LogCritical($"   📊 推仓阶梯{tierIndex}状态更新: 更新前={tier.ExecutionState}");
                                    tier.ExecutionState = ExecutionState.Executing;
                                    tier.ExecutionTime = now;
                                    tier.ExecutionPnl = triggerPnl;
                                    tier.ExecutionResult = result;
                                    _logger.LogCritical($"   📊 推仓阶梯{tierIndex}状态更新: 更新后={tier.ExecutionState}");
                                }
                            }
                            break;

                        case "profitprotection":
                        case "保盈":
                            if (tierIndex.HasValue)
                            {
                                var tier = state.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                                if (tier != null)
                                {
                                    _logger.LogCritical($"   📊 保盈阶梯{tierIndex}状态更新: 更新前={tier.ExecutionState}");
                                    tier.ExecutionState = ExecutionState.Executing;
                                    tier.ExecutionTime = now;
                                    tier.ExecutionPnl = triggerPnl;
                                    tier.ExecutionResult = result;
                                    _logger.LogCritical($"   📊 保盈阶梯{tierIndex}状态更新: 更新后={tier.ExecutionState}");
                                }
                            }
                            break;
                    }
                    
                    states[contractKey] = state;
                    
                    _logger.LogCritical($"   💾 开始保存到文件...");
                    SaveMonitoringStates(states);
                    _logger.LogCritical($"   ✅ 文件保存完成");
                    
                    _logger.LogInformation($"⚡ 已更新执行状态为执行中: {contractKey} {operationType}_{tierIndex}");
                }
                else
                {
                    _logger.LogCritical($"   ❌ 未找到合约状态: {contractKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新执行状态为执行中失败: {contractKey} {operationType}_{tierIndex}");
            }
        }

        /// <summary>
        /// 检查是否已执行
        /// </summary>
        public bool IsExecuted(string contractKey, string operationType, int? tierIndex = null)
        {
            var states = LoadMonitoringStates();
            if (states.TryGetValue(contractKey, out var state))
            {
                return _stateGenerator.IsExecuted(state, operationType, tierIndex);
            }
            return false;
        }

        /// <summary>
        /// 获取特定合约的监控状态
        /// </summary>
        public ContractMonitoringState? GetMonitoringState(string contractKey)
        {
            var states = LoadMonitoringStates();
            return states.TryGetValue(contractKey, out var state) ? state : null;
        }

        /// <summary>
        /// 获取所有活跃的监控状态
        /// </summary>
        public Dictionary<string, ContractMonitoringState> GetActiveMonitoringStates()
        {
            var states = LoadMonitoringStates();
            return states.Where(kvp => kvp.Value.IsActive)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// 清理非活跃状态（可选）
        /// </summary>
        public void CleanupInactiveStates(TimeSpan olderThan)
        {
            var states = LoadMonitoringStates();
            var cutoffTime = DateTime.Now - olderThan;
            var initialCount = states.Count;
            
            var activeStates = states.Where(kvp => 
                kvp.Value.IsActive || kvp.Value.LastUpdateTime > cutoffTime)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            if (activeStates.Count < initialCount)
            {
                SaveMonitoringStates(activeStates);
                _logger.LogInformation($"🧹 已清理旧状态: {initialCount - activeStates.Count} 个");
            }
        }

        /// <summary>
        /// 🔧 重要方法：切换合约配置（从基础模板同步配置数据，保留执行状态）
        /// </summary>
        public void SwitchContractConfiguration(string contractKey, string newBaseConfigName)
        {
            var states = LoadMonitoringStates();
            if (!states.TryGetValue(contractKey, out var existingState))
            {
                _logger.LogWarning($"⚠️ 未找到合约状态: {contractKey}");
                return;
            }

            // 获取新的基础配置模板
            var newBaseConfig = _configManager.GetConfiguration(newBaseConfigName);
            if (newBaseConfig == null)
            {
                _logger.LogWarning($"⚠️ 未找到基础配置模板: {newBaseConfigName}");
                return;
            }

            // 保存现有的执行状态
            var oldBreakEvenExecutionState = existingState.BreakEvenConfig;
            var oldAddPositionExecutionStates = existingState.AddPositionConfig.Tiers.ToDictionary(t => t.TierIndex, t => t);
            var oldProfitProtectionExecutionStates = existingState.ProfitProtectionConfig.Tiers.ToDictionary(t => t.TierIndex, t => t);

            // 🔄 从新的基础配置模板生成新的配置数据
            var mockPosition = new PositionInfo
            {
                Symbol = existingState.Symbol,
                PositionAmt = existingState.PositionSide == "LONG" ? existingState.CurrentQuantity : -existingState.CurrentQuantity,
                EntryPrice = existingState.CurrentEntryPrice,
                MarkPrice = existingState.CurrentMarkPrice,
                UnrealizedProfit = existingState.CurrentUnrealizedPnl
            };

            var newState = _stateGenerator.GenerateMonitoringState(newBaseConfig, mockPosition, null);
            
            // 📋 保留重要的状态信息
            newState.ArchiveId = existingState.ArchiveId;
            newState.CreateTime = existingState.CreateTime;
            newState.CurrentQuantity = existingState.CurrentQuantity;
            newState.CurrentEntryPrice = existingState.CurrentEntryPrice;
            newState.CurrentMarkPrice = existingState.CurrentMarkPrice;
            newState.CurrentUnrealizedPnl = existingState.CurrentUnrealizedPnl;
            newState.InitialQuantity = existingState.InitialQuantity;
            newState.InitialEntryPrice = existingState.InitialEntryPrice;
            newState.IsActive = existingState.IsActive;
            newState.ExecutionHistories = existingState.ExecutionHistories;

            // 🔄 恢复执行状态（保留已执行的状态）
            // 保本执行状态
            if (oldBreakEvenExecutionState.IsExecuted)
            {
                newState.BreakEvenConfig.ExecutionState = oldBreakEvenExecutionState.ExecutionState;
                newState.BreakEvenConfig.ExecutionTime = oldBreakEvenExecutionState.ExecutionTime;
                newState.BreakEvenConfig.ExecutionPnl = oldBreakEvenExecutionState.ExecutionPnl;
                newState.BreakEvenConfig.ExecutionResult = oldBreakEvenExecutionState.ExecutionResult;
            }

            // 推仓执行状态
            foreach (var tier in newState.AddPositionConfig.Tiers)
            {
                if (oldAddPositionExecutionStates.TryGetValue(tier.TierIndex, out var oldTier) && oldTier.IsExecuted)
                {
                    tier.ExecutionState = oldTier.ExecutionState;
                    tier.ExecutionTime = oldTier.ExecutionTime;
                    tier.ExecutionPnl = oldTier.ExecutionPnl;
                    tier.ExecutionResult = oldTier.ExecutionResult;
                    tier.AddPositionQuantity = oldTier.AddPositionQuantity;
                    tier.StopLossPrice = oldTier.StopLossPrice;
                }
            }

            // 保盈执行状态
            foreach (var tier in newState.ProfitProtectionConfig.Tiers)
            {
                if (oldProfitProtectionExecutionStates.TryGetValue(tier.TierIndex, out var oldTier) && oldTier.IsExecuted)
                {
                    tier.ExecutionState = oldTier.ExecutionState;
                    tier.ExecutionTime = oldTier.ExecutionTime;
                    tier.ExecutionPnl = oldTier.ExecutionPnl;
                    tier.ExecutionResult = oldTier.ExecutionResult;
                    tier.StopLossPrice = oldTier.StopLossPrice;
                }
            }

            // 更新状态并保存
            states[contractKey] = newState;
            SaveMonitoringStates(states);

            _logger.LogInformation($"🔄 已切换合约配置: {contractKey} → {newBaseConfigName}（保留执行状态）");
        }

        /// <summary>
        /// 🔧 批量切换所有合约配置
        /// </summary>
        public void SwitchAllContractsConfiguration(string newBaseConfigName)
        {
            var states = LoadMonitoringStates();
            var activeContracts = states.Where(kvp => kvp.Value.IsActive).ToList();

            foreach (var kvp in activeContracts)
            {
                SwitchContractConfiguration(kvp.Key, newBaseConfigName);
            }

            _logger.LogInformation($"🔄 已批量切换所有合约配置 → {newBaseConfigName}");
        }
    }
} 