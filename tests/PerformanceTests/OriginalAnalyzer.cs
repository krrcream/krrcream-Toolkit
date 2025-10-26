using System.Globalization;
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
        public static OsuAnalysisBasic Analyze(string filePath, Beatmap beatmap)
        {
            // 单线程计算SR指标
            var calculator = SRCalculator.Instance;

            // compute custom stats via SRCalculator
            int    keys  = (int)beatmap.DifficultySection.CircleSize;
            float  od    = beatmap.DifficultySection.OverallDifficulty;
            double xxySr = calculator.CalculateSR(beatmap, out _);
            double krrLv = -1;

            if (keys <= 10)
            {
                (double a, double b, double c) = keys == 10
                                                     ? (-0.0773, 3.8651, -3.4979)
                                                     : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            // 单线程计算音符数量
            int notesCount = beatmap.HitObjects.Count(obj => obj is HitCircle || obj is Slider || obj is Spinner);

            // gather standard metadata with OsuParsers
            string bpmDisplay = GetBPMDisplay(beatmap);

            var result = new OsuAnalysisBasic
            {
                // Basic metadata
                Diff       = beatmap.MetadataSection.Version,
                Title      = beatmap.MetadataSection.Title,
                Artist     = beatmap.MetadataSection.Artist,
                Creator    = beatmap.MetadataSection.Creator,
                BPMDisplay = bpmDisplay,

                // Difficulty settings
                OD = od,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Performance metrics
                NotesCount = notesCount,

                // Beatmap identifiers
                BeatmapID    = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID

                // Raw beatmap object
                // Beatmap = beatmap
            };

            return result;
        }

        private static int CalculateNotesCount(Beatmap beatmap)
        {
            return beatmap.HitObjects.Count(obj => obj is HitCircle || obj is Slider || obj is Spinner);
        }

        private static string GetBPMDisplay(Beatmap beatmap)
        {
            if (beatmap.TimingPoints.Count == 0)
                return "120.00";

            // 计算平均BPM
            double totalBPM = 0;
            int    count    = 0;

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
