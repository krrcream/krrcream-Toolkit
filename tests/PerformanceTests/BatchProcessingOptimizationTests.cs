using System;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.PerformanceTests
{
    public class BatchProcessingOptimizationTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BatchProcessingOptimizationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            // 在单元测试中禁用控制台日志输出，避免大量日志噪音
            Logger.SetConsoleOutputEnabled(false);
        }

        public void Dispose()
        {
            // 测试结束后重新启用控制台输出
            Logger.SetConsoleOutputEnabled(true);
        }

        [Fact]
        public void BatchSizeCalculation_ShouldProduceReasonableBatchCounts()
        {
            // 测试批次数量是否合理
            var testCases = new[]
            {
                (totalFiles: 50, expectedBatchSize: 1), // 直接并发
                (totalFiles: 80, expectedBatchSize: 1), // 直接并发
                (totalFiles: 100, expectedBatchSize: 5), // 小批次
                (totalFiles: 500, expectedBatchSize: 10), // 中批次
                (totalFiles: 2000, expectedBatchSize: 50) // 大批次
            };

            foreach ((int totalFiles, int expectedBatchSize) in testCases)
            {
                int batchCount = (int)Math.Ceiling((double)totalFiles / expectedBatchSize);
                int concurrentBatches = Environment.ProcessorCount;

                _testOutputHelper.WriteLine(
                    $"文件数: {totalFiles:N0}, 批次大小: {expectedBatchSize}, " +
                    $"批次数: {batchCount}, 并发批次: {concurrentBatches}, " +
                    $"并发效率: {(double)batchCount / concurrentBatches:F1}轮");

                // 验证批次数量合理性
                Assert.True(batchCount >= 1, "至少应有1个批次");
                Assert.True(batchCount <= totalFiles, "批次数不应超过文件总数");

                // 验证并发效率（批次数/CPU核心数）
                double concurrencyRatio = (double)batchCount / concurrentBatches;
                Assert.True(concurrencyRatio >= 0.5, "并发效率不应过低");
            }
        }

        [Theory]
        [InlineData(50)] // 直接并发场景
        [InlineData(80)] // 边界场景
        [InlineData(100)] // 小批次场景
        [InlineData(1000)] // 大批次场景
        public void TaskCreationOptimization_ShouldHandleDifferentScales(int totalFiles)
        {
            // 原始方式：每个文件一个Task
            int originalTaskCount = totalFiles;

            // 新的激进策略
            int batchSize = totalFiles switch
                            {
                                <= 80   => 1, // 直接并发，无优化
                                <= 200  => 5,
                                <= 500  => 10,
                                <= 1000 => 20,
                                _       => 50
                            };

            int optimizedTaskCount = (int)Math.Ceiling((double)totalFiles / batchSize);
            double taskReduction = (double)(originalTaskCount - optimizedTaskCount) / originalTaskCount * 100;

            _testOutputHelper.WriteLine($"文件数: {totalFiles:N0}");
            _testOutputHelper.WriteLine($"批次大小: {batchSize}");
            _testOutputHelper.WriteLine($"原始Task数: {originalTaskCount:N0}");
            _testOutputHelper.WriteLine($"优化后Task数: {optimizedTaskCount:N0}");
            _testOutputHelper.WriteLine($"Task减少: {taskReduction:F1}%");
            _testOutputHelper.WriteLine($"策略: {(batchSize == 1 ? "直接并发" : "批处理")}");

            // 验证逻辑正确性
            Assert.True(optimizedTaskCount >= 1, "至少应有1个Task");
            Assert.True(optimizedTaskCount <= originalTaskCount, "优化后Task数不应超过原始数");

            // 对于80个文件以内，应该是直接并发（无优化）
            if (totalFiles <= 80)
            {
                Assert.Equal(originalTaskCount, optimizedTaskCount);
                Assert.Equal(0, taskReduction); // 无减少
            }
            else
                Assert.True(taskReduction > 0, "超过80个文件应有Task减少");
        }
    }
}
