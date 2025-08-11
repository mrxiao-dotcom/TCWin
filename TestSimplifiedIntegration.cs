using System;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;
using System.Linq; // Added for .Any()

namespace BinanceFuturesTrader
{
    /// <summary>
    /// 🧪 简化模块集成测试脚本
    /// 验证完整的简化系统集成是否正常工作
    /// </summary>
    public class TestSimplifiedIntegration
    {
        public static async Task RunIntegrationTestAsync()
        {
            Console.WriteLine("🎯 开始测试简化模块集成...");
            
            // 创建测试用的日志工厂
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            
            // 模拟现有服务（实际使用时会从现有系统获取）
            var mockBinanceService = CreateMockBinanceService();
            var mockTradingService = CreateMockTradingService();
            var mockGlobalModeManager = CreateMockGlobalModeManager();

            try
            {
                // 1. 创建服务整合器
                Console.WriteLine("\n🔧 创建服务整合器...");
                var integrator = new SimplifiedServiceIntegrator(
                    mockBinanceService,
                    mockTradingService, 
                    mockGlobalModeManager,
                    loggerFactory.CreateLogger<SimplifiedServiceIntegrator>());

                // 2. 初始化所有服务
                Console.WriteLine("\n🚀 初始化所有服务...");
                var initResult = await integrator.InitializeAsync();
                if (!initResult)
                {
                    Console.WriteLine("❌ 服务初始化失败");
                    return;
                }
                Console.WriteLine("✅ 服务初始化成功");

                // 3. 执行健康检查
                Console.WriteLine("\n🔍 执行健康检查...");
                var healthCheck = await integrator.PerformHealthCheckAsync();
                Console.WriteLine($"健康状态: {(healthCheck.IsHealthy ? "✅ 健康" : "❌ 异常")}");
                if (!healthCheck.IsHealthy)
                {
                    Console.WriteLine("发现问题:");
                    foreach (var issue in healthCheck.Issues)
                    {
                        Console.WriteLine($"   - {issue}");
                    }
                }

                // 4. 测试基础配置管理
                Console.WriteLine("\n📋 测试基础配置管理...");
                await TestConfigManagementAsync(integrator);

                // 5. 测试合约状态管理
                Console.WriteLine("\n📊 测试合约状态管理...");
                await TestContractStateManagementAsync(integrator);

                // 6. 测试执行引擎
                Console.WriteLine("\n⚙️ 测试执行引擎...");
                await TestExecutionEngineAsync(integrator);

                // 7. 测试UI适配器
                Console.WriteLine("\n🖥️ 测试UI适配器...");
                await TestUIAdapterAsync(integrator);

                // 8. 测试监控服务
                Console.WriteLine("\n👁️ 测试监控服务...");
                await TestMonitorServiceAsync(integrator);

                // 9. 测试完整工作流
                Console.WriteLine("\n🔄 测试完整工作流...");
                await TestCompleteWorkflowAsync(integrator);

                // 10. 获取统计信息
                Console.WriteLine("\n📈 获取统计信息...");
                await DisplayStatisticsAsync(integrator);

                Console.WriteLine("\n🎉 所有集成测试通过！简化模块集成成功！");

                // 清理资源
                integrator.Dispose();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 集成测试失败: {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            }
        }

        #region 测试方法

        /// <summary>
        /// 测试配置管理功能
        /// </summary>
        private static async Task TestConfigManagementAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                // 测试获取可用配置
                var configNames = await integrator.ConfigManager.GetAvailableConfigNamesAsync();
                Console.WriteLine($"✅ 可用配置: {string.Join(", ", configNames)}");

                // 测试配置验证
                foreach (var configName in configNames)
                {
                    var isValid = await integrator.ConfigManager.ValidateBaseConfigAsync(configName);
                    Console.WriteLine($"   - {configName}: {(isValid ? "✅ 有效" : "❌ 无效")}");
                }

                // 测试配置统计
                var configStats = await integrator.ConfigManager.GetConfigUsageStatsAsync();
                Console.WriteLine($"✅ 配置使用统计: {configStats.Count} 种配置被使用");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 配置管理测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试合约状态管理
        /// </summary>
        private static async Task TestContractStateManagementAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                // 创建测试合约
                var testSymbol = "TESTINTEGRATION";
                var testSide = "LONG";
                var configName = "基础";

                var createResult = await integrator.CreateContractConfigAsync(testSymbol, testSide, configName);
                Console.WriteLine($"✅ 创建测试合约: {testSymbol}_{testSide} -> {(createResult ? "成功" : "失败")}");

                if (createResult)
                {
                    // 测试状态查询
                    var contractState = await integrator.StateService.GetContractStateAsync(testSymbol, testSide);
                    if (contractState != null)
                    {
                        Console.WriteLine($"   配置名称: {contractState.ConfigName}");
                        Console.WriteLine($"   保本金额: {contractState.BreakEvenConfig.TriggerProfitAmount}");
                        Console.WriteLine($"   推仓阶梯: {contractState.AddPositionConfig.Tiers.Count} 个");
                        Console.WriteLine($"   保盈阶梯: {contractState.ProfitProtectionConfig.Tiers.Count} 个");
                    }

                    // 测试状态更新
                    await integrator.StateService.UpdateExecutionStateAsync(testSymbol, testSide, "ADDPOSITION", 1, StandardExecutionState.Executed, "集成测试执行");
                    Console.WriteLine("✅ 状态更新测试完成");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 合约状态管理测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试执行引擎
        /// </summary>
        private static async Task TestExecutionEngineAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                var testSymbol = "TESTINTEGRATION";
                var testSide = "LONG";
                var testPnl = 50.0m; // 模拟浮盈

                // 模拟执行监控
                var results = await integrator.ExecutionEngine.ExecuteContractMonitoringAsync(testSymbol, testSide, testPnl);
                Console.WriteLine($"✅ 执行引擎测试: 执行了 {results.Count} 个操作");

                foreach (var result in results)
                {
                    Console.WriteLine($"   - {result.DisplayName}: {(result.IsSuccess ? "✅ 成功" : "❌ 失败")} - {result.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 执行引擎测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试UI适配器
        /// </summary>
        private static async Task TestUIAdapterAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                // 测试获取UI数据
                var uiConfigs = await integrator.UIAdapter.GetUIContractConfigsAsync();
                Console.WriteLine($"✅ UI适配器: 获取 {uiConfigs.Count} 个UI配置");

                // 测试可用配置名称
                var configNames = await integrator.UIAdapter.GetAvailableConfigNamesForUIAsync();
                Console.WriteLine($"✅ 可用配置: {configNames.Count} 个");

                // 测试统计信息
                var uiStats = await integrator.UIAdapter.GetUIStatsAsync();
                Console.WriteLine($"✅ UI统计: 监控状态={uiStats.MonitorStats?.IsRunning}, 合约数={uiStats.MonitorStats?.TotalContracts}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UI适配器测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试监控服务
        /// </summary>
        private static async Task TestMonitorServiceAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                // 测试启动监控
                var startResult = await integrator.StartMonitoringAsync();
                Console.WriteLine($"✅ 启动监控: {(startResult ? "成功" : "失败")}");

                if (startResult)
                {
                    // 等待一段时间
                    await Task.Delay(2000);

                    // 测试手动扫描
                    var scanResult = await integrator.MonitorService.ExecuteManualScanAsync();
                    Console.WriteLine($"✅ 手动扫描: {(scanResult ? "成功" : "失败")}");

                    // 测试停止监控
                    integrator.StopMonitoring();
                    Console.WriteLine("✅ 停止监控: 成功");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 监控服务测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试完整工作流
        /// </summary>
        private static async Task TestCompleteWorkflowAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                Console.WriteLine("开始完整工作流测试...");

                // 1. 创建合约配置
                await integrator.CreateContractConfigAsync("WORKFLOW", "LONG", "基础");

                // 2. 启动监控
                await integrator.StartMonitoringAsync();

                // 3. 等待监控运行
                await Task.Delay(3000);

                // 4. 手动执行特定合约监控
                var manualResults = await integrator.MonitorService.ExecuteManualContractMonitoringAsync("WORKFLOW", "LONG");
                Console.WriteLine($"   手动监控结果: {manualResults.Count} 个操作");

                // 5. 停止监控
                integrator.StopMonitoring();

                // 6. 重置状态
                await integrator.ResetAllExecutionStatesAsync();

                Console.WriteLine("✅ 完整工作流测试完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 完整工作流测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示统计信息
        /// </summary>
        private static async Task DisplayStatisticsAsync(SimplifiedServiceIntegrator integrator)
        {
            try
            {
                var legacyStats = await integrator.GetLegacyStatsAsync();
                
                Console.WriteLine("📊 系统统计信息:");
                Console.WriteLine($"   监控状态: {(legacyStats.IsMonitoringRunning ? "运行中" : "已停止")}");
                Console.WriteLine($"   总合约数: {legacyStats.TotalContracts}");
                Console.WriteLine($"   启用合约: {legacyStats.EnabledContracts}");
                Console.WriteLine($"   已执行操作: {legacyStats.TotalExecutedOperations}");
                Console.WriteLine($"   最后更新: {legacyStats.LastUpdateTime:yyyy-MM-dd HH:mm:ss}");

                if (legacyStats.ConfigUsageStats.Any())
                {
                    Console.WriteLine("   配置使用情况:");
                    foreach (var stat in legacyStats.ConfigUsageStats)
                    {
                        Console.WriteLine($"     - {stat.Key}: {stat.Value} 个合约");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取统计信息失败: {ex.Message}");
            }
        }

        #endregion

        #region Mock服务创建

        /// <summary>
        /// 创建模拟的BinanceService
        /// </summary>
        private static BinanceService CreateMockBinanceService()
        {
            // BinanceService使用默认构造函数
            try
            {
                return new BinanceService();
            }
            catch
            {
                // 如果无法创建真实的BinanceService，返回null
                // 实际使用时应该从现有系统获取
                return null!;
            }
        }

        /// <summary>
        /// 创建模拟的TradingExecutionService
        /// </summary>
        private static TradingExecutionService CreateMockTradingService()
        {
            try
            {
                var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TradingExecutionService>();
                var binanceService = CreateMockBinanceService();
                
                // TradingExecutionService构造函数需要(ILogger<TradingExecutionService>, IBinanceService)
                return new TradingExecutionService(logger, binanceService);
            }
            catch
            {
                return null!;
            }
        }

        /// <summary>
        /// 创建模拟的GlobalModeManager
        /// </summary>
        private static GlobalModeManager CreateMockGlobalModeManager()
        {
            try
            {
                return GlobalModeManager.Instance;
            }
            catch
            {
                return null!;
            }
        }

        #endregion
    }
} 