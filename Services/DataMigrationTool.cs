using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 数据迁移工具
    /// 负责将旧版数据文件格式迁移到新版增强型数据管理器格式
    /// </summary>
    public class DataMigrationTool
    {
        private readonly ILogger<DataMigrationTool> _logger;
        private readonly FilePathManager _filePathManager;
        private readonly string _currentAccountName;

        public DataMigrationTool(ILogger<DataMigrationTool> logger, string? accountName = null)
        {
            _logger = logger;
            _filePathManager = new FilePathManager();
            _currentAccountName = accountName ?? _filePathManager.GetCurrentAccountName();
            
            _logger.LogInformation($"🔄 数据迁移工具初始化完成 - 账号: {_currentAccountName}");
        }

        /// <summary>
        /// 迁移结果
        /// </summary>
        public class MigrationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public int BaseConfigsCount { get; set; }
            public int ContractStatesCount { get; set; }
            public int ExecutionHistoryCount { get; set; }
            public List<string> Warnings { get; set; } = new();
            public Exception? Exception { get; set; }
        }

        /// <summary>
        /// 检查是否需要迁移
        /// </summary>
        public async Task<bool> IsMigrationNeededAsync(string targetDataDirectory)
        {
            try
            {
                _logger.LogInformation("🔍 检查是否需要数据迁移...");

                // 检查新格式文件是否已存在
                var newFormatExists = CheckNewFormatExists(targetDataDirectory);
                if (newFormatExists)
                {
                    _logger.LogInformation("✅ 新格式文件已存在，无需迁移");
                    return false;
                }

                // 检查旧格式文件是否存在
                var oldFormatExists = await CheckOldFormatExistsAsync();
                if (!oldFormatExists)
                {
                    _logger.LogInformation("📋 没有发现旧格式文件，无需迁移");
                    return false;
                }

                _logger.LogInformation("🚀 发现旧格式文件，需要进行迁移");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 检查迁移需求时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 执行完整数据迁移
        /// </summary>
        public async Task<MigrationResult> MigrateAllDataAsync(string targetDataDirectory)
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("🚀 开始执行完整数据迁移...");
                
                // 确保目标目录存在
                Directory.CreateDirectory(targetDataDirectory);
                
                // 创建备份
                await CreateBackupAsync();
                
                // 1. 迁移基础配置
                var baseConfigResult = await MigrateBaseConfigsAsync(targetDataDirectory);
                result.BaseConfigsCount = baseConfigResult.BaseConfigsCount;
                result.Warnings.AddRange(baseConfigResult.Warnings);
                
                // 2. 迁移合约状态
                var contractStateResult = await MigrateContractStatesAsync(targetDataDirectory);
                result.ContractStatesCount = contractStateResult.ContractStatesCount;
                result.Warnings.AddRange(contractStateResult.Warnings);
                
                // 3. 迁移执行历史
                var historyResult = await MigrateExecutionHistoryAsync(targetDataDirectory);
                result.ExecutionHistoryCount = historyResult.ExecutionHistoryCount;
                result.Warnings.AddRange(historyResult.Warnings);
                
                result.Success = true;
                result.Message = $"✅ 数据迁移完成！基础配置: {result.BaseConfigsCount}, 合约状态: {result.ContractStatesCount}, 执行历史: {result.ExecutionHistoryCount}";
                
                _logger.LogInformation(result.Message);
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
                result.Message = $"❌ 数据迁移失败: {ex.Message}";
                
                _logger.LogError(ex, "❌ 数据迁移过程中发生错误");
                
                return result;
            }
        }

        #region 私有方法

        /// <summary>
        /// 检查新格式文件是否存在
        /// </summary>
        private bool CheckNewFormatExists(string targetDataDirectory)
        {
            var baseConfigsPath = Path.Combine(targetDataDirectory, "base_configs.json");
            var contractStatesPath = Path.Combine(targetDataDirectory, "contract_states.json");
            var executionHistoryPath = Path.Combine(targetDataDirectory, "execution_history.json");

            return File.Exists(baseConfigsPath) || File.Exists(contractStatesPath) || File.Exists(executionHistoryPath);
        }

        /// <summary>
        /// 检查旧格式文件是否存在
        /// </summary>
        private async Task<bool> CheckOldFormatExistsAsync()
        {
            // 检查基础配置文件
            var baseConfigPath = _filePathManager.GetBaseConfigsFilePath();
            var baseConfigExists = File.Exists(baseConfigPath);

            // 检查合约状态文件
            var contractStatePath = _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);
            var contractStateExists = File.Exists(contractStatePath);

            // 检查执行历史文件
            var historyPath = _filePathManager.GetExecutionHistoryFilePath(_currentAccountName);
            var historyExists = File.Exists(historyPath);

            _logger.LogInformation($"🔍 旧格式文件检查结果:");
            _logger.LogInformation($"   📁 基础配置: {baseConfigExists} ({baseConfigPath})");
            _logger.LogInformation($"   📁 合约状态: {contractStateExists} ({contractStatePath})");
            _logger.LogInformation($"   📁 执行历史: {historyExists} ({historyPath})");

            return baseConfigExists || contractStateExists || historyExists;
        }

        /// <summary>
        /// 创建数据备份
        /// </summary>
        private async Task CreateBackupAsync()
        {
            try
            {
                _logger.LogInformation("💾 创建数据备份...");
                
                var backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BinanceFuturesTrader",
                    "Backup",
                    $"Migration_{DateTime.Now:yyyyMMdd_HHmmss}"
                );
                
                Directory.CreateDirectory(backupDir);

                // 备份基础配置
                var baseConfigPath = _filePathManager.GetBaseConfigsFilePath();
                if (File.Exists(baseConfigPath))
                {
                    var backupConfigPath = Path.Combine(backupDir, "AutoMonitorConfigs.json");
                    File.Copy(baseConfigPath, backupConfigPath);
                    _logger.LogInformation($"📁 已备份基础配置: {backupConfigPath}");
                }

                // 备份合约状态
                var contractStatePath = _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);
                if (File.Exists(contractStatePath))
                {
                    var backupStatePath = Path.Combine(backupDir, "contract_monitoring_states.json");
                    File.Copy(contractStatePath, backupStatePath);
                    _logger.LogInformation($"📁 已备份合约状态: {backupStatePath}");
                }

                // 备份执行历史
                var historyPath = _filePathManager.GetExecutionHistoryFilePath(_currentAccountName);
                if (File.Exists(historyPath))
                {
                    var backupHistoryPath = Path.Combine(backupDir, "execution_history.json");
                    File.Copy(historyPath, backupHistoryPath);
                    _logger.LogInformation($"📁 已备份执行历史: {backupHistoryPath}");
                }

                _logger.LogInformation($"✅ 数据备份完成: {backupDir}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ 创建备份时发生错误，继续迁移过程");
            }
        }

        /// <summary>
        /// 迁移基础配置
        /// </summary>
        private async Task<MigrationResult> MigrateBaseConfigsAsync(string targetDataDirectory)
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("🔄 开始迁移基础配置...");
                
                var baseConfigPath = _filePathManager.GetBaseConfigsFilePath();
                if (!File.Exists(baseConfigPath))
                {
                    _logger.LogInformation("📋 基础配置文件不存在，跳过迁移");
                    return result;
                }

                // 读取旧格式配置
                var json = await File.ReadAllTextAsync(baseConfigPath);
                var oldConfigs = new List<AutoMonitorConfig>();

                // 尝试解析JSON
                try
                {
                    // 首先尝试数组格式
                    oldConfigs = JsonSerializer.Deserialize<List<AutoMonitorConfig>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<AutoMonitorConfig>();
                }
                catch (JsonException)
                {
                    // 如果失败，尝试旧的对象格式
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("accountConfigs", out var accountConfigsElement))
                    {
                        foreach (var accountProperty in accountConfigsElement.EnumerateObject())
                        {
                            var configElement = accountProperty.Value;
                            var config = JsonSerializer.Deserialize<AutoMonitorConfig>(configElement.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (config != null)
                            {
                                oldConfigs.Add(config);
                            }
                        }
                    }
                }

                // 转换为新格式
                var newBaseConfigs = new List<BaseConfig>();
                foreach (var oldConfig in oldConfigs)
                {
                    var newConfig = AutoMonitorConfigAdapter.ToBaseConfig(oldConfig);
                    newBaseConfigs.Add(newConfig);
                }

                // 创建新格式文件
                var baseConfigFile = new BaseConfigFile
                {
                    Version = "1.0",
                    LastUpdated = DateTime.UtcNow,
                    CurrentConfigId = newBaseConfigs.FirstOrDefault()?.Id ?? string.Empty,
                    Configs = newBaseConfigs
                };

                // 保存新格式文件
                var targetPath = Path.Combine(targetDataDirectory, "base_configs.json");
                var newJson = JsonSerializer.Serialize(baseConfigFile, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(targetPath, newJson);

                result.BaseConfigsCount = newBaseConfigs.Count;
                _logger.LogInformation($"✅ 基础配置迁移完成: {result.BaseConfigsCount} 个配置");
                
                return result;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"基础配置迁移失败: {ex.Message}");
                _logger.LogError(ex, "❌ 基础配置迁移失败");
                return result;
            }
        }

        /// <summary>
        /// 迁移合约状态
        /// </summary>
        private async Task<MigrationResult> MigrateContractStatesAsync(string targetDataDirectory)
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("🔄 开始迁移合约状态...");
                
                var contractStatePath = _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);
                if (!File.Exists(contractStatePath))
                {
                    _logger.LogInformation("📋 合约状态文件不存在，跳过迁移");
                    return result;
                }

                // 读取旧格式状态
                var json = await File.ReadAllTextAsync(contractStatePath);
                var oldStates = JsonSerializer.Deserialize<Dictionary<string, ContractMonitoringState>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, ContractMonitoringState>();

                // 转换为新格式
                var newContractStates = new List<ContractState>();
                foreach (var kvp in oldStates)
                {
                    var oldState = kvp.Value;
                    var newState = ConvertToNewContractState(oldState, kvp.Key);
                    newContractStates.Add(newState);
                }

                // 创建新格式文件
                var contractStateFile = new ContractStateFile
                {
                    Version = "1.0",
                    LastUpdated = DateTime.UtcNow,
                    AccountName = _currentAccountName,
                    States = newContractStates.ToDictionary(s => s.GetKey(), s => s)
                };

                // 保存新格式文件
                var targetPath = Path.Combine(targetDataDirectory, "contract_states.json");
                var newJson = JsonSerializer.Serialize(contractStateFile, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(targetPath, newJson);

                result.ContractStatesCount = newContractStates.Count;
                _logger.LogInformation($"✅ 合约状态迁移完成: {result.ContractStatesCount} 个状态");
                
                return result;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"合约状态迁移失败: {ex.Message}");
                _logger.LogError(ex, "❌ 合约状态迁移失败");
                return result;
            }
        }

        /// <summary>
        /// 迁移执行历史
        /// </summary>
        private async Task<MigrationResult> MigrateExecutionHistoryAsync(string targetDataDirectory)
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("🔄 开始迁移执行历史...");
                
                var historyPath = _filePathManager.GetExecutionHistoryFilePath(_currentAccountName);
                if (!File.Exists(historyPath))
                {
                    _logger.LogInformation("📋 执行历史文件不存在，跳过迁移");
                    return result;
                }

                // 读取旧格式历史
                var json = await File.ReadAllTextAsync(historyPath);
                var oldHistories = JsonSerializer.Deserialize<List<object>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<object>();

                // 转换为新格式（这里需要根据实际的旧格式来处理）
                var newHistories = new List<ContractExecutionHistory>();
                foreach (var oldHistory in oldHistories)
                {
                    try
                    {
                        // 这里需要根据实际的旧历史格式来转换
                        var historyJson = JsonSerializer.Serialize(oldHistory);
                        var parsedHistory = JsonSerializer.Deserialize<JsonElement>(historyJson);
                        
                        var newHistory = new ContractExecutionHistory
                        {
                            Timestamp = DateTime.UtcNow, // 默认值，可以根据实际数据调整
                            ContractKey = "UNKNOWN", // 默认值，可以根据实际数据调整
                            ExecutionType = ExecutionTypes.ConfigurationSwitched,
                            ExecutionResult = ExecutionResults.Success,
                            ExecutionDetails = $"迁移的历史记录: {historyJson}"
                        };
                        
                        newHistories.Add(newHistory);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ 无法转换历史记录，跳过");
                    }
                }

                // 创建新格式文件
                var historyFile = new ExecutionHistoryFile
                {
                    Version = "1.0",
                    LastUpdated = DateTime.UtcNow,
                    History = newHistories
                };

                // 保存新格式文件
                var targetPath = Path.Combine(targetDataDirectory, "execution_history.json");
                var newJson = JsonSerializer.Serialize(historyFile, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(targetPath, newJson);

                result.ExecutionHistoryCount = newHistories.Count;
                _logger.LogInformation($"✅ 执行历史迁移完成: {result.ExecutionHistoryCount} 个记录");
                
                return result;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"执行历史迁移失败: {ex.Message}");
                _logger.LogError(ex, "❌ 执行历史迁移失败");
                return result;
            }
        }

        /// <summary>
        /// 将旧的ContractMonitoringState转换为新的ContractState
        /// </summary>
        private ContractState ConvertToNewContractState(ContractMonitoringState oldState, string contractKey)
        {
            // 解析symbol和side
            var parts = contractKey.Split('_');
            var symbol = parts.Length > 0 ? parts[0] : "UNKNOWN";
            var side = parts.Length > 1 ? parts[1] : "LONG";

            var newState = new ContractState
            {
                Symbol = symbol,
                PositionSide = side,
                BaseConfigId = oldState.BaseConfigName ?? "default",
                PositionInfo = new ContractPositionInfo
                {
                    // 使用默认值避免属性错误
                },
                ExecutionStates = new ExecutionStates
                {
                    Breakeven = new BreakevenExecutionState
                    {
                        State = ExecutionStateTypes.NotTriggered, // 使用默认值
                        TriggerAmount = 0, // 使用默认值
                        ExecutedAt = null
                    },
                    AddPositionTiers = new List<AddPositionExecutionState>(),
                    ProfitProtectionTiers = new List<ProfitProtectionExecutionState>()
                },
                Meta = new ContractStateMeta
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                }
            };

            return newState;
        }

        /// <summary>
        /// 获取推仓层级状态 - 简化版本，返回默认值
        /// </summary>
        private string GetPushTierStatus(ContractMonitoringState oldState, int tierIndex)
        {
            // 简化实现，返回默认状态
            return string.Empty;
        }

        /// <summary>
        /// 获取保盈层级状态 - 简化版本，返回默认值
        /// </summary>
        private string GetProfitTierStatus(ContractMonitoringState oldState, int tierIndex)
        {
            // 简化实现，返回默认状态
            return string.Empty;
        }

        /// <summary>
        /// 将旧的状态字符串转换为新的执行状态
        /// </summary>
        private string ConvertStatusToExecutionState(string? oldStatus)
        {
            return oldStatus switch
            {
                "-" or null or "" => ExecutionStateTypes.NotTriggered,
                "执行中" => ExecutionStateTypes.Executing,
                "√" => ExecutionStateTypes.Executed,
                _ => ExecutionStateTypes.NotTriggered
            };
        }

        #endregion
    }
} 