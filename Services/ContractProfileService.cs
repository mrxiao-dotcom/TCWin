using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 合约档案管理服务 - 负责管理合约档案的CRUD操作和状态跟踪
    /// </summary>
    public class ContractProfileService
    {
        private readonly ILogger<ContractProfileService> _logger;
        private readonly IBinanceService _binanceService;
        private readonly BaseConfigManager _configManager;
        private readonly RiskCapitalService _riskCapitalService;
        private readonly string _profileFilePath;
        private static readonly object _fileLock = new object(); // 🔧 改为静态锁，避免多实例文件访问冲突
        
        /// <summary>
        /// 合约档案列表
        /// </summary>
        public ObservableCollection<ContractProfile> ContractProfiles { get; private set; }
        
        /// <summary>
        /// 档案变化事件
        /// </summary>
        public event EventHandler<ContractProfileChangedEventArgs>? ProfileChanged;
        
        public ContractProfileService(
            ILogger<ContractProfileService> logger,
            IBinanceService binanceService,
            BaseConfigManager configManager,
            RiskCapitalService riskCapitalService)
        {
            _logger = logger;
            _binanceService = binanceService;
            _configManager = configManager;
            _riskCapitalService = riskCapitalService;
            
            _profileFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ContractProfiles.json");
            ContractProfiles = new ObservableCollection<ContractProfile>();
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(_profileFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            
            // 加载档案
            _ = LoadProfilesAsync();
        }
        
        #region 档案管理
        
        /// <summary>
        /// 创建合约档案
        /// </summary>
        /// <param name="position">持仓信息</param>
        /// <param name="baseConfigName">基础配置名称</param>
        /// <returns>创建的档案</returns>
        public async Task<ContractProfile> CreateProfileAsync(PositionInfo position, string baseConfigName)
        {
            try
            {
                // 检查是否已存在相同的档案
                var existingProfile = ContractProfiles.FirstOrDefault(p => 
                    p.Symbol == position.Symbol && p.Side == (position.PositionAmt > 0 ? "LONG" : "SHORT"));
                
                if (existingProfile != null)
                {
                    _logger.LogInformation($"档案已存在: {existingProfile.DisplayName}");
                    return existingProfile;
                }
                
                // 创建新档案
                var profile = new ContractProfile
                {
                    Symbol = position.Symbol,
                    Side = position.PositionAmt > 0 ? "LONG" : "SHORT",
                    PositionSize = position.PositionAmt,
                    EntryPrice = position.EntryPrice,
                    CurrentPrice = position.MarkPrice,
                    UnrealizedPnl = position.UnrealizedProfit,
                    BaseConfigName = baseConfigName,
                    UseIndependentConfig = false,
                    IsMonitoring = false,
                    CreateTime = DateTime.Now,
                    LastUpdateTime = DateTime.Now
                };
                
                // 初始化状态
                await InitializeProfileStatesAsync(profile);
                
                // 添加到集合
                ContractProfiles.Add(profile);
                
                // 保存到文件
                await SaveProfilesAsync();
                
                // 添加操作历史
                profile.AddOperationHistory("创建档案", "成功", $"基础配置: {baseConfigName}");
                
                // 触发事件
                ProfileChanged?.Invoke(this, new ContractProfileChangedEventArgs
                {
                    ChangeType = ProfileChangeType.Created,
                    Profile = profile
                });
                
                _logger.LogInformation($"创建合约档案: {profile.DisplayName}");
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建合约档案失败: {position.Symbol}");
                throw;
            }
        }
        
        /// <summary>
        /// 更新合约档案
        /// </summary>
        /// <param name="profile">档案</param>
        public async Task UpdateProfileAsync(ContractProfile profile)
        {
            try
            {
                profile.LastUpdateTime = DateTime.Now;
                
                // 保存到文件
                await SaveProfilesAsync();
                
                // 触发事件
                ProfileChanged?.Invoke(this, new ContractProfileChangedEventArgs
                {
                    ChangeType = ProfileChangeType.Updated,
                    Profile = profile
                });
                
                _logger.LogDebug($"更新合约档案: {profile.DisplayName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新合约档案失败: {profile.DisplayName}");
                throw;
            }
        }
        
        /// <summary>
        /// 删除合约档案
        /// </summary>
        /// <param name="profileId">档案ID</param>
        public async Task DeleteProfileAsync(string profileId)
        {
            try
            {
                var profile = ContractProfiles.FirstOrDefault(p => p.ProfileId == profileId);
                if (profile == null)
                {
                    throw new ArgumentException($"档案不存在: {profileId}");
                }
                
                ContractProfiles.Remove(profile);
                
                // 保存到文件
                await SaveProfilesAsync();
                
                // 触发事件
                ProfileChanged?.Invoke(this, new ContractProfileChangedEventArgs
                {
                    ChangeType = ProfileChangeType.Deleted,
                    Profile = profile
                });
                
                _logger.LogInformation($"删除合约档案: {profile.DisplayName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除合约档案失败: {profileId}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取合约档案
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="side">方向</param>
        /// <returns>档案</returns>
        public ContractProfile? GetProfile(string symbol, string side)
        {
            return ContractProfiles.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
        }
        
        /// <summary>
        /// 获取所有活跃档案
        /// </summary>
        /// <returns>活跃档案列表</returns>
        public List<ContractProfile> GetActiveProfiles()
        {
            return ContractProfiles.Where(p => p.IsMonitoring).ToList();
        }
        
        #endregion
        
        #region 批量操作
        
        /// <summary>
        /// 从当前持仓批量创建档案
        /// </summary>
        /// <param name="baseConfigName">基础配置名称</param>
        /// <returns>创建的档案数量</returns>
        public async Task<int> CreateProfilesFromCurrentPositionsAsync(string baseConfigName)
        {
            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                var activePositions = positions.Where(p => p.PositionAmt != 0).ToList();
                
                int createdCount = 0;
                
                foreach (var position in activePositions)
                {
                    try
                    {
                        await CreateProfileAsync(position, baseConfigName);
                        createdCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"为持仓创建档案失败: {position.Symbol}");
                    }
                }
                
                _logger.LogInformation($"批量创建档案完成: {createdCount}/{activePositions.Count}");
                return createdCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量创建档案失败");
                throw;
            }
        }
        
        /// <summary>
        /// 批量更新档案价格信息
        /// </summary>
        /// <returns>更新的档案数量</returns>
        public async Task<int> UpdateAllProfilesPricesAsync()
        {
            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                var positionDict = positions.ToDictionary(p => p.Symbol, p => p);
                
                int updatedCount = 0;
                
                foreach (var profile in ContractProfiles)
                {
                    try
                    {
                        if (positionDict.TryGetValue(profile.Symbol, out var position))
                        {
                            // 检查方向是否匹配
                            var currentSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                            if (profile.Side == currentSide)
                            {
                                profile.UpdatePriceInfo(position.MarkPrice, position.UnrealizedProfit);
                                updatedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"更新档案价格失败: {profile.DisplayName}");
                    }
                }
                
                if (updatedCount > 0)
                {
                    await SaveProfilesAsync();
                }
                
                _logger.LogDebug($"批量更新档案价格完成: {updatedCount}个档案");
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新档案价格失败");
                return 0;
            }
        }
        
        #endregion
        
        #region 配置管理
        
        /// <summary>
        /// 设置档案使用独立配置
        /// </summary>
        /// <param name="profile">档案</param>
        /// <param name="useIndependent">是否使用独立配置</param>
        public async Task SetProfileIndependentConfigAsync(ContractProfile profile, bool useIndependent)
        {
            try
            {
                profile.UseIndependentConfig = useIndependent;
                
                if (useIndependent)
                {
                    // 创建独立配置
                    await CreateIndependentConfigAsync(profile);
                    profile.AddOperationHistory("切换配置", "成功", "启用独立配置");
                }
                else
                {
                    // 清除独立配置
                    profile.IndependentBreakEvenConfig = null;
                    profile.IndependentAddPositionConfig = null;
                    profile.IndependentProfitProtectionConfig = null;
                    profile.AddOperationHistory("切换配置", "成功", "使用基础配置");
                }
                
                await UpdateProfileAsync(profile);
                _logger.LogInformation($"档案 {profile.DisplayName} 配置模式已切换: {(useIndependent ? "独立" : "基础")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"设置档案配置模式失败: {profile.DisplayName}");
                throw;
            }
        }
        
        /// <summary>
        /// 创建独立配置
        /// </summary>
        /// <param name="profile">档案</param>
        private async Task CreateIndependentConfigAsync(ContractProfile profile)
        {
            try
            {
                // 获取基础配置
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    throw new InvalidOperationException($"基础配置不存在: {profile.BaseConfigName}");
                }
                
                // 创建独立的保本配置
                if (baseConfig.BreakEvenConfig.IsEnabled)
                {
                    profile.IndependentBreakEvenConfig = new ContractBreakEvenConfig
                    {
                        IsEnabled = true,
                        TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount
                    };
                }
                
                // 创建独立的推仓配置
                if (baseConfig.AddPositionConfig.IsEnabled)
                {
                    profile.IndependentAddPositionConfig = new ContractAddPositionConfig
                    {
                        IsEnabled = true,
                        Tiers = baseConfig.AddPositionConfig.Tiers.Select(t => new ContractAddPositionTier
                        {
                            TierIndex = t.TierIndex,
                            IsEnabled = t.IsEnabled,
                            TriggerProfitAmount = t.TriggerProfitAmount,
                            RiskMultiplier = t.RiskMultiplier,
                            StopLossRatio = t.StopLossRatio,
                            AddPositionQuantity = Math.Abs(profile.PositionSize) * 0.5m, // 默认加仓50%
                            StopLossPrice = 0 // 需要根据实际情况计算
                        }).ToList()
                    };
                }
                
                // 创建独立的保盈配置
                if (baseConfig.ProfitProtectionConfig.IsEnabled)
                {
                    profile.IndependentProfitProtectionConfig = new ContractProfitProtectionConfig
                    {
                        IsEnabled = true,
                        Tiers = baseConfig.ProfitProtectionConfig.Tiers.Select(t => new ContractProfitProtectionTier
                        {
                            TierIndex = t.TierIndex,
                            IsEnabled = t.IsEnabled,
                            TriggerProfitAmount = t.TriggerProfitAmount,
                            ProtectionAmount = t.ProtectionAmount,
                            StopLossPrice = 0 // 需要根据实际情况计算
                        }).ToList()
                    };
                }
                
                _logger.LogInformation($"为档案 {profile.DisplayName} 创建独立配置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建独立配置失败: {profile.DisplayName}");
                throw;
            }
        }
        
        #endregion
        
        #region 状态管理
        
        /// <summary>
        /// 初始化档案状态
        /// </summary>
        /// <param name="profile">档案</param>
        private async Task InitializeProfileStatesAsync(ContractProfile profile)
        {
            try
            {
                // 获取基础配置
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    return;
                }
                
                // 初始化推仓状态
                profile.AddPositionStates.Clear();
                foreach (var tier in baseConfig.AddPositionConfig.Tiers)
                {
                    profile.AddPositionStates.Add(new ContractTierState
                    {
                        TierIndex = tier.TierIndex,
                        TierType = "AddPosition",
                        IsTriggered = false,
                        ExecutionStatus = StatusConstants.Waiting
                    });
                }
                
                // 初始化保盈状态
                profile.ProfitProtectionStates.Clear();
                foreach (var tier in baseConfig.ProfitProtectionConfig.Tiers)
                {
                    profile.ProfitProtectionStates.Add(new ContractTierState
                    {
                        TierIndex = tier.TierIndex,
                        TierType = "ProfitProtection",
                        IsTriggered = false,
                        ExecutionStatus = StatusConstants.Waiting
                    });
                }
                
                _logger.LogDebug($"初始化档案状态: {profile.DisplayName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化档案状态失败: {profile.DisplayName}");
            }
        }
        
        /// <summary>
        /// 更新档案状态
        /// </summary>
        /// <param name="profile">档案</param>
        public async Task UpdateProfileStatesAsync(ContractProfile profile)
        {
            try
            {
                // 获取有效配置
                var (breakEvenConfig, addPositionConfig, profitProtectionConfig) = GetEffectiveConfig(profile);
                
                // 更新保本状态
                if (breakEvenConfig != null && breakEvenConfig.IsEnabled)
                {
                    UpdateBreakEvenState(profile, breakEvenConfig);
                }
                
                // 更新推仓状态
                if (addPositionConfig != null && addPositionConfig.IsEnabled)
                {
                    UpdateAddPositionStates(profile, addPositionConfig);
                }
                
                // 更新保盈状态
                if (profitProtectionConfig != null && profitProtectionConfig.IsEnabled)
                {
                    UpdateProfitProtectionStates(profile, profitProtectionConfig);
                }
                
                await UpdateProfileAsync(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新档案状态失败: {profile.DisplayName}");
            }
        }
        
        /// <summary>
        /// 获取有效配置
        /// </summary>
        /// <param name="profile">档案</param>
        /// <returns>有效配置</returns>
        private (ContractBreakEvenConfig?, ContractAddPositionConfig?, ContractProfitProtectionConfig?) GetEffectiveConfig(ContractProfile profile)
        {
            if (profile.UseIndependentConfig)
            {
                return (profile.IndependentBreakEvenConfig, profile.IndependentAddPositionConfig, profile.IndependentProfitProtectionConfig);
            }
            else
            {
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    return (null, null, null);
                }
                
                // 将基础配置转换为合约配置格式
                var breakEvenConfig = baseConfig.BreakEvenConfig.IsEnabled ? new ContractBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount
                } : null;
                
                var addPositionConfig = baseConfig.AddPositionConfig.IsEnabled ? new ContractAddPositionConfig
                {
                    IsEnabled = true,
                    Tiers = baseConfig.AddPositionConfig.Tiers.Select(t => new ContractAddPositionTier
                    {
                        TierIndex = t.TierIndex,
                        IsEnabled = t.IsEnabled,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        RiskMultiplier = t.RiskMultiplier,
                        StopLossRatio = t.StopLossRatio,
                        AddPositionQuantity = Math.Abs(profile.PositionSize) * 0.5m
                    }).ToList()
                } : null;
                
                var profitProtectionConfig = baseConfig.ProfitProtectionConfig.IsEnabled ? new ContractProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = baseConfig.ProfitProtectionConfig.Tiers.Select(t => new ContractProfitProtectionTier
                    {
                        TierIndex = t.TierIndex,
                        IsEnabled = t.IsEnabled,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        ProtectionAmount = t.ProtectionAmount
                    }).ToList()
                } : null;
                
                return (breakEvenConfig, addPositionConfig, profitProtectionConfig);
            }
        }
        
        /// <summary>
        /// 更新保本状态
        /// </summary>
        private void UpdateBreakEvenState(ContractProfile profile, ContractBreakEvenConfig config)
        {
            var currentPnl = profile.UnrealizedPnl;
            var triggerAmount = config.TriggerProfitAmount;
            
            if (!profile.BreakEvenState.IsTriggered && currentPnl >= triggerAmount)
            {
                profile.BreakEvenState.IsTriggered = true;
                profile.BreakEvenState.TriggerTime = DateTime.Now;
                profile.BreakEvenState.TriggerPrice = profile.CurrentPrice;
                profile.BreakEvenState.TriggerPnl = currentPnl;
                profile.BreakEvenState.ExecutionStatus = "触发中";
                
                profile.AddOperationHistory("保本触发", "成功", $"触发金额: {triggerAmount:F2}U, 当前浮盈: {currentPnl:F2}U");
            }
        }
        
        /// <summary>
        /// 更新推仓状态
        /// </summary>
        private void UpdateAddPositionStates(ContractProfile profile, ContractAddPositionConfig config)
        {
            var currentPnl = profile.UnrealizedPnl;
            
            foreach (var tier in config.Tiers)
            {
                var state = profile.AddPositionStates.FirstOrDefault(s => s.TierIndex == tier.TierIndex);
                if (state == null) continue;
                
                if (!state.IsTriggered && currentPnl >= tier.TriggerProfitAmount)
                {
                    state.IsTriggered = true;
                    state.TriggerTime = DateTime.Now;
                    state.TriggerPrice = profile.CurrentPrice;
                    state.TriggerPnl = currentPnl;
                    state.ExecutionStatus = "触发中";
                    
                    profile.AddOperationHistory("推仓触发", "成功", $"阶梯{tier.TierIndex}: 触发金额{tier.TriggerProfitAmount:F2}U");
                }
            }
        }
        
        /// <summary>
        /// 更新保盈状态
        /// </summary>
        private void UpdateProfitProtectionStates(ContractProfile profile, ContractProfitProtectionConfig config)
        {
            var currentPnl = profile.UnrealizedPnl;
            
            foreach (var tier in config.Tiers)
            {
                var state = profile.ProfitProtectionStates.FirstOrDefault(s => s.TierIndex == tier.TierIndex);
                if (state == null) continue;
                
                if (!state.IsTriggered && currentPnl >= tier.TriggerProfitAmount)
                {
                    state.IsTriggered = true;
                    state.TriggerTime = DateTime.Now;
                    state.TriggerPrice = profile.CurrentPrice;
                    state.TriggerPnl = currentPnl;
                    state.ExecutionStatus = "触发中";
                    
                    profile.AddOperationHistory("保盈触发", "成功", $"阶梯{tier.TierIndex}: 触发金额{tier.TriggerProfitAmount:F2}U");
                }
            }
        }
        
        #endregion
        
        #region 持久化
        
        /// <summary>
        /// 加载档案列表
        /// </summary>
        private async Task LoadProfilesAsync()
        {
            try
            {
                if (!File.Exists(_profileFilePath))
                {
                    return;
                }
                
                await Task.Run(() =>
                {
                    lock (_fileLock)
                    {
                        // 🔧 添加重试机制和更安全的文件访问
                        var retryCount = 0;
                        const int maxRetries = 3;
                        
                        while (retryCount < maxRetries)
                        {
                            try
                            {
                                var json = File.ReadAllText(_profileFilePath);
                                var profiles = JsonSerializer.Deserialize<List<ContractProfile>>(json, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true,
                                    WriteIndented = true
                                });
                                
                                if (profiles != null)
                                {
                                    ContractProfiles.Clear();
                                    foreach (var profile in profiles)
                                    {
                                        ContractProfiles.Add(profile);
                                    }
                                    
                                    _logger.LogInformation($"加载了 {profiles.Count} 个合约档案");
                                }
                                break; // 成功则退出重试循环
                            }
                            catch (IOException ioEx) when (retryCount < maxRetries - 1)
                            {
                                retryCount++;
                                _logger.LogWarning($"文件读取失败，重试 {retryCount}/{maxRetries}: {ioEx.Message}");
                                Task.Delay(100 * retryCount).Wait(); // 递增延迟
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载档案文件失败");
            }
        }
        
        /// <summary>
        /// 保存档案列表
        /// </summary>
        private async Task SaveProfilesAsync()
        {
            try
            {
                var profiles = ContractProfiles.ToList();
                var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });
                
                await Task.Run(() =>
                {
                    lock (_fileLock)
                    {
                        // 🔧 添加重试机制和更安全的文件写入
                        var retryCount = 0;
                        const int maxRetries = 3;
                        
                        while (retryCount < maxRetries)
                        {
                            try
                            {
                                File.WriteAllText(_profileFilePath, json);
                                break; // 成功则退出重试循环
                            }
                            catch (IOException ioEx) when (retryCount < maxRetries - 1)
                            {
                                retryCount++;
                                _logger.LogWarning($"文件写入失败，重试 {retryCount}/{maxRetries}: {ioEx.Message}");
                                Task.Delay(100 * retryCount).Wait(); // 递增延迟
                            }
                        }
                    }
                });
                
                _logger.LogDebug($"保存了 {profiles.Count} 个合约档案到文件");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存档案文件失败");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 合约档案变化事件参数
    /// </summary>
    public class ContractProfileChangedEventArgs : EventArgs
    {
        public ProfileChangeType ChangeType { get; set; }
        public ContractProfile Profile { get; set; } = null!;
    }
    
    /// <summary>
    /// 档案变化类型
    /// </summary>
    public enum ProfileChangeType
    {
        Created,
        Updated,
        Deleted,
        StateChanged
    }
} 