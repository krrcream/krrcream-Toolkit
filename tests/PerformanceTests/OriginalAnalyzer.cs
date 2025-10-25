using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tests.PerformanceTests
{
    /// <summary>
    /// 原始版本的分析器 - 单线程实现
    /// </summary>
    public static class OriginalAnalyzer
    {
        public static OsuAnalysisResult Analyze(string filePath, Beatmap beatmap)
        {
            // 单线程计算SR指标
            var calculator = new SRCalculator();

            // compute custom stats via SRCalculator
            int keys = (int)beatmap.DifficultySection.CircleSize;
            float od = beatmap.DifficultySection.OverallDifficulty;
            List<Note> notes = calculator.getNotes(beatmap);
            double xxySr = calculator.Calculate(notes, keys, od, out _);
            double krrLv = -1;

            if (keys <= 10)
            {
                (double a, double b, double c) = keys == 10
                                                     ? (-0.0773, 3.8651, -3.4979)
                                                     : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            // 单线程计算KPS指标
            (int notesCount, double maxKPS, double avgKPS) = CalculateKPSMetrics(beatmap);

            // gather standard metadata with OsuParsers
            string bpmDisplay = GetBPMDisplay(beatmap);

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
                KeyCount = keys,
                OD = od,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Analysis results
                XXY_SR = xxySr,
                KRR_LV = krrLv,
                LNPercent = beatmap.GetLNPercent(),

                // Performance metrics
                NotesCount = notesCount,
                MaxKPS = maxKPS,
                AvgKPS = avgKPS,

                // Beatmap identifiers
                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID

                // Raw beatmap object
                // Beatmap = beatmap
            };

            return result;
        }

        private static (int notesCount, double maxKPS, double avgKPS) CalculateKPSMetrics(Beatmap beatmap)
        {
            List<HitObject> hitObjects = beatmap.HitObjects;
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

        private static string GetBPMDisplay(Beatmap beatmap)
        {
            if (beatmap.TimingPoints.Count == 0)
                return "120.00";

            // 计算平均BPM
            double totalBPM = 0;
            int count = 0;

            foreach (TimingPoint timingPoint in beatmap.TimingPoints)
            {
                if (timingPoint.BeatLength > 0)
                {
                    double bpm = 60000.0 / timingPoint.BeatLength;

                    if (bpm > 0 && bpm < 1000) // 合理的BPM范围
                    {
                        totalBPM += bpm;
                        count++;
                    }
                }
            }

            if (count == 0)
                return "120.00";

            double avgBPM = totalBPM / count;
            return avgBPM.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
