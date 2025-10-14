using System;
using System.Collections.Generic;
using System.Linq;
using krrTools.Beatmaps;
using Xunit;
using BenchmarkDotNet.Attributes;
using OsuParsers.Decoders;
using Xunit.Abstractions;

namespace krrTools.Tests.Beatmaps;

public class SRCalculatorTests
{
    private readonly ITestOutputHelper _output;

    // SR显示精度控制常量
    private const int SR_DECIMAL_PLACES = 4;

    public SRCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Calculate_EmptyNoteSequence_ThrowsException()
    {
        // Arrange
        var calculator = new SRCalculator();
        var noteSequence = new List<Note>();
        var keyCount = 4;
        var od = 8.0;

        // Assert
        Assert.Throws<InvalidOperationException>(() => calculator.Calculate(noteSequence, keyCount, od, out _));
    }

    [Fact]
    public void Calculate_SingleNote_ReturnsPositiveValue()
    {
        // Arrange
        var calculator = new SRCalculator();
        var noteSequence = new List<Note> { new(3, 5, 1000) };
        var keyCount = 4;
        var od = 8.0;

        // Act
        var result = calculator.Calculate(noteSequence, keyCount, od, out _);

        // Assert
        Assert.True(result >= 0);
    }

    [Fact]
    public void Calculate_MultipleNotes_ReturnsHigherValue()
    {
        // Arrange
        var calculator = new SRCalculator();
        var singleNote = new List<Note> { new(0, 0, 1000) };
        var multipleNotes = new List<Note>
        {
            new(0, 0, 1000),
            new(1, 0, 1500),
            new(2, 0, 2000)
        };
        var keyCount = 4;
        var od = 8.0;

        // Act
        var singleResult = calculator.Calculate(singleNote, keyCount, od, out _);
        var multipleResult = calculator.Calculate(multipleNotes, keyCount, od, out _);

        // Assert
        Assert.True(multipleResult >= singleResult);
    }

    [MemoryDiagnoser]
    public class SRCalculatorBenchmarks
    {
        private List<Note> _testNotes;
        private SRCalculator _newCalculator;
        private OldSRCalculator _oldCalculator;

        [GlobalSetup]
        public void Setup()
        {
            // 创建测试数据：一个中等复杂度的谱面
            _testNotes = new List<Note>();
            var random = new Random(42); // 固定种子确保一致性
            var totalNotes = 1000;
            var maxTime = 180000; // 3分钟

            for (var i = 0; i < totalNotes; i++)
            {
                var time = (int)(i * (maxTime / (double)totalNotes));
                var column = random.Next(0, 4); // 4K谱面
                var tail = random.Next(0, 10) < 2 ? time + random.Next(500, 2000) : -1; // 20% LN
                _testNotes.Add(new Note(column, time, tail));
            }

            _newCalculator = new SRCalculator();
            _oldCalculator = new OldSRCalculator();
        }

        [Benchmark]
        public double NewSRCalculator()
        {
            return _newCalculator.Calculate(_testNotes, 4, 8.0, out _);
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
        // 加载真实的谱面文件
        var testFilePath =
            @"E:\BASE CODE\GitHub\krrTools\tests\TestOsuFile\Jumpstream - Happy Hardcore Synthesizer (SK_la) [10k-1].osu";
        var beatmap = BeatmapDecoder.Decode(testFilePath);
        var calculator = new SRCalculator();
        var notes = calculator.getNotes(beatmap);
        var keyCount = (int)beatmap.DifficultySection.CircleSize;
        double od = beatmap.DifficultySection.OverallDifficulty;

        _output.WriteLine("=== SR计算详细性能分析 ===");
        _output.WriteLine($"谱面: {System.IO.Path.GetFileName(testFilePath)}");
        _output.WriteLine($"音符数量: {notes.Count}, 键数: {keyCount}K, OD: {od}");
        _output.WriteLine("运行3次以获取平均时间...");
        _output.WriteLine("");

        // 运行几次获取平均时间和SR值
        var newTimesList = new List<Dictionary<string, long>>();
        var newSRList = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var sr = new SRCalculator().Calculate(notes, keyCount, od, out var times);
            newTimesList.Add(times);
            newSRList.Add(sr);
        }

        var oldTimesList = new List<Dictionary<string, long>>();
        var oldSRList = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var sr = new OldSRCalculator().Calculate(notes, keyCount, od, out var times);
            oldTimesList.Add(times);
            oldSRList.Add(sr);
        }

        // 计算平均时间和SR值
        var avgNewTimes = newTimesList
            .SelectMany(d => d)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value));
        var avgNewSR = newSRList.Average();

        var avgOldTimes = oldTimesList
            .SelectMany(d => d)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value));
        var avgOldSR = oldSRList.Average();

        // 生成 ASCII 表格
        var sections = new[] { "Section232425", "Section2627", "Section3", "Total" };
        var displaySections = new[] { "Section23/24/25", "Section26/27", "Section3", "Total" };
        var colWidths = new[] { 6, 15, 11, 7, 5, 6 }; // 部分, Section23/24/25, Section26/27, Section3, Total, SR

        // 表头
        var header =
            $"| {"部分".PadRight(colWidths[0])} | {displaySections[0].PadRight(colWidths[1])} | {displaySections[1].PadRight(colWidths[2])} | {displaySections[2].PadRight(colWidths[3])} | {displaySections[3].PadRight(colWidths[4])} | {"SR".PadRight(colWidths[5])} |";
        var separator = $"+{string.Join("+", colWidths.Select(w => new string('-', w + 2)))}+";

        _output.WriteLine(separator);
        _output.WriteLine(header);
        _output.WriteLine(separator);

        // 新版本行
        var newTimes = sections.Select(s =>
                avgNewTimes.GetValueOrDefault(s, 0).ToString("F1").PadLeft(colWidths[Array.IndexOf(sections, s) + 1]))
            .ToArray();
        var newSRStr = avgNewSR.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(colWidths[5]);
        _output.WriteLine(
            $"| {"新版本".PadRight(colWidths[0])} | {newTimes[0]} | {newTimes[1]} | {newTimes[2]} | {newTimes[3]} | {newSRStr} |");

        // 旧版本行
        var oldTimes = sections.Select(s =>
                avgOldTimes.GetValueOrDefault(s, 0).ToString("F1").PadLeft(colWidths[Array.IndexOf(sections, s) + 1]))
            .ToArray();
        var oldSRStr = avgOldSR.ToString($"F{SR_DECIMAL_PLACES}").PadLeft(colWidths[5]);
        _output.WriteLine(
            $"| {"旧版本".PadRight(colWidths[0])} | {oldTimes[0]} | {oldTimes[1]} | {oldTimes[2]} | {oldTimes[3]} | {oldSRStr} |");

        // 差异行
        var diffs = sections.Select(s =>
        {
            var diff = avgNewTimes.GetValueOrDefault(s, 0) - avgOldTimes.GetValueOrDefault(s, 0);
            return $"{(diff >= 0 ? "+" : "")}{diff:F1}".PadLeft(colWidths[Array.IndexOf(sections, s) + 1]);
        }).ToArray();
        var srDiff = avgNewSR - avgOldSR;
        var srDiffStr = $"{(srDiff >= 0 ? "+" : "")}{srDiff.ToString($"F{SR_DECIMAL_PLACES}")}".PadLeft(colWidths[5]);
        _output.WriteLine(
            $"| {"差异".PadRight(colWidths[0])} | {diffs[0]} | {diffs[1]} | {diffs[2]} | {diffs[3]} | {srDiffStr} |");

        _output.WriteLine(separator);

        // 同时输出到控制台
        Console.WriteLine(separator);
        Console.WriteLine(header);
        Console.WriteLine(separator);
        Console.WriteLine(
            $"| {"新版本".PadRight(colWidths[0])} | {newTimes[0]} | {newTimes[1]} | {newTimes[2]} | {newTimes[3]} | {newSRStr} |");
        Console.WriteLine(
            $"| {"旧版本".PadRight(colWidths[0])} | {oldTimes[0]} | {oldTimes[1]} | {oldTimes[2]} | {oldTimes[3]} | {oldSRStr} |");
    }
}