using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.PerformanceTests;

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
        var testOsuFileDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile");
        var osuFiles = Directory.GetFiles(testOsuFileDir, "*.osu", SearchOption.TopDirectoryOnly);

        if (osuFiles.Length == 0)
        {
            _testOutputHelper.WriteLine("No .osu files found in TestOsuFile directory. Skipping concurrent test.");
            return;
        }

        // 读取第一个真实文件到内存中
        var sampleFilePath = osuFiles.First();
        var sampleBeatmap = BeatmapDecoder.Decode(sampleFilePath);
        _testOutputHelper.WriteLine($"Loaded sample beatmap from: {Path.GetFileName(sampleFilePath)}");

        // 模拟SimulatedFileCount个文件处理 - 每个任务都处理同一个内存中的谱面
        const int simulatedFileCount = SimulatedFileCount;

        // 并行分析
        var stopwatch = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, simulatedFileCount)
            .Select(i => Task.Run(() => OsuAnalyzer.Analyze($"simulated_{i}.osu", sampleBeatmap)))
            .ToList();

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // 验证所有结果
        foreach (var result in results)
        {
            Assert.NotNull(result);
            Assert.True(result.XXY_SR >= 0);
            Assert.True(result.MaxKPS >= 0);
        }

        // 计算性能统计
        var totalTime = stopwatch.ElapsedMilliseconds;
        var averageTime = totalTime / (double)simulatedFileCount;
        var throughput = simulatedFileCount / (totalTime / 1000.0); // 文件/秒

        _testOutputHelper.WriteLine(
            $"Concurrent analysis of {simulatedFileCount} simulated files (using 1 real beatmap) took: {totalTime}ms");
        _testOutputHelper.WriteLine($"Average time per beatmap: {averageTime:F2}ms");
        _testOutputHelper.WriteLine($"Throughput: {throughput:F2} beatmaps/second");
        _testOutputHelper.WriteLine(
            $"Total CPU time equivalent: ~{totalTime * Environment.ProcessorCount}ms (if single-threaded)");
        _testOutputHelper.WriteLine($"Sample file location: {sampleFilePath}");
    }
}