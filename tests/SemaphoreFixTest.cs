using System;
using System.Threading.Tasks;
using krrTools.Tools.KRRLVAnalysis;

namespace krrTools.Tests
{
    /// <summary>
    /// 测试信号量修复是否有效
    /// </summary>
    public class SemaphoreFixTest
    {
        public static async Task TestSemaphoreUsage()
        {
            // 创建ViewModel实例
            var viewModel = new KRRLVAnalysisViewModel();

            // 创建一些测试文件路径
            string[] testFiles = new string[]
            {
                "test1.osu",
                "test2.osu",
                "test3.osu"
            };

            try
            {
                // 尝试处理文件 - 这应该不会抛出信号量异常
                viewModel.ProcessDroppedFiles(testFiles);

                Console.WriteLine("✅ 信号量修复测试通过 - 没有抛出异常");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("semaphore") || ex.Message.Contains("maximum count"))
                    Console.WriteLine("❌ 信号量问题仍然存在: " + ex.Message);
                else
                    Console.WriteLine("ℹ️ 其他异常 (预期的文件不存在错误): " + ex.Message);
            }
            finally
            {
                // 清理资源
                if (viewModel is IDisposable disposable) disposable.Dispose();
            }
        }
    }
}
