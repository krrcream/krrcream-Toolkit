using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps
{
    public class OsuAnalysisResult
    {
        // File information
        public string? FilePath { get; init; }
        public string? FileName { get; init; }
        
        // Basic metadata
        public string? Diff { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Creator { get; init; }
        public string? BPMDisplay { get; init; }
        
        // Difficulty settings
        public double Keys { get; init; }
        public double OD { get; init; }
        public double HP { get; init; }

        // Analysis results
        public double XXY_SR { get; init; }
        public double KRR_LV { get; init; }
        public double LNPercent { get; init; }

        // Performance metrics
        public int NotesCount { get; init; }
        public double MaxKPS { get; init; }
        public double AvgKPS { get; init; }

        // Beatmap identifiers
        public double BeatmapID { get; init; }
        public double BeatmapSetID { get; init; }
        
        // Raw beatmap object (optional, for advanced usage)
        public Beatmap? Beatmap { get; init; }
    }

    public static class OsuAnalyzer
    {
        public static async Task<OsuAnalysisResult> AnalyzeAsync(string filePath)
        {
            var beatmap = await Task.Run(() => BeatmapDecoder.Decode(filePath));

            // 异步并行计算SR和KPS指标
            var srTask = Task.Run(() => PerformAnalysis(beatmap));
            var kpsTask = Task.Run(() => CalculateKPSMetrics(beatmap));

            // 异步等待并行任务完成
            await Task.WhenAll(srTask, kpsTask);
            var (Keys1, OD1, xxySR, krrLV) = srTask.Result;
            var (notesCount, maxKPS, avgKPS) = kpsTask.Result;
            
            // gather standard metadata with OsuParsers
            var bpmDisplay = await Task.Run(() => GetBPMDisplay(beatmap));

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

        /// <summary>
        /// 同步版本 - 已过时，请使用 AnalyzeAsync
        /// </summary>
        [Obsolete("此方法已过时，请使用 AnalyzeAsync 异步版本")]
        public static OsuAnalysisResult Analyze(string filePath)
        {
            return AnalyzeAsync(filePath).GetAwaiter().GetResult();
        }

        public static async Task<OsuAnalysisResult> AnalyzeAsync(string filePath, Beatmap beatmap)
        {
            // 异步并行计算SR和KPS指标
            var srTask = Task.Run(() => PerformAnalysis(beatmap));
            var kpsTask = Task.Run(() => CalculateKPSMetrics(beatmap));

            // 异步等待并行任务完成
            await Task.WhenAll(srTask, kpsTask);
            var (Keys1, OD1, xxySR, krrLV) = srTask.Result;
            var (notesCount, maxKPS, avgKPS) = kpsTask.Result;
            var bpmDisplay = await Task.Run(() => GetBPMDisplay(beatmap));
            var result = new OsuAnalysisResult
            {
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

        /// <summary>
        /// 同步版本 - 已过时，请使用 AnalyzeAsync
        /// </summary>
        [Obsolete("此方法已过时，请使用 AnalyzeAsync 异步版本")]
        public static OsuAnalysisResult Analyze(string filePath, Beatmap beatmap)
        {
            return AnalyzeAsync(filePath, beatmap).GetAwaiter().GetResult();
        }
        
        private static string GetBPMDisplay(Beatmap beatmap)
        {
            var bpm = beatmap.MainBPM;
            var bpmMax = beatmap.MaxBPM;
            var bpmMin = beatmap.MinBPM;
            
            string BPMFormat = string.Format(CultureInfo.InvariantCulture, "{0}({1} - {2})", bpm, bpmMin, bpmMax);
                
            return BPMFormat;
        }

        private static (int keys, double od, double xxySr, double krrLv) PerformAnalysis(Beatmap beatmap)
        {
            // 创建新的SRCalculator实例，避免多线程竞争
            var calculator = new SRCalculator();

            var keys = (int)beatmap.DifficultySection.CircleSize;
            var od = beatmap.DifficultySection.OverallDifficulty;
            var notes = calculator.getNotes(beatmap);
            double xxySr = calculator.Calculate(notes, keys, od, out _);
            double krrLv = CalculateKrrLevel(keys, xxySr);

            return (keys, od, xxySr, krrLv);
        }

        private static double CalculateKrrLevel(int keys, double xxySr)
        {
            double krrLv = -1;
            if (keys <= 10)
            {
                var (a, b, c) = keys == 10
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            return krrLv;
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

        public static List<double> GetBeatLengthAxis(Dictionary<double, double> beatLengthDict, double mainBPM,
            List<int> timeAxis)
        {
            double defaultLength = 60000 / mainBPM;
            List<double> bLAxis = new List<double>();
            for (int i = 0; i < timeAxis.Count; i++)
            {
                bLAxis.Add(defaultLength);
            }

            // 将字典的键转换为有序列表，便于比较
            var sortedKeys = beatLengthDict.Keys.OrderBy(k => k).ToList();

            for (int i = 0; i < timeAxis.Count; i++)
            {
                double currentTime = timeAxis[i];

                // 处理边界情况：时间点在第一个时间点之前
                if (currentTime < sortedKeys[0])
                {
                    bLAxis[i] = beatLengthDict[sortedKeys[0]];
                    continue;
                }

                // 处理边界情况：时间点在最后一个时间点之后
                if (currentTime >= sortedKeys[^1])
                {
                    bLAxis[i] = beatLengthDict[sortedKeys[^1]];
                    continue;
                }

                // 查找当前时间点对应的时间段
                for (int k = 0; k < sortedKeys.Count - 1; k++)
                {
                    if (currentTime >= sortedKeys[k] && currentTime < sortedKeys[k + 1])
                    {
                        bLAxis[i] = beatLengthDict[sortedKeys[k]];
                        break;
                    }
                }
            }

            return bLAxis;
        }

        /// <summary>
        /// 计算 YLS LV (基于 XXY SR)
        /// </summary>
        public static double CalculateYlsLevel(double xxyStarRating)
        {
            const double LOWER_BOUND = 2.76257856739498;
            const double UPPER_BOUND = 10.5541834716376;

            if (xxyStarRating is >= LOWER_BOUND and <= UPPER_BOUND)
            {
                return FittingFormula(xxyStarRating);
            }

            if (xxyStarRating is < LOWER_BOUND and > 0)
            {
                return 3.6198 * xxyStarRating;
            }

            if (xxyStarRating is > UPPER_BOUND and < 12.3456789)
            {
                return (2.791 * xxyStarRating) + 0.5436;
            }

            return double.NaN;
        }

        private static double FittingFormula(double x)
        {
            // TODO: 实现正确的拟合公式
            // For now, returning a placeholder value
            return x * 1.5; // Replace with actual formula
        }
    }
}