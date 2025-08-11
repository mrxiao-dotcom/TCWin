using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 文件持久化管理器
    /// 负责安全的文件读写操作，支持原子写入和错误恢复
    /// </summary>
    public class FilePersistenceManager
    {
        private readonly ILogger<FilePersistenceManager> _logger;
        private readonly string _dataDirectory;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        // 文件名常量
        private const string BaseConfigsFileName = "base_configs.json";
        private const string ContractStatesFileName = "contract_states.json";
        private const string ExecutionHistoryFileName = "execution_history.json";

        public FilePersistenceManager(ILogger<FilePersistenceManager> logger, string? dataDirectory = null)
        {
            _logger = logger;
            _dataDirectory = dataDirectory ?? GetDefaultDataDirectory();
            
            // 确保数据目录存在
            Directory.CreateDirectory(_dataDirectory);
            
            // JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            _logger.LogInformation($"📁 文件持久化管理器初始化完成，数据目录: {_dataDirectory}");
        }

        #region 公共API

        /// <summary>
        /// 立即保存数据（根据触发类型）
        /// </summary>
        public async Task SaveImmediately(SaveTrigger trigger, params object[] data)
        {
            try
            {
                _logger.LogDebug($"🔄 触发立即保存: {trigger}");

                switch (trigger)
                {
                    case SaveTrigger.ExecutionCompleted:
                        if (data.Length >= 2 && data[0] is ContractStateFile contractStates && data[1] is ExecutionHistoryFile executionHistory)
                        {
                            await SaveContractStatesAsync(contractStates);
                            await SaveExecutionHistoryAsync(executionHistory);
                        }
                        break;

                    case SaveTrigger.ConfigurationChanged:
                        if (data.Length >= 1 && data[0] is BaseConfigFile baseConfigs)
                        {
                            await SaveBaseConfigsAsync(baseConfigs);
                        }
                        break;

                    case SaveTrigger.ConfigurationSwitched:
                        if (data.Length >= 2 && data[0] is BaseConfigFile baseConfigs2 && data[1] is ContractStateFile contractStates2)
                        {
                            await SaveBaseConfigsAsync(baseConfigs2);
                            await SaveContractStatesAsync(contractStates2);
                        }
                        break;

                    case SaveTrigger.PositionClosed:
                        if (data.Length >= 1 && data[0] is ContractStateFile contractStates3)
                        {
                            await SaveContractStatesAsync(contractStates3);
                        }
                        break;

                    case SaveTrigger.PeriodicSave:
                        if (data.Length >= 3 && 
                            data[0] is BaseConfigFile baseConfigs4 && 
                            data[1] is ContractStateFile contractStates4 && 
                            data[2] is ExecutionHistoryFile executionHistory4)
                        {
                            await SaveAllAsync(baseConfigs4, contractStates4, executionHistory4);
                        }
                        break;
                }

                _logger.LogDebug($"✅ 立即保存完成: {trigger}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 立即保存失败: {trigger}");
                throw;
            }
        }

        /// <summary>
        /// 保存所有数据文件
        /// </summary>
        public async Task SaveAllAsync(BaseConfigFile baseConfigs, ContractStateFile contractStates, ExecutionHistoryFile executionHistory)
        {
            var tasks = new List<Task>
            {
                SaveBaseConfigsAsync(baseConfigs),
                SaveContractStatesAsync(contractStates),
                SaveExecutionHistoryAsync(executionHistory)
            };

            await Task.WhenAll(tasks);
            _logger.LogInformation("📄 所有数据文件保存完成");
        }

        /// <summary>
        /// 加载基础配置文件
        /// </summary>
        public async Task<BaseConfigFile> LoadBaseConfigsAsync()
        {
            return await LoadJsonFileAsync<BaseConfigFile>(BaseConfigsFileName) 
                   ?? CreateDefaultBaseConfigFile();
        }

        /// <summary>
        /// 加载合约状态文件
        /// </summary>
        public async Task<ContractStateFile> LoadContractStatesAsync()
        {
            return await LoadJsonFileAsync<ContractStateFile>(ContractStatesFileName) 
                   ?? CreateDefaultContractStateFile();
        }

        /// <summary>
        /// 加载执行历史文件
        /// </summary>
        public async Task<ExecutionHistoryFile> LoadExecutionHistoryAsync()
        {
            return await LoadJsonFileAsync<ExecutionHistoryFile>(ExecutionHistoryFileName) 
                   ?? CreateDefaultExecutionHistoryFile();
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FileExists(string fileName)
        {
            var filePath = Path.Combine(_dataDirectory, fileName);
            return File.Exists(filePath);
        }

        /// <summary>
        /// 获取文件路径
        /// </summary>
        public string GetFilePath(string fileName)
        {
            return Path.Combine(_dataDirectory, fileName);
        }

        #endregion

        #region 私有保存方法

        /// <summary>
        /// 保存基础配置文件
        /// </summary>
        private async Task SaveBaseConfigsAsync(BaseConfigFile baseConfigs)
        {
            baseConfigs.LastUpdated = DateTime.UtcNow;
            await WriteJsonFileAsync(BaseConfigsFileName, baseConfigs);
            _logger.LogDebug($"💾 基础配置文件已保存: {baseConfigs.Configs.Count} 个配置");
        }

        /// <summary>
        /// 保存合约状态文件
        /// </summary>
        private async Task SaveContractStatesAsync(ContractStateFile contractStates)
        {
            contractStates.LastUpdated = DateTime.UtcNow;
            await WriteJsonFileAsync(ContractStatesFileName, contractStates);
            _logger.LogDebug($"💾 合约状态文件已保存: {contractStates.States.Count} 个合约");
        }

        /// <summary>
        /// 保存执行历史文件
        /// </summary>
        private async Task SaveExecutionHistoryAsync(ExecutionHistoryFile executionHistory)
        {
            executionHistory.LastUpdated = DateTime.UtcNow;
            await WriteJsonFileAsync(ExecutionHistoryFileName, executionHistory);
            _logger.LogDebug($"💾 执行历史文件已保存: {executionHistory.History.Count} 条记录");
        }

        #endregion

        #region 底层文件操作

        /// <summary>
        /// 原子写入JSON文件
        /// </summary>
        private async Task WriteJsonFileAsync<T>(string fileName, T data)
        {
            await _fileLock.WaitAsync();
            try
            {
                var filePath = Path.Combine(_dataDirectory, fileName);
                var json = JsonSerializer.Serialize(data, _jsonOptions);

                // 原子写入：先写临时文件，再重命名
                var tempFile = filePath + ".tmp";
                var backupFile = filePath + ".backup";

                // 如果目标文件存在，先备份
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupFile, true);
                }

                // 写入临时文件
                await File.WriteAllTextAsync(tempFile, json, System.Text.Encoding.UTF8);

                // 原子替换
                File.Move(tempFile, filePath, true);

                // 删除备份文件（保存成功后）
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }

                _logger.LogDebug($"📝 文件写入成功: {fileName} ({json.Length} 字符)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 文件写入失败: {fileName}");
                
                // 尝试恢复备份
                await TryRestoreBackup(fileName);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// 读取JSON文件
        /// </summary>
        private async Task<T?> LoadJsonFileAsync<T>(string fileName) where T : class
        {
            await _fileLock.WaitAsync();
            try
            {
                var filePath = Path.Combine(_dataDirectory, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug($"📂 文件不存在: {fileName}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning($"⚠️ 文件内容为空: {fileName}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                _logger.LogDebug($"📖 文件读取成功: {fileName} ({json.Length} 字符)");
                
                return result;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"❌ JSON解析失败: {fileName}");
                
                // 尝试恢复备份
                var restored = await TryRestoreBackup(fileName);
                if (restored)
                {
                    return await LoadJsonFileAsync<T>(fileName);
                }
                
                throw new InvalidOperationException($"文件格式错误且无法恢复: {fileName}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 文件读取失败: {fileName}");
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// 尝试恢复备份文件
        /// </summary>
        private async Task<bool> TryRestoreBackup(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, fileName);
                var backupFile = filePath + ".backup";

                if (File.Exists(backupFile))
                {
                    _logger.LogWarning($"🔄 尝试从备份恢复文件: {fileName}");
                    File.Copy(backupFile, filePath, true);
                    _logger.LogInformation($"✅ 文件已从备份恢复: {fileName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 备份恢复失败: {fileName}");
            }
            
            return false;
        }

        #endregion

        #region 默认数据创建

        /// <summary>
        /// 创建默认基础配置文件
        /// </summary>
        private BaseConfigFile CreateDefaultBaseConfigFile()
        {
            _logger.LogInformation("🆕 创建默认基础配置文件");
            
            return new BaseConfigFile
            {
                Version = "2.0",
                LastUpdated = DateTime.UtcNow,
                CurrentConfigId = "",
                Configs = new List<BaseConfig>()
            };
        }

        /// <summary>
        /// 创建默认合约状态文件
        /// </summary>
        private ContractStateFile CreateDefaultContractStateFile()
        {
            _logger.LogInformation("🆕 创建默认合约状态文件");
            
            return new ContractStateFile
            {
                Version = "2.0",
                LastUpdated = DateTime.UtcNow,
                CurrentConfigId = "",
                AccountName = "",
                States = new Dictionary<string, ContractState>()
            };
        }

        /// <summary>
        /// 创建默认执行历史文件
        /// </summary>
        private ExecutionHistoryFile CreateDefaultExecutionHistoryFile()
        {
            _logger.LogInformation("🆕 创建默认执行历史文件");
            
            return new ExecutionHistoryFile
            {
                Version = "2.0",
                LastUpdated = DateTime.UtcNow,
                History = new List<ContractExecutionHistory>()
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取默认数据目录
        /// </summary>
        private string GetDefaultDataDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDirectory = Path.Combine(appDataPath, "BinanceFuturesTrader", "AutoMonitorData");
            return appDirectory;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _fileLock?.Dispose();
        }

        #endregion
    }
} 