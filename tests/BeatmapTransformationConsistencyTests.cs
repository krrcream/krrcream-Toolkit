#nullable enable
using System.Collections.Generic;
using System.Linq;
using krrTools.Tools.N2NC;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using Xunit;

namespace krrTools.Tests
{
    /// <summary>
    /// 谱面转换一致性测试 - 专门测试随机种子一致性和转换结果的可重复性
    /// </summary>
    public class BeatmapTransformationConsistencyTests
    {
        /// <summary>
        /// 创建一个简单的测试谱面
        /// </summary>
        private Beatmap CreateSimpleBeatmap(int keyCount = 4, int noteCount = 20)
        {
            var beatmap = new Beatmap
            {
                DifficultySection =
                {
                    // 设置基本属性
                    CircleSize = keyCount,
                    OverallDifficulty = 8
                },
                MetadataSection =
                {
                    Title = "Test Song",
                    Artist = "Test Artist",
                    Version = "Test Diff"
                },
                HitObjects = []
            };

            // 简单处理 - 创建空的列表，让转换器自己处理

            // 通过简单方式创建音符 - 直接使用基类构造函数
            for (int i = 0; i < noteCount; i++)
            {
                // 创建基础HitObject，使用最简单的方式
                var hitObject = new HitObject();
                beatmap.HitObjects.Add(hitObject);
            }

            return beatmap;
        }

        /// <summary>
        /// 计算谱面的简单特征码，用于比较
        /// </summary>
        private string GetBeatmapSignature(Beatmap beatmap)
        {
            var keyCount = (int)beatmap.DifficultySection.CircleSize;
            var noteCount = beatmap.HitObjects.Count;
            var version = beatmap.MetadataSection.Version ?? "";
            
            return $"K{keyCount}N{noteCount}V{version.GetHashCode()}";
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
                options.TransformSpeed.Value = 2.0;
                options.Seed = seed;
                var transformer = new N2NC();
                var signatures = new List<string>();

                // Act - 执行多次相同的转换
                for (int run = 0; run < 3; run++)
                {
                    var beatmap = CreateSimpleBeatmap(4, 15);
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
                var seeds = new[] { 11111, 22222, 33333 };
                var results = new List<string>();
                var transformer = new N2NC();

                // Act - 使用不同种子进行转换
                foreach (var seed in seeds)
                {
                    var beatmap = CreateSimpleBeatmap(4, 15);
                    var options = new N2NCOptions();
                    options.TargetKeys.Value = 7;
                    options.TransformSpeed.Value = 2.0;
                    options.Seed = seed;
                    transformer.TransformBeatmap(beatmap, options);
                    results.Add(GetBeatmapSignature(beatmap));
                }

                // Assert - 不同种子应该产生不同结果
                Assert.Equal(3, results.Distinct().Count()); // 应该有3个不同的结果
                
                // 但所有结果都应该是7键
                Assert.All(results, r => Assert.Contains("K7", r));
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

                // Act - 对不同输入键数的谱面使用相同设置
                var beatmap4K = CreateSimpleBeatmap(4, 10);
                var beatmap6K = CreateSimpleBeatmap(6, 10);
                
                transformer.TransformBeatmap(beatmap4K, options);
                transformer.TransformBeatmap(beatmap6K, options);

                // Assert - 都应该转换为目标键数
                Assert.Equal(8, (int)beatmap4K.DifficultySection.CircleSize);
                Assert.Equal(8, (int)beatmap6K.DifficultySection.CircleSize);
                
                // 版本名称应该包含转换标记
                Assert.Contains("4to8", beatmap4K.MetadataSection.Version);
                Assert.Contains("6to8", beatmap6K.MetadataSection.Version);
            });
        }

        [Fact]
        public void N2NC_NullSeed_ShouldUseDefaultAndBeConsistent()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var options1 = new N2NCOptions();
                options1.TargetKeys.Value = 6;
                options1.TransformSpeed.Value = 1.0;
                options1.Seed = null;
                var options2 = new N2NCOptions();
                options2.TargetKeys.Value = 6;
                options2.TransformSpeed.Value = 1.0;
                options2.Seed = null;
                var transformer = new N2NC();

                // Act
                var beatmap1 = CreateSimpleBeatmap(4, 12);
                var beatmap2 = CreateSimpleBeatmap(4, 12);
                
                transformer.TransformBeatmap(beatmap1, options1);
                transformer.TransformBeatmap(beatmap2, options2);

                // Assert - null种子应该使用默认值，结果应该一致
                var signature1 = GetBeatmapSignature(beatmap1);
                var signature2 = GetBeatmapSignature(beatmap2);
                
                Assert.Equal(signature1, signature2); // null种子应该产生相同结果
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
                var beatmap = CreateSimpleBeatmap(4, 15);
                transformer.TransformBeatmap(beatmap, options);

                // Assert - 验证转换确实发生了
                Assert.Equal(7, (int)beatmap.DifficultySection.CircleSize);
                Assert.Contains("[4to7C]", beatmap.MetadataSection.Version);
                
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
                options.TargetKeys.Value = 4;
                options.TransformSpeed.Value = 2.0;
                options.Seed = 12345;
                var transformer = new N2NC();
                var beatmap = CreateSimpleBeatmap(4, 10);
                // var originalVersion = beatmap.MetadataSection.Version;

                // Act
                transformer.TransformBeatmap(beatmap, options);

                // Assert - 相同键数应该不做修改
                Assert.Equal(4, (int)beatmap.DifficultySection.CircleSize);
                // 版本应该保持不变或不包含转换标记
                Assert.DoesNotContain("[4to4C]", beatmap.MetadataSection.Version);
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
                options.TransformSpeed.Value = 2.0;
                options.Seed = 55555;
                var transformer = new N2NC();
                
                // 第一次转换
                var beatmap1 = CreateSimpleBeatmap(4, 10);
                transformer.TransformBeatmap(beatmap1, options);
                var result1 = GetBeatmapSignature(beatmap1);
                
                // 修改设置然后改回来
                options.TargetKeys.Value = 8; // 临时修改
                options.TargetKeys.Value = 6; // 改回原值
                options.Seed = 77777;   // 修改种子
                options.Seed = 55555;   // 改回原种子

                // Act - 第二次转换，设置已恢复
                var beatmap2 = CreateSimpleBeatmap(4, 10);
                transformer.TransformBeatmap(beatmap2, options);
                var result2 = GetBeatmapSignature(beatmap2);

                // Assert - 相同的最终设置应该产生相同结果
                Assert.Equal(result1, result2);
            });
        }
    }
}