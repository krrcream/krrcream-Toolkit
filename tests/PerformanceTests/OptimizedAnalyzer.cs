using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tests.PerformanceTests;

/// <summary>
/// 优化版本的分析器 - 并行处理实现
/// </summary>
public static class OptimizedAnalyzer
{
    public static OsuAnalysisResult Analyze(string filePath, Beatmap beatmap)
    {
        // 并行计算SR和KPS指标
        var srTask = Task.Run(() =>
        {
            // 创建新的SRCalculator实例，避免多线程竞争
            var calculator = new SRCalculator();

            // compute custom stats via SRCalculator
            var keys = (int)beatmap.DifficultySection.CircleSize;
            var od = beatmap.DifficultySection.OverallDifficulty;
            var notes = calculator.getNotes(beatmap);
            var xxySr = calculator.Calculate(notes, keys, od, out _);
            double krrLv = -1;
            if (keys <= 10)
            {
                var (a, b, c) = keys == 10
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);

                var LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            return (keys, od, xxySr, krrLv);
        });

        var kpsTask = Task.Run(() => { return CalculateKPSMetrics(beatmap); });

        // 等待并行任务完成
        Task.WaitAll(srTask, kpsTask);
        var (Keys1, OD1, xxySR, krrLV) = srTask.Result;
        var (notesCount, maxKPS, avgKPS) = kpsTask.Result;

        // gather standard metadata with OsuParsers
        var bpmDisplay = GetBPMDisplay(beatmap);

        var result = new OsuAnalysisResult
        {
            // File information
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),

            // Basic metadata
            Diff = beatmap.MetadataSection.Version,
            Title = beatmap.MetadataSection.Title,
            Artist = beatmap.MetadataSection.Artist,
            Creator = beatmap.MetadataSection.Creator,
            BPMDisplay = bpmDisplay,

            // Difficulty settings
            Keys = Keys1,
            OD = OD1,
            HP = beatmap.DifficultySection.HPDrainRate,

            // Analysis results
            XXY_SR = xxySR,
            KRR_LV = krrLV,
            LNPercent = beatmap.GetLNPercent(),

            // Performance metrics
            NotesCount = notesCount,
            MaxKPS = maxKPS,
            AvgKPS = avgKPS,

            // Beatmap identifiers
            BeatmapID = beatmap.MetadataSection.BeatmapID,
            BeatmapSetID = beatmap.MetadataSection.BeatmapSetID,

            // Raw beatmap object
            Beatmap = beatmap
        };

        return result;
    }

    private static (int notesCount, double maxKPS, double avgKPS) CalculateKPSMetrics(Beatmap beatmap)
    {
        var hitObjects = beatmap.HitObjects;
        if (hitObjects.Count == 0)
            return (0, 0, 0);

        // 计算KPS
        var notes = hitObjects.Where(obj => obj is HitCircle || obj is Slider || obj is Spinner)
            .OrderBy(obj => obj.StartTime)
            .ToList();

        if (notes.Count == 0)
            return (0, 0, 0);

        // 使用滑动窗口计算最大KPS
        const int windowMs = 1000; // 1秒窗口
        double maxKPS = 0;
        double totalKPS = 0;
        var windowCount = 0;

        for (var i = 0; i < notes.Count; i++)
        {
            var count = 1;
            for (var j = i + 1; j < notes.Count; j++)
                if (notes[j].StartTime - notes[i].StartTime <= windowMs)
                    count++;
                else
                    break;

            double kps = count;
            maxKPS = Math.Max(maxKPS, kps);
            totalKPS += kps;
            windowCount++;
        }

        var avgKPS = windowCount > 0 ? totalKPS / windowCount : 0;

        return (notes.Count, maxKPS, avgKPS);
    }

    private static string GetBPMDisplay(Beatmap beatmap)
    {
        if (beatmap.TimingPoints.Count == 0)
            return "120.00";

        // 计算平均BPM
        double totalBPM = 0;
        var count = 0;

        foreach (var timingPoint in beatmap.TimingPoints)
            if (timingPoint.BeatLength > 0)
            {
                var bpm = 60000.0 / timingPoint.BeatLength;
                if (bpm > 0 && bpm < 1000) // 合理的BPM范围
                {
                    totalBPM += bpm;
                    count++;
                }
            }

        if (count == 0)
            return "120.00";

        var avgBPM = totalBPM / count;
        return avgBPM.ToString("F2", CultureInfo.InvariantCulture);
    }
}