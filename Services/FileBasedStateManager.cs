using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;
using System.Linq; // Added for Skip and ToList

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 以文件为驱动中心的状态管理器
    /// 🔒 核心特性：文件锁定机制，确保状态一致性
    /// </summary>
    public class FileBasedStateManager
    {
        private readonly ILogger<FileBasedStateManager>? _logger;
        private readonly string _monitoringStateFilePath;
        private readonly string _executionHistoryFilePath;
        private readonly SemaphoreSlim _fileLock;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileBasedStateManager(ILogger<FileBasedStateManager>? logger = null)
        {
            _logger = logger;
            _fileLock = new SemaphoreSlim(1, 1); // 文件级别的互斥锁
            
            // 初始化文件路径
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataDir = Path.Combine(appDataPath, "BinanceFuturesTrader", "AutoMonitor");
            Directory.CreateDirectory(dataDir);
            
            _monitoringStateFilePath = Path.Combine(dataDir, "contract_monitoring_states.json");
            _executionHistoryFilePath = Path.Combine(dataDir, "execution_history.json");
            
            // JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            _logger?.LogInformation("🔒 FileBasedStateManager 初始化完成");
        }

        /// <summary>
        /// 🔒 执行扫描周期：锁定文件 -> 读取 -> 处理 -> 保存 -> 解锁
        /// </summary>
        public async Task<T> ExecuteWithFileLockAsync<T>(Func<Dictionary<string, ContractMonitoringState>, Task<T>> scanOperation)
        {
            await _fileLock.WaitAsync();
            _logger?.LogDebug("🔒 [FILE-LOCK] 文件锁定成功，开始扫描周期");
            
            try
            {
                // 步骤1: 从文件读取最新状态到内存
                var states = await LoadStatesFromFileAsync();
                _logger?.LogDebug($"🔒 [FILE-READ] 从文件加载 {states.Count} 个合约状态");

                // 步骤2: 执行扫描和状态修改逻辑
                var result = await scanOperation(states);
                
                // 步骤3: 将修改后的状态保存回文件
                await SaveStatesToFileAsync(states);
                _logger?.LogDebug($"🔒 [FILE-SAVE] 状态已保存到文件，包含 {states.Count} 个合约");

                return result;
            }
            finally
            {
                _fileLock.Release();
                _logger?.LogDebug("🔒 [FILE-UNLOCK] 文件锁定释放，扫描周期完成");
            }
        }

        /// <summary>
        /// 🔒 获取当前状态的只读快照（不锁定）
        /// </summary>
        public async Task<Dictionary<string, ContractMonitoringState>> GetStatesSnapshotAsync()
        {
            return await LoadStatesFromFileAsync();
        }

        /// <summary>
        /// 🔒 强制保存状态到文件（带锁定）
        /// </summary>
        public async Task ForceUpdateStateAsync(string contractKey, ContractMonitoringState state)
        {
            await _fileLock.WaitAsync();
            try
            {
                var states = await LoadStatesFromFileAsync();
                states[contractKey] = state;
                await SaveStatesToFileAsync(states);
                _logger?.LogInformation($"🔒 [FORCE-UPDATE] 合约状态已强制更新: {contractKey}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// 🔒 清理已平仓的合约状态
        /// </summary>
        public async Task CleanupClosedPositionsAsync(HashSet<string> activeContractKeys)
        {
            await _fileLock.WaitAsync();
            try
            {
                var states = await LoadStatesFromFileAsync();
                var toRemove = new List<string>();
                
                foreach (var key in states.Keys)
                {
                    if (!activeContractKeys.Contains(key))
                    {
                        toRemove.Add(key);
                    }
                }
                
                foreach (var key in toRemove)
                {
                    states.Remove(key);
                    _logger?.LogInformation($"🔒 [CLEANUP] 已清理已平仓合约: {key}");
                }
                
                if (toRemove.Count > 0)
                {
                    await SaveStatesToFileAsync(states);
                    _logger?.LogInformation($"🔒 [CLEANUP] 清理完成，移除 {toRemove.Count} 个已平仓合约");
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// 从文件加载状态（私有方法，不加锁）
        /// </summary>
        private async Task<Dictionary<string, ContractMonitoringState>> LoadStatesFromFileAsync()
        {
            try
            {
                if (!File.Exists(_monitoringStateFilePath))
                {
                    _logger?.LogDebug("🔒 [FILE-READ] 状态文件不存在，返回空状态");
                    return new Dictionary<string, ContractMonitoringState>();
                }

                var json = await File.ReadAllTextAsync(_monitoringStateFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("🔒 [FILE-READ] 状态文件为空，返回空状态");
                    return new Dictionary<string, ContractMonitoringState>();
                }

                var states = JsonSerializer.Deserialize<Dictionary<string, ContractMonitoringState>>(json, _jsonOptions);
                return states ?? new Dictionary<string, ContractMonitoringState>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "🔒 [FILE-READ-ERROR] 读取状态文件失败，返回空状态");
                return new Dictionary<string, ContractMonitoringState>();
            }
        }

        /// <summary>
        /// 保存状态到文件（私有方法，不加锁）
        /// </summary>
        private async Task SaveStatesToFileAsync(Dictionary<string, ContractMonitoringState> states)
        {
            try
            {
                var json = JsonSerializer.Serialize(states, _jsonOptions);
                await File.WriteAllTextAsync(_monitoringStateFilePath, json);
                _logger?.LogDebug($"🔒 [FILE-SAVE] 状态文件保存成功: {_monitoringStateFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "🔒 [FILE-SAVE-ERROR] 保存状态文件失败");
                throw;
            }
        }

        /// <summary>
        /// 记录执行历史到文件
        /// </summary>
        public async Task AppendExecutionHistoryAsync(ExecutionHistoryRecord record)
        {
            try
            {
                var history = new List<ExecutionHistoryRecord>();
                
                if (File.Exists(_executionHistoryFilePath))
                {
                    var json = await File.ReadAllTextAsync(_executionHistoryFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        history = JsonSerializer.Deserialize<List<ExecutionHistoryRecord>>(json, _jsonOptions) ?? new List<ExecutionHistoryRecord>();
                    }
                }
                
                history.Add(record);
                
                // 保留最近1000条记录
                if (history.Count > 1000)
                {
                    history = history.Skip(history.Count - 1000).ToList();
                }
                
                var updatedJson = JsonSerializer.Serialize(history, _jsonOptions);
                await File.WriteAllTextAsync(_executionHistoryFilePath, updatedJson);
                
                _logger?.LogDebug($"🔒 [HISTORY] 执行历史已记录: {record.ExecutionType} - {record.Symbol}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "🔒 [HISTORY-ERROR] 记录执行历史失败");
            }
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public async Task<List<ExecutionHistoryRecord>> GetExecutionHistoryAsync()
        {
            try
            {
                if (!File.Exists(_executionHistoryFilePath))
                {
                    return new List<ExecutionHistoryRecord>();
                }

                var json = await File.ReadAllTextAsync(_executionHistoryFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ExecutionHistoryRecord>();
                }

                return JsonSerializer.Deserialize<List<ExecutionHistoryRecord>>(json, _jsonOptions) ?? new List<ExecutionHistoryRecord>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "🔒 [HISTORY-READ-ERROR] 读取执行历史失败");
                return new List<ExecutionHistoryRecord>();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _fileLock?.Dispose();
        }
    }
} 