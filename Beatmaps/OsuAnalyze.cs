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
        // 数据量影响内存占用，谨慎添加列表或大型对象

        public string? FilePath;
        public string? FileName;

        // Basic metadata
        public string? Diff { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Creator { get; set; }
        public string? BPMDisplay { get; set; }

        // Difficulty settings
        public double KeyCount { get; set; }
        public double OD { get; set; }
        public double HP { get; set; }

        // Analysis results
        public double XXY_SR { get; set; }
        public double KRR_LV { get; set; }
        public double YLs_LV { get; set; }
        public double LNPercent { get; set; }

        // Performance metrics
        public int NotesCount { get; set; }
        public double MaxKPS { get; set; }
        public double AvgKPS { get; set; }

        // Beatmap identifiers
        public double BeatmapID { get; set; }
        public double BeatmapSetID { get; set; }

        // Status
        public string? Status { get; set; }
    }

    public static class OsuAnalyzer
    {
        /// <summary>
        /// 异步分析谱面文件，自带mania快速检查，返回完整分析结果
        /// <para></para>
        /// 外部应提前预处理路径有效性
        /// </summary>
        /// <returns>分析结果，分析失败时Status字段表示失败状态，其他基础信息仍填充</returns>
        public static async Task<OsuAnalysisResult> AnalyzeAsync(string filePath)
        {
            try
            {
                Beatmap? beatmap = await Task.Run(() => BeatmapDecoder.Decode(filePath));
                if (beatmap == null) throw new InvalidDataException($"OsuAnalyzer Decode Skip:{filePath}.");

                // 填充基础信息
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
                    BPMDisplay = beatmap.GetBPMDisplay(),

                    // Difficulty settings
                    KeyCount = beatmap.DifficultySection.CircleSize,
                    OD = beatmap.DifficultySection.OverallDifficulty,
                    HP = beatmap.DifficultySection.HPDrainRate,

                    // Beatmap identifiers
                    BeatmapID = beatmap.MetadataSection.BeatmapID,
                    BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
                };

                // 检查是否为Mania模式
                if (beatmap.GeneralSection.ModeId != 3) // 3 为Mania模式
                {
                    result.Status = "no-mania";
                    return result;
                }

                if (beatmap.HitObjects.Count == 0)
                {
                    result.Status = "no-notes";
                    return result;
                }

                // 转换为Mania beatmap
                Beatmap maniaBeatmap = beatmap;

                // 异步并行计算
                Task<(double xxySR, double krrLV, double ylsLV)> srTask = Task.Run(() => PerformAnalysis(maniaBeatmap));
                Task<(int notesCount, double maxKPS, double avgKPS)> kpsTask = Task.Run(() => CalculateKPSMetrics(maniaBeatmap));
                // 异步等待并行任务完成
                await Task.WhenAll(srTask, kpsTask);

                (double xxySR, double krrLV, double ylsLV) = srTask.Result;
                (int notesCount, double maxKPS, double avgKPS) = kpsTask.Result;

                // 填充Mania特定分析结果
                result.XXY_SR = xxySR;
                result.KRR_LV = krrLV;
                result.YLs_LV = ylsLV;
                result.LNPercent = maniaBeatmap.GetLNPercent();

                // Performance metrics
                result.NotesCount = notesCount;
                result.MaxKPS = maxKPS;
                result.AvgKPS = avgKPS;

                // Status
                result.Status = "√";

                return result;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[OsuAnalyzer] Analysis failed for {0}: {1}", filePath, ex.Message);
                return new OsuAnalysisResult
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Status = $"Fail"
                };
            }
        }

        private static (double xxySR, double krrLV, double ylsLV) PerformAnalysis(Beatmap beatmap)
        {
            // 创建新的SRCalculator实例，避免多线程竞争
            var calculator = new SRCalculator();

            int keys = (int)beatmap.DifficultySection.CircleSize;
            float od = beatmap.DifficultySection.OverallDifficulty;
            List<Note> notes = calculator.getNotes(beatmap);

            // Handle beatmaps with no hit objects
            if (notes.Count == 0) return (0, 0, 0);

            double xxySR = calculator.Calculate(notes, keys, od, out _);
            double krrLV = CalculateKrrLevel(keys, xxySR);
            double ylsLV = CalculateYlsLevel(xxySR);

            return (xxySR, krrLV, ylsLV);
        }

        private static (int notesCount, double maxKPS, double avgKPS) CalculateKPSMetrics(Beatmap beatmap)
        {
            List<HitObject>? hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, 0);

            // 计算KPS
            List<HitObject> notes = hitObjects.Where(obj => obj is HitCircle || obj is Slider || obj is Spinner)
                                              .OrderBy(obj => obj.StartTime)
                                              .ToList();

            if (notes.Count == 0)
                return (0, 0, 0);

            // 使用滑动窗口计算最大KPS
            const int windowMs = 1000; // 1秒窗口
            double maxKPS = 0;
            double totalKPS = 0;
            int windowCount = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                int count = 1;

                for (int j = i + 1; j < notes.Count; j++)
                {
                    if (notes[j].StartTime - notes[i].StartTime <= windowMs)
                        count++;
                    else
                        break;
                }

                double kps = count;
                maxKPS = Math.Max(maxKPS, kps);
                totalKPS += kps;
                windowCount++;
            }

            double avgKPS = windowCount > 0 ? totalKPS / windowCount : 0;

            return (notes.Count, maxKPS, avgKPS);
        }

        private static double CalculateKrrLevel(int keys, double xxySr)
        {
            double krrLv = -1;

            if (keys <= 10)
            {
                (double a, double b, double c) = keys == 10
                                                     ? (-0.0773, 3.8651, -3.4979)
                                                     : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            return krrLv;
        }

        // YLS LV主要用于8K
        private static double CalculateYlsLevel(double xxyStarRating)
        {
            const double LOWER_BOUND = 2.76257856739498;
            const double UPPER_BOUND = 10.5541834716376;

            if (xxyStarRating is >= LOWER_BOUND and <= UPPER_BOUND) return FittingFormula(xxyStarRating);

            if (xxyStarRating is < LOWER_BOUND and > 0) return 3.6198 * xxyStarRating;

            if (xxyStarRating is > UPPER_BOUND and < 12.3456789) return 2.791 * xxyStarRating + 0.5436;

            return double.NaN;
        }

        private static double FittingFormula(double x)
        {
            // TODO: 凉雨算法，等待实现正确的拟合公式
            return x * 1.5;
        }
    }
}
