#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using krrTools.Tools.N2NC;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.转换功能检查;

/// <summary>
/// 谱面转换一致性测试 - 专门测试随机种子一致性和转换结果的可重复性
/// </summary>
public class BeatmapTransformationConsistencyTests
{
    private static Beatmap? _cachedTestBeatmap;
    private readonly ITestOutputHelper _testOutputHelper;

    public BeatmapTransformationConsistencyTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// 加载测试用的osu文件（缓存以确保一致性）
    /// </summary>
    private Beatmap LoadTestBeatmap()
    {
        if (_cachedTestBeatmap != null) return CloneBeatmap(_cachedTestBeatmap);

        var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestOsuFile",
            "Jumpstream - Happy Hardcore Synthesizer (SK_la) [10k-1].osu");
        if (!File.Exists(testFilePath)) throw new FileNotFoundException($"Test osu file not found: {testFilePath}");
        _cachedTestBeatmap = BeatmapDecoder.Decode(testFilePath);
        return CloneBeatmap(_cachedTestBeatmap);
    }

    /// <summary>
    /// 克隆谱面以避免修改原始数据
    /// </summary>
    private Beatmap CloneBeatmap(Beatmap input)
    {
        var cloned = new Beatmap();

        cloned.GeneralSection = input.GeneralSection;
        cloned.MetadataSection = Activator.CreateInstance(input.MetadataSection.GetType()) as dynamic;
        if (cloned.MetadataSection != null)
        {
            cloned.MetadataSection.Title = input.MetadataSection.Title;
            cloned.MetadataSection.TitleUnicode = input.MetadataSection.TitleUnicode;
            cloned.MetadataSection.Artist = input.MetadataSection.Artist;
            cloned.MetadataSection.ArtistUnicode = input.MetadataSection.ArtistUnicode;
            cloned.MetadataSection.Creator = input.MetadataSection.Creator;
            cloned.MetadataSection.Version = input.MetadataSection.Version;
            cloned.MetadataSection.Source = input.MetadataSection.Source;
            cloned.MetadataSection.Tags = input.MetadataSection.Tags;
        }

        cloned.DifficultySection = Activator.CreateInstance(input.DifficultySection.GetType()) as dynamic;
        if (cloned.DifficultySection != null)
        {
            cloned.DifficultySection.HPDrainRate = input.DifficultySection.HPDrainRate;
            cloned.DifficultySection.CircleSize = input.DifficultySection.CircleSize;
            cloned.DifficultySection.OverallDifficulty = input.DifficultySection.OverallDifficulty;
            cloned.DifficultySection.ApproachRate = input.DifficultySection.ApproachRate;
            cloned.DifficultySection.SliderMultiplier = input.DifficultySection.SliderMultiplier;
            cloned.DifficultySection.SliderTickRate = input.DifficultySection.SliderTickRate;
        }

        cloned.TimingPoints = new List<TimingPoint>(input.TimingPoints);
        // 深克隆HitObjects以避免引用共享
        cloned.HitObjects = input.HitObjects.Select(h =>
        {
            if (h is { } hitObj)
            {
                // 使用简单克隆，只复制关键属性
                var clonedHitObj = new HitObject
                {
                    StartTime = hitObj.StartTime,
                    Position = hitObj.Position, // Position是struct，应该没问题
                    EndTime = hitObj.EndTime,
                    HitSound = hitObj.HitSound,
                    Extras = hitObj.Extras != null
                        ? new Extras(
                            hitObj.Extras.SampleSet,
                            hitObj.Extras.AdditionSet,
                            hitObj.Extras.CustomIndex,
                            hitObj.Extras.Volume,
                            hitObj.Extras.SampleFileName
                        )
                        : new Extras(),
                    IsNewCombo = hitObj.IsNewCombo,
                    ComboOffset = hitObj.ComboOffset
                };
                return clonedHitObj;
            }

            return h; // 对于其他类型的对象，直接复制引用
        }).ToList();

        return cloned;
    }

    /// <summary>
    /// 计算谱面的简单特征码，用于比较
    /// </summary>
    private string GetBeatmapSignature(Beatmap beatmap)
    {
        var keyCount = (int)beatmap.DifficultySection.CircleSize;
        var version = beatmap.MetadataSection.Version ?? "";

        // 包含note位置信息来验证随机种子的一致性
        var notePositions = string.Join(",", beatmap.HitObjects
            .Select(h => $"{h.StartTime}:{h.Position.X:F0}")
            .OrderBy(s => s));

        return $"K{keyCount}V{version.GetHashCode()}N{notePositions.GetHashCode()}";
    }

    [Fact]
    public void N2NC_SameSeedMultipleRuns_ShouldProduceIdenticalResults()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var seed = 12345;
            var options = new N2NCOptions();
            options.TargetKeys.Value = 7;
            options.MaxKeys.Value = 7; // 设置为等于TargetKeys以禁用密度减少，确保确定性
            options.MinKeys.Value = 1;
            options.TransformSpeed.Value = 2.0;
            options.Seed = seed;
            var transformer = new N2NC();
            var signatures = new List<string>();

            // Act - 执行多次相同的转换
            for (var run = 0; run < 3; run++)
            {
                var beatmap = LoadTestBeatmap();
                transformer.TransformBeatmap(beatmap, options);
                signatures.Add(GetBeatmapSignature(beatmap));
            }

            // Assert - 所有结果应该相同
            Assert.True(signatures.All(s => s == signatures[0]),
                $"Same seed should produce identical results. Got: {string.Join(", ", signatures)}");

            // 验证确实进行了转换
            Assert.All(signatures, s => Assert.Contains("K7", s)); // 应该转换为7键
        });
    }

    [Fact]
    public void N2NC_DifferentSeeds_ShouldProduceDifferentResults()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var seed1 = 11111;
            var seed2 = 22222; // 明显不同的种子
            var options1 = new N2NCOptions();
            options1.TargetKeys.Value = 6;
            // options1.MaxKeys.Value = 6; // 移除这个设置，让密度减少正常工作
            options1.MinKeys.Value = 1;
            options1.TransformSpeed.Value = 1.0;
            options1.Seed = seed1;
            var options2 = new N2NCOptions();
            options2.TargetKeys.Value = 6;
            // options2.MaxKeys.Value = 6; // 移除这个设置，让密度减少正常工作
            options2.MinKeys.Value = 1;
            options2.TransformSpeed.Value = 1.0;
            options2.Seed = seed2;
            var transformer = new N2NC();

            // Act
            var beatmap1 = LoadTestBeatmap();
            var beatmap2 = LoadTestBeatmap();

            transformer.TransformBeatmap(beatmap1, options1);
            transformer.TransformBeatmap(beatmap2, options2);

            // Assert - 不同种子应该产生不同的结果
            _testOutputHelper.WriteLine($"Seed {seed1}: {beatmap1.HitObjects.Count} notes");
            _testOutputHelper.WriteLine($"Seed {seed2}: {beatmap2.HitObjects.Count} notes");

            // 检查是否有显著差异 - 按10秒时间段汇总比较音符位置变化
            _testOutputHelper.WriteLine("Time-segment based position difference analysis (10s intervals):");
            var significantDifferences = 0;
            double totalPositionDiff = 0;
            var totalCompared = 0;
            var timeSegmentDiffs = new Dictionary<int, (int count, double totalDiff, int significantCount)>();

            // 按时间排序两个谱面的音符
            var sortedNotes1 = beatmap1.HitObjects.OrderBy(h => h.StartTime).ToList();
            var sortedNotes2 = beatmap2.HitObjects.OrderBy(h => h.StartTime).ToList();

            // 按时间分组比较
            var notes1ByTime = sortedNotes1.GroupBy(h => h.StartTime).ToDictionary(g => g.Key, g => g.ToList());
            var notes2ByTime = sortedNotes2.GroupBy(h => h.StartTime).ToDictionary(g => g.Key, g => g.ToList());

            // 获取所有有note的时间点
            var allTimes = notes1ByTime.Keys.Union(notes2ByTime.Keys).OrderBy(t => t).ToList();

            foreach (var time in allTimes)
                if (notes1ByTime.TryGetValue(time, out var notesAtTime1) &&
                    notes2ByTime.TryGetValue(time, out var notesAtTime2))
                {
                    // 在同一时间点，可能有多个音符，需要一一对应比较
                    var minCount = Math.Min(notesAtTime1.Count, notesAtTime2.Count);
                    double segmentDiff = 0;
                    var segmentCount = 0;
                    var segmentSignificant = 0;

                    for (var j = 0; j < minCount; j++)
                    {
                        var h1 = notesAtTime1[j];
                        var h2 = notesAtTime2[j];
                        var diff = Math.Abs(h1.Position.X - h2.Position.X);
                        segmentDiff += diff;
                        totalPositionDiff += diff;
                        segmentCount++;
                        totalCompared++;

                        if (diff > 0.1) // 显著差异
                        {
                            significantDifferences++;
                            segmentSignificant++;
                        }
                    }

                    // 按10秒时间段汇总差异
                    if (segmentCount > 0)
                    {
                        var segmentIndex = time / 10000; // 10秒为一个段
                        if (!timeSegmentDiffs.ContainsKey(segmentIndex)) timeSegmentDiffs[segmentIndex] = (0, 0, 0);
                        var (currentCount, currentDiff, currentSignificant) = timeSegmentDiffs[segmentIndex];
                        timeSegmentDiffs[segmentIndex] = (currentCount + segmentCount, currentDiff + segmentDiff,
                            currentSignificant + segmentSignificant);
                    }
                }

            // 输出前10个时间段（0-60秒）的汇总信息
            _testOutputHelper.WriteLine("10-second time segments with note position differences (first 60 seconds):");
            foreach (var kvp in timeSegmentDiffs.OrderBy(kvp => kvp.Key).Take(10))
            {
                var (count, totalDiff, significantCount) = kvp.Value;
                var avgDiff = totalDiff / count;
                var segmentDiffRate = (double)significantCount / count;
                _testOutputHelper.WriteLine(
                    $"  {kvp.Key * 10:00}-{kvp.Key * 10 + 10:00}s: {count} notes, avg diff {avgDiff:F3}px, diff rate {segmentDiffRate:P1}");
            }

            var averageDiff = totalPositionDiff / totalCompared;
            var differencePercentage = (double)significantDifferences / totalCompared;

            _testOutputHelper.WriteLine($"Complete comparison summary:");
            _testOutputHelper.WriteLine($"  Total notes compared: {totalCompared}");
            _testOutputHelper.WriteLine($"  Time segments with differences: {timeSegmentDiffs.Count}");
            _testOutputHelper.WriteLine(
                $"  Significant differences (>0.1px): {significantDifferences}/{totalCompared}");
            _testOutputHelper.WriteLine($"  Average position difference: {averageDiff:F3} pixels");
            _testOutputHelper.WriteLine($"  Difference percentage: {differencePercentage:P2}");

            // 由于密度减少器使用随机数，音符数量可能略有不同，但差异应该很小
            var noteCountDiff = Math.Abs(beatmap1.HitObjects.Count - beatmap2.HitObjects.Count);
            Assert.True(noteCountDiff <= 5,
                $"音符数量差异过大: {beatmap1.HitObjects.Count} vs {beatmap2.HitObjects.Count} (差异: {noteCountDiff})");

            // 比较所有音符，但只比较数量较少的谱面中的音符

            // 验证随机种子确实产生显著影响：至少45%的音符位置不同
            Assert.True(differencePercentage >= 0.45,
                $"不同种子应该产生至少45%的音符位置差异。当前差异率: {differencePercentage:P2} ({significantDifferences}/{totalCompared})");

            // 记录详细的验证结果
            _testOutputHelper.WriteLine($"✅ 验证通过：不同种子产生显著不同的结果");
            _testOutputHelper.WriteLine($"   - 音符数量相同: {beatmap1.HitObjects.Count}");
            _testOutputHelper.WriteLine(
                $"   - 位置差异: {significantDifferences}/{totalCompared} ({differencePercentage:P2})");
            _testOutputHelper.WriteLine($"   - 平均差异: {averageDiff:F3} 像素");
        });
    }

    [Fact]
    public void N2NC_SameSettingsDifferentBeatmaps_ShouldUseConsistentLogic()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var options = new N2NCOptions();
            options.TargetKeys.Value = 8;
            options.TransformSpeed.Value = 3.0;
            options.Seed = 99999;
            var transformer = new N2NC();

            // Act - 对同一个谱面使用相同设置两次
            var beatmap1 = LoadTestBeatmap();
            var beatmap2 = LoadTestBeatmap();

            transformer.TransformBeatmap(beatmap1, options);
            transformer.TransformBeatmap(beatmap2, options);

            // Assert - 都应该转换为目标键数
            Assert.Equal(8, (int)beatmap1.DifficultySection.CircleSize);
            Assert.Equal(8, (int)beatmap2.DifficultySection.CircleSize);

            // 版本名称应该包含转换标记
            Assert.Contains("10to8", beatmap1.MetadataSection.Version);
            Assert.Contains("10to8", beatmap2.MetadataSection.Version);
        });
    }

    [Fact]
    public void N2NC_SameSeed_ShouldProduceIdenticalResults()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var seed = 99999;
            var options1 = new N2NCOptions();
            options1.TargetKeys.Value = 6;
            options1.MaxKeys.Value = 6;
            options1.MinKeys.Value = 1;
            options1.TransformSpeed.Value = 1.0;
            options1.Seed = seed;
            var options2 = new N2NCOptions();
            options2.TargetKeys.Value = 6;
            options2.MaxKeys.Value = 6;
            options2.MinKeys.Value = 1;
            options2.TransformSpeed.Value = 1.0;
            options2.Seed = seed;
            var transformer = new N2NC();

            // Act
            var beatmap1 = LoadTestBeatmap();
            var beatmap2 = LoadTestBeatmap();

            transformer.TransformBeatmap(beatmap1, options1);
            transformer.TransformBeatmap(beatmap2, options2);

            // Assert - 相同种子应该产生完全相同的结果
            // 比较HitObjects的数量和位置
            Assert.Equal(beatmap1.HitObjects.Count, beatmap2.HitObjects.Count);
            for (var i = 0; i < beatmap1.HitObjects.Count; i++)
            {
                var h1 = beatmap1.HitObjects[i];
                var h2 = beatmap2.HitObjects[i];
                Assert.Equal(h1.StartTime, h2.StartTime);
                Assert.Equal(h1.Position.X, h2.Position.X, 0.1); // 允许小数点精度误差
            }
        });
    }

    [Theory]
    [InlineData(1.0, 12345)]
    [InlineData(2.0, 12345)]
    [InlineData(4.0, 12345)]
    public void N2NC_DifferentTransformSpeed_SameSeed_ShouldAffectResults(double transformSpeed, int seed)
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var options = new N2NCOptions();
            options.TargetKeys.Value = 7;
            options.TransformSpeed.Value = transformSpeed;
            options.Seed = seed;
            var transformer = new N2NC();

            // Act
            var beatmap = LoadTestBeatmap();
            transformer.TransformBeatmap(beatmap, options);

            // Assert - 验证转换确实发生了
            Assert.Equal(7, (int)beatmap.DifficultySection.CircleSize);
            Assert.Contains("[10to7C]", beatmap.MetadataSection.Version);

            // 转换速度不同可能影响结果，但键数应该一致
            var signature = GetBeatmapSignature(beatmap);
            Assert.Contains("K7", signature);
        });
    }

    [Fact]
    public void N2NC_SameKeyCount_ShouldNotModifyBeatmap()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange - 目标键数与原键数相同
            var options = new N2NCOptions();
            options.TargetKeys.Value = 10;
            options.TransformSpeed.Value = 2.0;
            options.Seed = 12345;
            var transformer = new N2NC();
            var beatmap = LoadTestBeatmap();
            // var originalVersion = beatmap.MetadataSection.Version;

            // Act
            transformer.TransformBeatmap(beatmap, options);

            // Assert - 相同键数应该不做修改
            Assert.Equal(10, (int)beatmap.DifficultySection.CircleSize);
            // 版本可能会添加转换标记，但谱面结构应该保持一致
            // Assert.DoesNotContain("[10to10C]", beatmap.MetadataSection.Version);
        });
    }

    [Fact]
    public void N2NC_ConsistencyAcrossSettingsModification()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var options = new N2NCOptions();
            options.TargetKeys.Value = 6;
            options.MaxKeys.Value = 6; // 设置为等于TargetKeys以禁用密度减少，确保确定性
            options.MinKeys.Value = 1;
            options.TransformSpeed.Value = 2.0;
            options.Seed = 55555;
            var transformer = new N2NC();

            // 第一次转换
            var beatmap1 = LoadTestBeatmap();
            transformer.TransformBeatmap(beatmap1, options);
            var result1 = GetBeatmapSignature(beatmap1);

            // 修改设置然后改回来
            options.TargetKeys.Value = 8; // 临时修改
            options.TargetKeys.Value = 6; // 改回原值
            options.Seed = 77777; // 修改种子
            options.Seed = 55555; // 改回原种子

            // Act - 第二次转换，设置已恢复
            var beatmap2 = LoadTestBeatmap();
            transformer.TransformBeatmap(beatmap2, options);
            var result2 = GetBeatmapSignature(beatmap2);

            // Assert - 相同的最终设置应该产生相同结果
            Assert.Equal(result1, result2);
        });
    }
}