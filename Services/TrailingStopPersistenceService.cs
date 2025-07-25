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
    /// 移动止损状态持久化服务
    /// </summary>
    public class TrailingStopPersistenceService
    {
        private readonly ILogger<TrailingStopPersistenceService> _logger;
        private readonly IBinanceService _binanceService;
        private readonly string _dataFilePath;
        
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public TrailingStopPersistenceService(ILogger<TrailingStopPersistenceService> logger, IBinanceService binanceService)
        {
            _logger = logger;
            _binanceService = binanceService;
            
            // 数据文件存储在程序目录下的Data文件夹
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDirectory);
            _dataFilePath = Path.Combine(dataDirectory, "trailing_stop_status.json");
        }

        /// <summary>
        /// 保存移动止损状态到本地文件
        /// </summary>
        public async Task SaveTrailingStopStatusAsync(IEnumerable<TrailingStopStatus> statuses)
        {
            try
            {
                var statusList = statuses.Where(s => s.IsActive).ToList();
                var json = JsonSerializer.Serialize(statusList, _jsonOptions);
                
                await File.WriteAllTextAsync(_dataFilePath, json);
                _logger.LogInformation($"✅ 移动止损状态已保存到文件，共 {statusList.Count} 个状态");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存移动止损状态失败");
            }
        }

        /// <summary>
        /// 从本地文件加载移动止损状态
        /// </summary>
        public async Task<List<TrailingStopStatus>> LoadTrailingStopStatusAsync()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                {
                    _logger.LogInformation("移动止损状态文件不存在，返回空列表");
                    return new List<TrailingStopStatus>();
                }

                var json = await File.ReadAllTextAsync(_dataFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogInformation("移动止损状态文件为空，返回空列表");
                    return new List<TrailingStopStatus>();
                }

                var statuses = JsonSerializer.Deserialize<List<TrailingStopStatus>>(json, _jsonOptions) 
                               ?? new List<TrailingStopStatus>();
                
                _logger.LogInformation($"📥 从文件加载了 {statuses.Count} 个移动止损状态");
                return statuses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载移动止损状态失败");
                return new List<TrailingStopStatus>();
            }
        }

        /// <summary>
        /// 验证并恢复移动止损状态
        /// </summary>
        public async Task<List<TrailingStopStatus>> RecoverTrailingStopStatusAsync()
        {
            try
            {
                var savedStatuses = await LoadTrailingStopStatusAsync();
                if (!savedStatuses.Any())
                {
                    _logger.LogInformation("没有需要恢复的移动止损状态");
                    return new List<TrailingStopStatus>();
                }

                // 获取当前所有委托单，用于验证移动止损单是否仍然存在
                var currentOrders = await _binanceService.GetOpenOrdersAsync();
                var activeStatuses = new List<TrailingStopStatus>();

                foreach (var status in savedStatuses)
                {
                    try
                    {
                        // 验证移动止损单是否仍然存在
                        var trailingOrder = currentOrders.FirstOrDefault(o => 
                            o.OrderId == status.TrailingOrderId && 
                            o.Type == "TRAILING_STOP_MARKET" && 
                            o.Status == "NEW");

                        if (trailingOrder != null)
                        {
                            // 移动止损单仍然有效
                            status.Status = "已恢复";
                            activeStatuses.Add(status);
                            _logger.LogInformation($"✅ 恢复移动止损状态: {status.Symbol} 订单#{status.TrailingOrderId}");
                        }
                        else
                        {
                            // 移动止损单已不存在（可能已执行或被取消）
                            status.Status = "已失效";
                            _logger.LogInformation($"⚠️ 移动止损单已失效: {status.Symbol} 订单#{status.TrailingOrderId}");
                        }

                        // 如果有固定止损单，也验证其状态
                        if (status.FixedOrderId.HasValue)
                        {
                            var fixedOrder = currentOrders.FirstOrDefault(o => 
                                o.OrderId == status.FixedOrderId.Value && 
                                o.Type == "STOP_MARKET" && 
                                o.Status == "NEW");

                            if (fixedOrder == null)
                            {
                                _logger.LogInformation($"⚠️ 关联的固定止损单已失效: {status.Symbol} 订单#{status.FixedOrderId}");
                                status.FixedOrderId = null; // 清除无效的固定止损单ID
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"验证移动止损状态失败: {status.Symbol}");
                        status.Status = "验证失败";
                    }
                }

                _logger.LogInformation($"🔄 移动止损状态恢复完成，有效状态 {activeStatuses.Count} 个，总共检查 {savedStatuses.Count} 个");
                
                // 保存恢复后的状态（移除失效的状态）
                if (activeStatuses.Count != savedStatuses.Count)
                {
                    await SaveTrailingStopStatusAsync(activeStatuses);
                }

                return activeStatuses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 恢复移动止损状态失败");
                return new List<TrailingStopStatus>();
            }
        }

        /// <summary>
        /// 添加或更新移动止损状态
        /// </summary>
        public async Task AddOrUpdateStatusAsync(TrailingStopStatus status, IEnumerable<TrailingStopStatus> currentStatuses)
        {
            try
            {
                var statusList = currentStatuses.ToList();
                
                // 查找是否已存在相同合约的状态
                var existingIndex = statusList.FindIndex(s => s.Symbol == status.Symbol);
                if (existingIndex >= 0)
                {
                    // 更新现有状态
                    statusList[existingIndex] = status;
                    _logger.LogInformation($"更新移动止损状态: {status.Symbol}");
                }
                else
                {
                    // 添加新状态
                    statusList.Add(status);
                    _logger.LogInformation($"添加移动止损状态: {status.Symbol}");
                }

                await SaveTrailingStopStatusAsync(statusList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"添加或更新移动止损状态失败: {status?.Symbol}");
            }
        }

        /// <summary>
        /// 移除移动止损状态
        /// </summary>
        public async Task RemoveStatusAsync(string symbol, IEnumerable<TrailingStopStatus> currentStatuses)
        {
            try
            {
                var statusList = currentStatuses.Where(s => s.Symbol != symbol).ToList();
                await SaveTrailingStopStatusAsync(statusList);
                _logger.LogInformation($"移除移动止损状态: {symbol}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"移除移动止损状态失败: {symbol}");
            }
        }

        /// <summary>
        /// 清除所有移动止损状态
        /// </summary>
        public async Task ClearAllStatusAsync()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    File.Delete(_dataFilePath);
                    _logger.LogInformation("🗑️ 已清除所有移动止损状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清除移动止损状态失败");
            }
        }
    }
} 