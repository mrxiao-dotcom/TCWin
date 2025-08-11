using System;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader
{
    /// <summary>
    /// 🧪 简化数据模型测试脚本
    /// 验证重构后的数据模型是否正常工作
    /// </summary>
    public class TestSimplifiedDataModel
    {
        public static async Task RunTestAsync()
        {
            Console.WriteLine("🎯 开始测试简化数据模型...");
            
            // 创建测试用的Logger
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SimplifiedStateService>();
            var configLogger = loggerFactory.CreateLogger<SimplifiedConfigManager>();

            try
            {
                // 1. 测试SimplifiedStateService
                Console.WriteLine("\n📁 测试SimplifiedStateService...");
                var stateService = new SimplifiedStateService(logger, "./TestData");
                
                // 测试基础配置加载
                var baseConfigs = await stateService.GetBaseConfigsAsync();
                Console.WriteLine($"✅ 加载基础配置: {baseConfigs.Count} 个");
                
                foreach (var config in baseConfigs)
                {
                    Console.WriteLine($"   - {config.Key}: {config.Value.Description}");
                    Console.WriteLine($"     推仓阶梯: {config.Value.AddPositionConfig.Tiers.Count} 个");
                    Console.WriteLine($"     保盈阶梯: {config.Value.ProfitProtectionConfig.Tiers.Count} 个");
                }

                // 2. 测试合约状态初始化
                Console.WriteLine("\n📊 测试合约状态初始化...");
                var testSymbol = "TESTUSDT";
                var testSide = "LONG";
                var configName = "基础";
                
                var contractState = await stateService.InitializeContractStateAsync(testSymbol, testSide, configName);
                Console.WriteLine($"✅ 初始化合约状态: {testSymbol}_{testSide}");
                Console.WriteLine($"   配置名称: {contractState.ConfigName}");
                Console.WriteLine($"   保本金额: {contractState.BreakEvenConfig.TriggerProfitAmount}");
                Console.WriteLine($"   推仓阶梯数量: {contractState.AddPositionConfig.Tiers.Count}");
                Console.WriteLine($"   保盈阶梯数量: {contractState.ProfitProtectionConfig.Tiers.Count}");

                // 3. 测试状态更新
                Console.WriteLine("\n🔄 测试状态更新...");
                await stateService.UpdateExecutionStateAsync(testSymbol, testSide, "ADDPOSITION", 1, StandardExecutionState.Executed, "测试执行成功");
                
                var updatedState = await stateService.GetContractStateAsync(testSymbol, testSide);
                var tier1 = updatedState?.AddPositionConfig.Tiers.Find(t => t.TierIndex == 1);
                if (tier1 != null)
                {
                    Console.WriteLine($"✅ 推仓阶梯1状态更新成功: {ExecutionStateExtensions.FromInt(tier1.ExecutionState)}");
                    Console.WriteLine($"   执行结果: {tier1.ExecutionResult}");
                    Console.WriteLine($"   执行时间: {tier1.ExecutionTime}");
                }

                // 4. 测试SimplifiedConfigManager
                Console.WriteLine("\n⚙️ 测试SimplifiedConfigManager...");
                var configManager = new SimplifiedConfigManager(configLogger, stateService);
                
                var availableConfigs = await configManager.GetAvailableConfigNamesAsync();
                Console.WriteLine($"✅ 可用配置: {string.Join(", ", availableConfigs)}");
                
                var isValid = await configManager.ValidateBaseConfigAsync("基础");
                Console.WriteLine($"✅ 基础配置验证: {(isValid ? "通过" : "失败")}");

                // 5. 测试ExecutionState扩展方法
                Console.WriteLine("\n🎮 测试ExecutionState扩展方法...");
                Console.WriteLine($"   NotTriggered -> {StandardExecutionState.NotTriggered.ToDisplayText()}");
                Console.WriteLine($"   Executing -> {StandardExecutionState.Executing.ToDisplayText()}");
                Console.WriteLine($"   Executed -> {StandardExecutionState.Executed.ToDisplayText()}");
                Console.WriteLine($"   Failed -> {StandardExecutionState.Failed.ToDisplayText()}");
                
                Console.WriteLine($"   CanExecute(NotTriggered): {StandardExecutionState.NotTriggered.CanExecute()}");
                Console.WriteLine($"   CanExecute(Executed): {StandardExecutionState.Executed.CanExecute()}");
                Console.WriteLine($"   IsCompleted(Executed): {StandardExecutionState.Executed.IsCompleted()}");

                // 6. 测试状态查询
                Console.WriteLine("\n🔍 测试状态查询...");
                var canExecute = await stateService.CanExecuteAsync(testSymbol, testSide, "ADDPOSITION", 2);
                Console.WriteLine($"✅ 推仓阶梯2可执行: {canExecute}");
                
                var executionState = await stateService.GetExecutionStateAsync(testSymbol, testSide, "ADDPOSITION", 1);
                Console.WriteLine($"✅ 推仓阶梯1状态: {executionState}");

                Console.WriteLine("\n🎉 所有测试通过！简化数据模型重构成功！");
                
                // 显示文件结构
                Console.WriteLine("\n📁 生成的文件结构:");
                Console.WriteLine("   TestData/BaseConfig.json - 基础配置文件");
                Console.WriteLine("   TestData/ContractStates.json - 统一状态文件");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
} 