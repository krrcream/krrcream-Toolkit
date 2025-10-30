using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LAsOsuBeatmapParser.Analysis;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps
{
    public class OsuAnalysisResult
    {
        // 数据量影响内存占用，谨慎添加列表或大型对象
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // Basic metadata
        public string Diff { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public string BPMDisplay { get; set; } = string.Empty;

        public int BeatmapID { get; set; }
        public int BeatmapSetID { get; set; }

        // Difficulty settings
        public double OD { get; set; }
        public double HP { get; set; }
        public double KeyCount { get; set; }

        public double NotesCount { get; set; }
        public double LN_Percent { get; set; }
        public double MaxKPS { get; set; }
        public double AvgKPS { get; set; }

        // Analysis results
        public double XXY_SR { get; set; }
        public double KRR_LV { get; set; }
        public double YLs_LV { get; set; }
    }

    public static class OsuAnalyzer
    {
        /// <summary>
        /// 快速异步获取谱面基础信息，不进行复杂分析计算
        /// <para></para>
        /// 外部应提前预处理路径有效性和解码操作
        /// </summary>
        /// <returns>基础信息对象</returns>
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

                Task<(double maxKPS, double avgKPS)> kpsTask = Task.Run(() => CalculateKPSMetrics(beatmap));
                Task<(double xxySR, double krrLV, double ylsLV)> srTask = Task.Run(() =>
                {
                    // Rust版有问题，部分文件会抛异常导致程序崩溃，暂时用C#版替代
                    // double sr = SRCalculatorRust.CalculateSR_FromFile(filePath);
                    // C#版遇到不那么规范的谱面会返回0，不崩溃，但是不好调试
                    double sr = LAsOsuBeatmapParser.Analysis.SRCalculator.Instance.CalculateSRFromFileCS(filePath);
                    int keys = (int)beatmap.DifficultySection.CircleSize;

                    double xxySR = sr;
                    double krrLV = CalculateKrrLevel(keys, xxySR);
                    double ylsLV = CalculateYlsLevel(xxySR);

                    return (xxySR, krrLV, ylsLV);
                });

                // 先等待KPS，保证KPS一定有
                (double maxKPS, double avgKPS) = await kpsTask;

                result.NotesCount = beatmap.HitObjects.Count;
                result.MaxKPS = maxKPS;
                result.AvgKPS = avgKPS;
                result.LN_Percent = beatmap.GetLNPercent();

                // SR分析用try包裹，失败不影响KPS
                try
                {
                    (double xxySR, double krrLV, double ylsLV) = await srTask;
                    result.XXY_SR = xxySR;
                    result.KRR_LV = krrLV;
                    result.YLs_LV = ylsLV;
                    result.Status = "√";
                }
                catch (Exception e)
                {
                    result.Status = "no-SR";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new OsuAnalysisResult
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Status = "Fail"
                };
            }
        }

        private static (double maxKPS, double avgKPS) CalculateKPSMetrics(Beatmap beatmap)
        {
            List<HitObject>? hitObjects = beatmap.HitObjects;

            // 计算KPS
            List<HitObject> notes = hitObjects.Where(obj => obj is HitCircle || obj is Slider || obj is Spinner)
                                              .OrderBy(obj => obj.StartTime)
                                              .ToList();

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

            return (maxKPS, avgKPS);
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
