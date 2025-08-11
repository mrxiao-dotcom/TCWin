using System;
using System.Threading.Tasks;

namespace BinanceFuturesTrader
{
    /// <summary>
    /// 🧪 简化集成测试运行器
    /// 用于快速验证简化模块集成是否正常工作
    /// </summary>
    public class RunSimplifiedIntegrationTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🎯 开始运行简化模块集成测试...");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                await TestSimplifiedIntegration.RunIntegrationTestAsync();
                
                Console.WriteLine();
                Console.WriteLine(new string('=', 60));
                Console.WriteLine("🎉 简化模块集成测试完成！");
                Console.WriteLine("✅ 所有组件已准备就绪，可以开始使用新的简化架构");
                Console.WriteLine();
                Console.WriteLine("📋 下一步操作:");
                Console.WriteLine("1. 在主程序中初始化 SimplifiedServiceIntegrator");
                Console.WriteLine("2. 使用 SimplifiedUIAdapter 替换现有UI绑定");
                Console.WriteLine("3. 启用新的自动监控服务");
                Console.WriteLine("4. 验证状态持久化功能");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ 集成测试失败:");
                Console.WriteLine($"   错误: {ex.Message}");
                Console.WriteLine($"   详细信息: {ex.StackTrace}");
                Console.WriteLine();
                Console.WriteLine("💡 建议检查:");
                Console.WriteLine("1. 确保所有依赖项已正确配置");
                Console.WriteLine("2. 检查数据目录权限");
                Console.WriteLine("3. 验证现有服务的可用性");
            }
            
            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
} 