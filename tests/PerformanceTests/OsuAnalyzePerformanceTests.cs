using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.PerformanceTests
{
    public class OsuAnalyzePerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        // 统一的测试文件数量常量
        private const int SimulatedFileCount = 10;

        public OsuAnalyzePerformanceTests(ITestOutputHelper testOutputHelper)
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
        public async Task Analyze_ShouldHandleConcurrentRequests()
        {
            // 从TestOsuFile文件夹读取实际的osu文件
            string   testOsuFileDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile");
            string[] osuFiles       = Directory.GetFiles(testOsuFileDir, "*.osu", SearchOption.TopDirectoryOnly);

            if (osuFiles.Length == 0)
            {
                _testOutputHelper.WriteLine("No .osu files found in TestOsuFile directory. Skipping concurrent test.");
                return;
            }

            // 读取第一个真实文件到内存中
            string sampleFilePath = osuFiles.First();
            _testOutputHelper.WriteLine($"Loaded sample beatmap from: {Path.GetFileName(sampleFilePath)}");

            // 解码谱面文件
            Beatmap sampleBeatmap = BeatmapDecoder.Decode(sampleFilePath);

            if (sampleBeatmap == null)
            {
                _testOutputHelper.WriteLine("Failed to decode sample beatmap. Skipping test.");
                return;
            }

            // 预热阶段
            _testOutputHelper.WriteLine("Warmup phase...");

            for (int i = 0; i < 3; i++)
            {
                OsuAnalysisBasic       basicInfo   = await OsuAnalyzer.AnalyzeBasicInfoAsync(sampleBeatmap);
                OsuAnalysisPerformance performance = await OsuAnalyzer.AnalyzeAdvancedAsync(sampleBeatmap);
            }

            _testOutputHelper.WriteLine("Warmup completed.");

            // 模拟SimulatedFileCount个文件处理 - 每个任务都处理同一个内存中的谱面
            const int simulatedFileCount = SimulatedFileCount;

            // 并行分析
            var stopwatch = Stopwatch.StartNew();
            List<Task<(OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance)>> tasks = Enumerable.Range(0, simulatedFileCount)
                                                                                                           .Select(async _ =>
                                                                                                            {
                                                                                                                OsuAnalysisBasic basicInfo = await OsuAnalyzer.AnalyzeBasicInfoAsync(sampleBeatmap);
                                                                                                                OsuAnalysisPerformance performance =
                                                                                                                    await OsuAnalyzer.AnalyzeAdvancedAsync(sampleBeatmap);
                                                                                                                return (basicInfo, performance);
                                                                                                            })
                                                                                                           .ToList();

            (OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance)[] results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // 验证所有结果
            foreach ((OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance) result in results)
            {
                (OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance) = result;
                Assert.NotNull(basicInfo);
                if (performance == null) _testOutputHelper.WriteLine($"Analysis failed for {basicInfo.Title}");
                Assert.NotNull(performance);
                Assert.True(performance.XXY_SR >= 0);
                Assert.True(basicInfo.MaxKPS >= 0);
            }

            // 计算性能统计
            long   totalTime   = stopwatch.ElapsedMilliseconds;
            double averageTime = totalTime / (double)simulatedFileCount;
            double throughput  = simulatedFileCount / (totalTime / 1000.0); // 文件/秒

            _testOutputHelper.WriteLine(
                $"Concurrent analysis of {simulatedFileCount} simulated files (using 1 real beatmap) took: {totalTime}ms");
            _testOutputHelper.WriteLine($"Average time per beatmap: {averageTime:F2}ms");
            _testOutputHelper.WriteLine($"Throughput: {throughput:F2} beatmaps/second");
            _testOutputHelper.WriteLine(
                $"Total CPU time equivalent: ~{totalTime * Environment.ProcessorCount}ms (if single-threaded)");
            _testOutputHelper.WriteLine($"Sample file location: {sampleFilePath}");
        }
    }
}
