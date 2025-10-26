using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.Beatmaps
{
    public class SRCalculatorTests
    {
        private readonly ITestOutputHelper _output;

        // SR显示精度控制常量
        private const int SR_DECIMAL_PLACES = 4;

        public SRCalculatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [MemoryDiagnoser]
        public class SRCalculatorBenchmarks
        {
            private List<SRsNote>      _testNotes;
            private Beatmap         _testBeatmap;
            private SRCalculator    _newCalculator;
            private OldSRCalculator _oldCalculator;

            [GlobalSetup]
            public void Setup()
            {
                // 创建测试数据：一个中等复杂度的谱面
                _testNotes = new List<SRsNote>();
                var random     = new Random(42); // 固定种子确保一致性
                int totalNotes = 1000;
                int maxTime    = 180000; // 3分钟

                for (int i = 0; i < totalNotes; i++)
                {
                    int time   = (int)(i * (maxTime / (double)totalNotes));
                    int column = random.Next(0, 4);                                           // 4K谱面
                    int tail   = random.Next(0, 10) < 2 ? time + random.Next(500, 2000) : -1; // 20% LN
                    _testNotes.Add(new SRsNote(column, time, tail));
                }

                _testBeatmap                                     = new Beatmap();
                _testBeatmap.DifficultySection.CircleSize        = 4;
                _testBeatmap.DifficultySection.OverallDifficulty = 8.0f;
                _testBeatmap.HitObjects = _testNotes.Select(note =>
                {
                    var hitObject = new HitObject();
                    hitObject.Position  = new Vector2((float)((note.K + 0.5) * 512.0 / 4), 0);
                    hitObject.StartTime = note.H;
                    hitObject.EndTime   = note.T >= 0 ? note.T : hitObject.StartTime;
                    return hitObject;
                }).ToList();

                _newCalculator = SRCalculator.Instance;
                _oldCalculator = new OldSRCalculator();
            }

            [Benchmark]
            public double NewSRCalculator()
            {
                return _newCalculator.CalculateSR(_testBeatmap, out _);
            }

            [Benchmark]
            public double OldSRCalculator()
            {
                return _oldCalculator.Calculate(_testNotes, 4, 8.0, out _);
            }
        }

        [Fact]
        public void RunDetailedPerformanceAnalysis()
        {
            // 加载真实的谱面文件 (4k-10k)
            string[] testFiles = new[]
            {
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [4k Original by Leo137].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [5k-1].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [6k-1].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [7k-1].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [8k-1].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [9k-1].osu",
                @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [10k-1].osu"
            };

            Beatmap[] beatmaps  = testFiles.Select(f => BeatmapDecoder.Decode(f)).ToArray();
            int[]     keyCounts = beatmaps.Select(bm => (int)bm.DifficultySection.CircleSize).ToArray();

            _output.WriteLine($"=== SR计算详细性能分析 (4k-10k) === 列队{beatmaps.Length}，运行3次以获取平均时间...");
            _output.WriteLine("");

            // 计算每个版本的 SR 和时间
            var newSRs      = new List<double>();
            var oldSRs      = new List<double>();
            var originalSRs = new List<double>();

            var newTimesList      = new List<Dictionary<string, long>>();
            var oldTimesList      = new List<Dictionary<string, long>>();
            var originalTimesList = new List<Dictionary<string, long>>();

            foreach (Beatmap bm in beatmaps)
            {
                List<SRsNote> notes    = SRCalculator.Instance.getNotes(bm);
                int        keyCount = (int)bm.DifficultySection.CircleSize;
                double     od       = bm.DifficultySection.OverallDifficulty;

                // 新版本
                double newSrSum    = 0;
                var    newTimesSum = new Dictionary<string, long>();

                for (int i = 0; i < 3; i++)
                {
                    double sr = SRCalculator.Instance.CalculateSR(bm, out Dictionary<string, long> times);
                    newSrSum += sr;

                    foreach (KeyValuePair<string, long> kv in times)
                    {
                        if (!newTimesSum.ContainsKey(kv.Key)) newTimesSum[kv.Key] = 0;
                        newTimesSum[kv.Key] += kv.Value;
                    }
                }

                newSRs.Add(newSrSum / 3);
                newTimesList.Add(newTimesSum.ToDictionary(kv => kv.Key, kv => kv.Value / 3));

                // 旧版本
                double oldSrSum    = 0;
                var    oldTimesSum = new Dictionary<string, long>();

                for (int i = 0; i < 3; i++)
                {
                    double sr = new OldSRCalculator().Calculate(notes, keyCount, od, out Dictionary<string, long> times);
                    oldSrSum += sr;

                    foreach (KeyValuePair<string, long> kv in times)
                    {
                        if (!oldTimesSum.ContainsKey(kv.Key)) oldTimesSum[kv.Key] = 0;
                        oldTimesSum[kv.Key] += kv.Value;
                    }
                }

                oldSRs.Add(oldSrSum / 3);
                oldTimesList.Add(oldTimesSum.ToDictionary(kv => kv.Key, kv => kv.Value / 3));

                // 原始版本
                double originalSrSum    = 0;
                var    originalTimesSum = new Dictionary<string, long>();

                for (int i = 0; i < 3; i++)
                {
                    double sr = new OriginalSRCalculator().Calculate(notes, keyCount, od, out Dictionary<string, long> times);
                    originalSrSum += sr;

                    foreach (KeyValuePair<string, long> kv in times)
                    {
                        if (!originalTimesSum.ContainsKey(kv.Key)) originalTimesSum[kv.Key] = 0;
                        originalTimesSum[kv.Key] += kv.Value;
                    }
                }

                originalSRs.Add(originalSrSum / 3);
                originalTimesList.Add(originalTimesSum.ToDictionary(kv => kv.Key, kv => kv.Value / 3));
            }

            // 计算平均时间 (使用10k谱面作为基准)
            int                      benchmarkIndex   = 6; // 10k谱面索引
            Dictionary<string, long> avgNewTimes      = newTimesList[benchmarkIndex];
            Dictionary<string, long> avgOldTimes      = oldTimesList[benchmarkIndex];
            Dictionary<string, long> avgOriginalTimes = originalTimesList[benchmarkIndex];

            // 使用10k谱面的 SR 作为代表
            double avgNewSR      = newSRs[benchmarkIndex];
            double avgOldSR      = oldSRs[benchmarkIndex];
            double avgOriginalSR = originalSRs[benchmarkIndex];

            // 计算一致性 (标准差)
            double CalculateStdDev(List<double> values)
            {
                double avg = values.Average();
                return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / values.Count);
            }

            double newConsistency      = CalculateStdDev(newSRs);
            double oldConsistency      = CalculateStdDev(oldSRs);
            double originalConsistency = CalculateStdDev(originalSRs);

            // 生成 ASCII 表格
            string[] sections        = new[] { "Section232425", "Section2627", "Section3", "Total" };
            string[] displaySections = new[] { "Section23/24/25", "Section26/27", "Section3", "Total" };
            int[]    colWidths       = new[] { 8, 15, 11, 7, 5, 6, 10 }; // 版本, Section23/24/25, Section26/27, Section3, Total, SR, 一致性

            // 表头
            string header =
                $"| {"版本".PadRight(colWidths[0])} | {displaySections[0].PadRight(colWidths[1])} | {displaySections[1].PadRight(colWidths[2])} | {displaySections[2].PadRight(colWidths[3])} | {displaySections[3].PadRight(colWidths[4])} | {"SR".PadRight(colWidths[5])} | {"一致性".PadRight(colWidths[6])} |";
            string separator = $"+{string.Join("+", colWidths.Select(w => new string('-', w + 2)))}+";

            _output.WriteLine(separator);
            _output.WriteLine(header);
            _output.WriteLine(separator);

            // 旧版本行
            string[] oldTimes = sections.Select(s => avgOldTimes.GetValueOrDefault(s, 0).ToString("F1").PadLeft(colWidths[Array.IndexOf(sections, s) + 1]))
                                        .ToArray();
            string oldSRStr          = avgOldSR.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(colWidths[5]);
            string oldConsistencyStr = oldConsistency.ToString("F4").PadLeft(colWidths[6]);
            _output.WriteLine(
                $"| {"旧版本".PadRight(colWidths[0])} | {oldTimes[0]} | {oldTimes[1]} | {oldTimes[2]} | {oldTimes[3]} | {oldSRStr} | {oldConsistencyStr} |");

            // 原始版本行
            string[] originalTimes = sections.Select(s => avgOriginalTimes.GetValueOrDefault(s, 0).ToString("F1").PadLeft(colWidths[Array.IndexOf(sections, s) + 1]))
                                             .ToArray();
            string originalSRStr          = avgOriginalSR.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(colWidths[5]);
            string originalConsistencyStr = originalConsistency.ToString("F4").PadLeft(colWidths[6]);
            _output.WriteLine(
                $"| {"原始版本".PadRight(colWidths[0])} | {originalTimes[0]} | {originalTimes[1]} | {originalTimes[2]} | {originalTimes[3]} | {originalSRStr} | {originalConsistencyStr} |");

            // 新版本行
            string[] newTimes = sections.Select(s => avgNewTimes.GetValueOrDefault(s, 0).ToString("F1").PadLeft(colWidths[Array.IndexOf(sections, s) + 1]))
                                        .ToArray();
            string newSRStr          = avgNewSR.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(colWidths[5]);
            string newConsistencyStr = newConsistency.ToString("F4").PadLeft(colWidths[6]);
            _output.WriteLine(
                $"| {"新版本".PadRight(colWidths[0])} | {newTimes[0]} | {newTimes[1]} | {newTimes[2]} | {newTimes[3]} | {newSRStr} | {newConsistencyStr} |");

            _output.WriteLine(separator);

            // 新表：4-10k 详细数据
            _output.WriteLine("=== 4-10k 详细数据 ===");
            string[] kLabels         = keyCounts.Select(k => $"{k}k").ToArray();
            int[]    detailColWidths = new[] { 10 }.Concat(Enumerable.Repeat(8, kLabels.Length)).ToArray(); // 项目, 4k, 5k, ...
            string   detailHeader    = $"| {"项目".PadRight(detailColWidths[0])} | {string.Join(" | ", kLabels.Select((k, i) => k.PadRight(detailColWidths[i + 1])))} |";
            string   detailSeparator = $"+{string.Join("+", detailColWidths.Select(w => new string('-', w + 2)))}+";

            _output.WriteLine(detailSeparator);
            _output.WriteLine(detailHeader);
            _output.WriteLine(detailSeparator);

            // 旧SR 行
            string oldSrRow = $"| {"旧SR".PadRight(detailColWidths[0])} | {string.Join(" | ", oldSRs.Select(sr => sr.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(oldSrRow);

            // 原始SR 行
            string originalSrRow = $"| {"原始SR".PadRight(detailColWidths[0])} | {string.Join(" | ", originalSRs.Select(sr => sr.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(originalSrRow);

            // 新SR 行
            string newSrRow = $"| {"新SR".PadRight(detailColWidths[0])} | {string.Join(" | ", newSRs.Select(sr => sr.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(newSrRow);

            // 旧总用时 行
            string oldTimeRow =
                $"| {"旧总用时".PadRight(detailColWidths[0])} | {string.Join(" | ", oldTimesList.Select(t => t.GetValueOrDefault("Total", 0).ToString("F1").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(oldTimeRow);

            // 原始总用时 行
            string originalTimeRow =
                $"| {"原始总用时".PadRight(detailColWidths[0])} | {string.Join(" | ", originalTimesList.Select(t => t.GetValueOrDefault("Total", 0).ToString("F1").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(originalTimeRow);

            // 新总用时 行
            string newTimeRow =
                $"| {"新总用时".PadRight(detailColWidths[0])} | {string.Join(" | ", newTimesList.Select(t => t.GetValueOrDefault("Total", 0).ToString("F1").PadLeft(detailColWidths[1])))} |";
            _output.WriteLine(newTimeRow);

            _output.WriteLine(detailSeparator);
            _output.WriteLine("");

            // 验证SR结果一致性
            const double SR_TOLERANCE = 0.0001;
            double       oldSrDiff    = Math.Abs(avgOldSR - avgOriginalSR);
            double       newSrDiff    = Math.Abs(avgNewSR - avgOriginalSR);

            Assert.True(oldSrDiff < SR_TOLERANCE,
                        $"旧版本SR结果不一致。差异: {oldSrDiff:F6}, 允许误差: {SR_TOLERANCE:F6}");
            Assert.True(newSrDiff < SR_TOLERANCE,
                        $"新版本SR结果不一致。差异: {newSrDiff:F6}, 允许误差: {SR_TOLERANCE:F6}");

            _output.WriteLine($"✅ SR结果一致性验证通过");
            _output.WriteLine($"  原始版本SR: {avgOriginalSR:F6}");
            _output.WriteLine($"  旧版本SR差异: {oldSrDiff:F6}");
            _output.WriteLine($"  新版本SR差异: {newSrDiff:F6}");

            // 断言检查：4-10k每个的SR都有0.001精度无偏差（新版本 vs 旧版本）
            const double PRECISION_TOLERANCE = 0.001;

            for (int i = 0; i < keyCounts.Length; i++)
            {
                double diff = Math.Abs(newSRs[i] - oldSRs[i]);
                Assert.True(diff < PRECISION_TOLERANCE,
                            $"{keyCounts[i]}k SR精度不满足要求。新SR: {newSRs[i]:F6}, 旧SR: {oldSRs[i]:F6}, 差异: {diff:F6}, 允许误差: {PRECISION_TOLERANCE:F6}");
            }

            _output.WriteLine($"✅ 4-10k SR精度验证通过 (精度: {PRECISION_TOLERANCE:F6})");
        }
    }
}
