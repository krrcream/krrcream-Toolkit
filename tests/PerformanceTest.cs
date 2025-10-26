using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // 加载文件
        string       directoryPath = @"F:\MUG OSU\osu test\Songs\la's map"; // 替换路径
        List<string> osuFiles      = Directory.GetFiles(directoryPath, "*.osu", SearchOption.AllDirectories).Take(200).ToList();
        Console.WriteLine($"Loaded {osuFiles.Count} files.");

        // 测试不同并发
        await TestConcurrency(osuFiles, 4); // 当前
        await TestConcurrency(osuFiles, 8);
        await TestConcurrency(osuFiles, Environment.ProcessorCount);
    }

    private static async Task TestConcurrency(List<string> files, int maxConcurrency)
    {
        var  semaphore     = new SemaphoreSlim(maxConcurrency);
        var  stopwatch     = Stopwatch.StartNew();
        long initialMemory = GC.GetTotalMemory(true);

        IEnumerable<Task<(OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance)>> tasks = files.Select(async file =>
        {
            await semaphore.WaitAsync();

            try
            {
                Beatmap beatmap = BeatmapDecoder.Decode(file);
                if (beatmap == null) return default;
                OsuAnalysisBasic       basicInfo   = await OsuAnalyzer.AnalyzeBasicInfoAsync(beatmap);
                OsuAnalysisPerformance performance = await OsuAnalyzer.AnalyzeAdvancedAsync(beatmap);
                return (basicInfo, performance);
            }
            finally
            {
                semaphore.Release();
            }
        });

        (OsuAnalysisBasic basicInfo, OsuAnalysisPerformance performance)[] results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        long finalMemory = GC.GetTotalMemory(false);

        Console.WriteLine($"Concurrency {maxConcurrency}: Time {stopwatch.Elapsed.TotalSeconds}s, Memory {(finalMemory - initialMemory) / 1024 / 1024} MB");
    }
}
