using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 统一文件路径管理服务 - 支持多账号隔离
    /// </summary>
    public class FilePathManager
    {
        private readonly ILogger<FilePathManager>? _logger;
        private readonly string _baseAppDataPath;
        
        public FilePathManager(ILogger<FilePathManager>? logger = null)
        {
            _logger = logger;
            _baseAppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceFuturesTrader"
            );
        }

        /// <summary>
        /// 获取账号专用目录
        /// </summary>
        /// <param name="accountName">账号名称</param>
        /// <returns>账号专用目录路径</returns>
        public string GetAccountDirectory(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                // 🔧 优先尝试获取当前真实的账户名称
                accountName = GetCurrentAccountName();
                _logger?.LogDebug($"📁 账户名为空，自动获取当前账户: {accountName}");
            }
            
            var accountDir = Path.Combine(_baseAppDataPath, "Accounts", accountName);
            EnsureDirectoryExists(accountDir);
            _logger?.LogDebug($"📁 账户目录路径: {accountDir}");
            return accountDir;
        }

        /// <summary>
        /// 获取全局配置目录（不按账号分离）
        /// </summary>
        public string GetGlobalConfigDirectory()
        {
            var globalDir = Path.Combine(_baseAppDataPath, "Global");
            EnsureDirectoryExists(globalDir);
            return globalDir;
        }

        #region 账号相关文件路径 (按账号隔离)

        /// <summary>
        /// 获取合约监控状态文件路径 (按账号隔离)
        /// </summary>
        public string GetContractMonitoringStatesFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "contract_monitoring_states.json");
        }

        /// <summary>
        /// 获取执行历史文件路径 (按账号隔离)
        /// </summary>
        public string GetExecutionHistoryFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "execution_history.json");
        }

        /// <summary>
        /// 获取持仓档案文件路径 (按账号隔离) - 已废弃但保留兼容性
        /// </summary>
        [Obsolete("已废弃：请使用 GetContractMonitoringStatesFilePath")]
        public string GetPositionProfilesFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "position_profiles.json");
        }

        /// <summary>
        /// 获取合约配置文件路径 (按账号隔离) - 旧格式兼容
        /// </summary>
        [Obsolete("已废弃：请使用 GetContractMonitoringStatesFilePath")]
        public string GetContractConfigsFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "ContractConfigs.json");
        }

        /// <summary>
        /// 获取追踪止损配置文件路径 (按账号隔离)
        /// </summary>
        public string GetTrailingStopConfigFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "trailing_stop_configs.json");
        }

        /// <summary>
        /// 获取最近合约文件路径 (按账号隔离)
        /// </summary>
        public string GetRecentContractsFilePath(string accountName)
        {
            return Path.Combine(GetAccountDirectory(accountName), "recent_contracts.json");
        }

        #endregion

        #region 全局配置文件路径 (不按账号分离)

        /// <summary>
        /// 获取基础配置文件路径 (全局) - 所有配置的统一文件
        /// </summary>
        public string GetBaseConfigsFilePath()
        {
            return Path.Combine(GetGlobalConfigDirectory(), "auto_monitor_configs.json");
        }

        /// <summary>
        /// 获取单个基础配置文件路径 (全局) - 一个配置一个文件
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <returns>单个配置文件路径</returns>
        public string GetSingleBaseConfigFilePath(string configName)
        {
            // 配置名称清理，确保文件名安全
            var safeConfigName = configName.Replace(" ", "_")
                                           .Replace("/", "_")
                                           .Replace("\\", "_")
                                           .Replace(":", "_")
                                           .Replace("*", "_")
                                           .Replace("?", "_")
                                           .Replace("\"", "_")
                                           .Replace("<", "_")
                                           .Replace(">", "_")
                                           .Replace("|", "_");
            
            return Path.Combine(GetGlobalConfigDirectory(), "BaseConfigs", $"{safeConfigName}.json");
        }

        /// <summary>
        /// 获取基础配置目录路径
        /// </summary>
        /// <returns>基础配置目录路径</returns>
        public string GetBaseConfigsDirectory()
        {
            var configsDir = Path.Combine(GetGlobalConfigDirectory(), "BaseConfigs");
            EnsureDirectoryExists(configsDir);
            return configsDir;
        }

        /// <summary>
        /// 获取交易设置文件路径 (全局)
        /// </summary>
        public string GetTradingSettingsFilePath()
        {
            return Path.Combine(GetGlobalConfigDirectory(), "trading_settings.json");
        }

        /// <summary>
        /// 获取账号配置文件路径 (全局)
        /// </summary>
        public string GetAccountConfigsFilePath()
        {
            return Path.Combine(GetGlobalConfigDirectory(), "account_configs.json");
        }

        /// <summary>
        /// 获取应用设置文件路径 (全局)
        /// </summary>
        public string GetAppSettingsFilePath()
        {
            return Path.Combine(GetGlobalConfigDirectory(), "app_settings.json");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _logger?.LogDebug($"📁 创建目录: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 创建目录失败: {directoryPath}");
                throw;
            }
        }

        /// <summary>
        /// 获取当前活跃账号名称 (从MainViewModel获取)
        /// </summary>
        public string GetCurrentAccountName()
        {
            try
            {
                // 🔧 修复：从主窗口的MainViewModel获取当前选中的账户名称
                if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.DataContext is ViewModels.MainViewModel mainViewModel)
                    {
                                                 var accountName = mainViewModel.SelectedAccount?.Name;
                        if (!string.IsNullOrWhiteSpace(accountName))
                        {
                            _logger?.LogDebug($"📁 成功获取当前账户名称: {accountName}");
                            return accountName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"⚠️ 获取当前账户名称失败: {ex.Message}，使用默认账户");
            }
            
            // 如果无法获取，返回合理的默认值
            return "default";
        }

        /// <summary>
        /// 获取文件信息摘要
        /// </summary>
        public FilePathInfo GetFileInfo(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return new FilePathInfo
            {
                FilePath = filePath,
                DirectoryPath = Path.GetDirectoryName(filePath) ?? "",
                FileName = Path.GetFileName(filePath),
                Exists = fileInfo.Exists,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue
            };
        }

        #endregion
    }

    /// <summary>
    /// 文件路径信息
    /// </summary>
    public class FilePathInfo
    {
        public string FilePath { get; set; } = "";
        public string DirectoryPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public bool Exists { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
} 