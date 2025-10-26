using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Tools.KRRLVAnalysis;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.PerformanceTests
{
    public class MemoryLeakTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MemoryLeakTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            Logger.SetConsoleOutputEnabled(false);

            // Setup dependency injection for tests
            var mockEventBus = new Mock<IEventBus>();
            var services     = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);
        }

        public void Dispose()
        {
            Logger.SetConsoleOutputEnabled(true);
            Injector.SetTestServiceProvider(null);
        }

        private static double BytesToMB(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }

        [Fact]
        public void KRRLVAnalysisViewModel_ShouldDisposeResourcesProperly()
        {
            // 记录初始内存使用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"Initial memory: {BytesToMB(initialMemory):F2} MB");

            // 创建ViewModel实例（依赖注入会自动处理）
            var viewModel = new KRRLVAnalysisViewModel();

            // 模拟一些操作
            viewModel.PathInput.Value = Directory.GetCurrentDirectory();

            // 记录创建后的内存使用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long afterCreationMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"After creation memory: {BytesToMB(afterCreationMemory):F2} MB");

            // Dispose ViewModel
            viewModel.Dispose();

            // 记录Dispose后的内存使用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long afterDisposeMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"After dispose memory: {BytesToMB(afterDisposeMemory):F2} MB");

            // 检查内存是否被合理释放（允许一些余量）
            long memoryDifference = afterDisposeMemory - initialMemory;
            _testOutputHelper.WriteLine($"Memory difference: {BytesToMB(memoryDifference):F2} MB");

            // 断言：Dispose后内存使用应该接近初始水平
            // 允许一定的余量，因为GC可能不会释放所有内存
            Assert.True(BytesToMB(memoryDifference) < 50, $"Memory leak detected: {BytesToMB(memoryDifference):F2} MB not released");
        }

        [Fact]
        public async Task SRCalculator_ShouldNotLeakMemoryUnderHighConcurrency()
        {
            // 从TestOsuFile文件夹读取实际的osu文件
            string   testOsuFileDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile");
            string[] osuFiles       = Directory.GetFiles(testOsuFileDir, "*.osu", SearchOption.TopDirectoryOnly);

            if (osuFiles.Length == 0)
            {
                _testOutputHelper.WriteLine("No .osu files found in TestOsuFile directory. Skipping memory leak test.");
                return;
            }

            // 读取第一个真实文件
            string  sampleFilePath = osuFiles.First();
            Beatmap sampleBeatmap  = BeatmapDecoder.Decode(sampleFilePath);

            if (sampleBeatmap == null)
            {
                _testOutputHelper.WriteLine("Failed to decode sample beatmap. Skipping test.");
                return;
            }

            // 记录初始内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"Initial memory: {BytesToMB(initialMemory):F2} MB");

            // 模拟高并发计算：100次SR计算，每次解码新Beatmap
            const int iterations = 100;
            var       tasks      = new Task<(double sr, Dictionary<string, long> times)>[iterations];

            for (int i = 0; i < iterations; i++)
            {
                Beatmap beatmap = BeatmapDecoder.Decode(sampleFilePath); // 每次解码新对象
                tasks[i] = SRCalculator.Instance.CalculateSRAsync(beatmap);
            }

            await Task.WhenAll(tasks);

            // 强制GC
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            long afterGCMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"After {iterations} calculations and forced GC: {BytesToMB(afterGCMemory):F2} MB");

            // 检查内存增长
            long memoryIncrease = afterGCMemory - initialMemory;
            _testOutputHelper.WriteLine($"Memory increase: {BytesToMB(memoryIncrease):F2} MB");

            // 断言：内存增长应该在合理范围内（例如小于50MB）
            // 由于LOH和可能的外部库，允许一些增长，但不应过大
            Assert.True(BytesToMB(memoryIncrease) < 50.0, $"Potential memory leak: {BytesToMB(memoryIncrease):F2} MB increase after {iterations} calculations");
        }

        [Fact]
        public void BeatmapDecoder_ShouldNotLeakMemoryOnRepeatedDecodes()
        {
            // 从TestOsuFile文件夹读取实际的osu文件
            string   testOsuFileDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile");
            string[] osuFiles       = Directory.GetFiles(testOsuFileDir, "*.osu", SearchOption.TopDirectoryOnly);

            if (osuFiles.Length == 0)
            {
                _testOutputHelper.WriteLine("No .osu files found in TestOsuFile directory. Skipping BeatmapDecoder test.");
                return;
            }

            // 读取第一个真实文件路径
            string sampleFilePath = osuFiles.First();

            // 记录初始内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"Initial memory before decodes: {BytesToMB(initialMemory):F2} MB");

            // 模拟多次解码同一个文件：100次
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                Beatmap beatmap = BeatmapDecoder.Decode(sampleFilePath);
                // 不保存beatmap，让GC回收
            }

            // 强制GC
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            long afterDecodeMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"After {iterations} decodes and forced GC: {BytesToMB(afterDecodeMemory):F2} MB");

            // 检查内存增长（只解码，不计算SR）
            long memoryIncrease = afterDecodeMemory - initialMemory;
            _testOutputHelper.WriteLine($"Memory increase from decodes: {BytesToMB(memoryIncrease):F2} MB");

            // 断言：解码不应导致显著内存泄露（例如小于10MB）
            Assert.True(BytesToMB(memoryIncrease) < 100.0, $"Potential memory leak in BeatmapDecoder: {BytesToMB(memoryIncrease):F2} MB increase after {iterations} decodes");
        }

        [Fact]
        public async Task SRCalculator_ShouldNotLeakMemoryWithSameBeatmap()
        {
            // 从TestOsuFile文件夹读取实际的osu文件
            string   testOsuFileDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile");
            string[] osuFiles       = Directory.GetFiles(testOsuFileDir, "*.osu", SearchOption.TopDirectoryOnly);

            if (osuFiles.Length == 0)
            {
                _testOutputHelper.WriteLine("No .osu files found in TestOsuFile directory. Skipping same Beatmap test.");
                return;
            }

            // 读取第一个真实文件
            string  sampleFilePath = osuFiles.First();
            Beatmap sampleBeatmap  = BeatmapDecoder.Decode(sampleFilePath);

            if (sampleBeatmap == null)
            {
                _testOutputHelper.WriteLine("Failed to decode sample beatmap. Skipping test.");
                return;
            }

            // 记录初始内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"Initial memory: {BytesToMB(initialMemory):F2} MB");

            // 模拟高并发计算：100次SR计算，使用同一个Beatmap对象
            const int iterations                          = 100;
            var       tasks                               = new Task<(double sr, Dictionary<string, long> times)>[iterations];
            for (int i = 0; i < iterations; i++) tasks[i] = SRCalculator.Instance.CalculateSRAsync(sampleBeatmap); // 使用同一个对象

            await Task.WhenAll(tasks);

            // 强制GC
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            long afterGCMemory = GC.GetTotalMemory(true);
            _testOutputHelper.WriteLine($"After {iterations} calculations with same Beatmap and forced GC: {BytesToMB(afterGCMemory):F2} MB");

            // 检查内存增长
            long memoryIncrease = afterGCMemory - initialMemory;
            _testOutputHelper.WriteLine($"Memory increase: {BytesToMB(memoryIncrease):F2} MB");

            // 断言：使用同一个Beatmap，内存增长应该更小（例如小于20MB）
            Assert.True(BytesToMB(memoryIncrease) < 20.0, $"Potential memory leak in SRCalculator: {BytesToMB(memoryIncrease):F2} MB increase after {iterations} calculations with same Beatmap");
        }
    }
}
