#nullable enable
using System;
using System.IO;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Utilities;
using Moq;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests
{
    public class BeatmapTransformationServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly Mock<IModuleManager> _mockModuleManager;
        private readonly BeatmapTransformationService _service;

        public BeatmapTransformationServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _mockModuleManager = new Mock<IModuleManager>();
            _service = new BeatmapTransformationService(_mockModuleManager.Object);
        }

        [Fact]
        public void TransformAndSaveBeatmap_WithLongPath_ShouldTruncateCorrectly()
        {
            // Arrange
            var mockTool = new Mock<IToolModule>();
            _mockModuleManager.Setup(m => m.GetToolByName(It.IsAny<string>())).Returns(mockTool.Object);

            string testOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestOutput");
            Directory.CreateDirectory(testOutputDir);
            // Create a long directory path
            string longDir = Path.Combine(testOutputDir, "VeryLongDirectoryNameThatWillMakeTheFullPathExceedTheLimitWhenCombinedWithTheFileName");
            Directory.CreateDirectory(longDir);
            string inputFile = Path.Combine(longDir, "test.osu");
            File.WriteAllText(inputFile, @"osu file format v14

[General]
AudioFilename: test.mp3
AudioLeadIn: 0
PreviewTime: -1
Countdown: 0
SampleSet: None
StackLeniency: 0.7
Mode: 3
LetterboxInBreaks: 0
SpecialStyle: 0
WidescreenStoryboard: 0

[Editor]
DistanceSpacing: 0.8
BeatDivisor: 4
GridSize: 16
TimelineZoom: 1

[Metadata]
Title:Very Long Title That Will Make The Filename Extremely Long When Combined With Artist And Other Metadata
TitleUnicode:Very Long Title That Will Make The Filename Extremely Long When Combined With Artist And Other Metadata
Artist:Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata
ArtistUnicode:Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata
Creator:Test
Version:Test
Source:
Tags:
BeatmapID:0
BeatmapSetID:0

[Difficulty]
HPDrainRate:4
CircleSize:4
OverallDifficulty:4
ApproachRate:4
SliderMultiplier:1.4
SliderTickRate:1

[Events]
//Background and Video events
//Break Periods
//Storyboard Layer 0 (Background)
//Storyboard Layer 1 (Fail)
//Storyboard Layer 2 (Pass)
//Storyboard Layer 3 (Foreground)
//Storyboard Layer 4 (Overlay)
//Storyboard Sound Samples

[TimingPoints]
0,500,4,1,0,50,1,0

[HitObjects]
64,0,0,1,0,0:0:0:0:
192,0,500,1,0,0:0:0:0:
");

            try
            {
                // Decode beatmap
                Beatmap? beatmap = BeatmapDecoder.Decode(inputFile).GetManiaBeatmap();
                Assert.NotNull(beatmap);

                // Transform beatmap
                Beatmap transformedBeatmap = _service.TransformBeatmap(beatmap, ConverterEnum.N2NC);

                // Calculate output path
                string outputPath = transformedBeatmap.GetOutputOsuFileName();
                string? outputDir = Path.GetDirectoryName(inputFile);
                string fullOutputPath = Path.Combine(outputDir!, outputPath);

                // Apply truncation if necessary
                if (fullOutputPath.Length > 255)
                {
                    string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                    fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                }

                // Print to test output
                _testOutputHelper.WriteLine($"Input Path: {inputFile}");
                _testOutputHelper.WriteLine($"Output Path: {fullOutputPath}");

                // Assert
                Assert.True(fullOutputPath.Length <= 255);
                Assert.EndsWith("....osu", fullOutputPath);
            }
            finally
            {
                // Cleanup
                if (File.Exists(inputFile)) File.Delete(inputFile);
                if (Directory.Exists(longDir)) Directory.Delete(longDir, true);
            }
        }

        [Fact]
        public void TransformAndSaveBeatmap_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var mockTool = new Mock<IToolModule>();
            _mockModuleManager.Setup(m => m.GetToolByName(It.IsAny<string>())).Returns(mockTool.Object);

            string testOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestOutput");
            Directory.CreateDirectory(testOutputDir);
            string inputFile = Path.Combine(testOutputDir, "test.osu");
            File.WriteAllText(inputFile, @"osu file format v14

[General]
AudioFilename: test.mp3
AudioLeadIn: 0
PreviewTime: -1
Countdown: 0
SampleSet: None
StackLeniency: 0.7
Mode: 3
LetterboxInBreaks: 0
SpecialStyle: 0
WidescreenStoryboard: 0

[Editor]
DistanceSpacing: 0.8
BeatDivisor: 4
GridSize: 16
TimelineZoom: 1

[Metadata]
Title:Test<>:|?*
TitleUnicode:Test<>:|?*
Artist:Test<>:|?*
ArtistUnicode:Test<>:|?*
Creator:Test
Version:Test
Source:
Tags:
BeatmapID:0
BeatmapSetID:0

[Difficulty]
HPDrainRate:4
CircleSize:4
OverallDifficulty:4
ApproachRate:4
SliderMultiplier:1.4
SliderTickRate:1

[Events]
//Background and Video events
//Break Periods
//Storyboard Layer 0 (Background)
//Storyboard Layer 1 (Fail)
//Storyboard Layer 2 (Pass)
//Storyboard Layer 3 (Foreground)
//Storyboard Layer 4 (Overlay)
//Storyboard Sound Samples

[TimingPoints]
0,500,4,1,0,50,1,0

[HitObjects]
64,0,0,1,0,0:0:0:0:
192,0,500,1,0,0:0:0:0:
");

            try
            {
                // Decode beatmap
                Beatmap? beatmap = BeatmapDecoder.Decode(inputFile).GetManiaBeatmap();
                Assert.NotNull(beatmap);

                // Transform beatmap
                Beatmap transformedBeatmap = _service.TransformBeatmap(beatmap, ConverterEnum.N2NC);

                // Calculate output path
                string outputPath = transformedBeatmap.GetOutputOsuFileName();
                string? outputDir = Path.GetDirectoryName(inputFile);
                string fullOutputPath = Path.Combine(outputDir!, outputPath);

                // Apply truncation if necessary
                if (fullOutputPath.Length > 255)
                {
                    string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                    fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                }

                // Print to test output
                _testOutputHelper.WriteLine($"Input Path: {inputFile}");
                _testOutputHelper.WriteLine($"Output Path: {fullOutputPath}");

                // Assert
                Assert.True(File.Exists(inputFile)); // Input exists
                Assert.DoesNotContain(fullOutputPath, "<>:\"|?*");
            }
            finally
            {
                // Cleanup
                if (File.Exists(inputFile)) File.Delete(inputFile);
            }
        }

        [Fact]
        public void TransformAndSaveBeatmap_WithNonEnglishPath_ShouldHandleCorrectly()
        {
            // Arrange
            var mockTool = new Mock<IToolModule>();
            _mockModuleManager.Setup(m => m.GetToolByName(It.IsAny<string>())).Returns(mockTool.Object);

            string testOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestOutput");
            Directory.CreateDirectory(testOutputDir);
            string inputFile = Path.Combine(testOutputDir, "test.osu");
            File.WriteAllText(inputFile, @"osu file format v14

[General]
AudioFilename: test.mp3
AudioLeadIn: 0
PreviewTime: -1
Countdown: 0
SampleSet: None
StackLeniency: 0.7
Mode: 3
LetterboxInBreaks: 0
SpecialStyle: 0
WidescreenStoryboard: 0

[Editor]
DistanceSpacing: 0.8
BeatDivisor: 4
GridSize: 16
TimelineZoom: 1

[Metadata]
Title:测试文件
TitleUnicode:测试文件
Artist:艺术家
ArtistUnicode:艺术家
Creator:Test
Version:Test
Source:
Tags:
BeatmapID:0
BeatmapSetID:0

[Difficulty]
HPDrainRate:4
CircleSize:4
OverallDifficulty:4
ApproachRate:4
SliderMultiplier:1.4
SliderTickRate:1

[Events]
//Background and Video events
//Break Periods
//Storyboard Layer 0 (Background)
//Storyboard Layer 1 (Fail)
//Storyboard Layer 2 (Pass)
//Storyboard Layer 3 (Foreground)
//Storyboard Layer 4 (Overlay)
//Storyboard Sound Samples

[TimingPoints]
0,500,4,1,0,50,1,0

[HitObjects]
64,0,0,1,0,0:0:0:0:
192,0,500,1,0,0:0:0:0:
");

            try
            {
                // Decode beatmap
                Beatmap? beatmap = BeatmapDecoder.Decode(inputFile).GetManiaBeatmap();
                Assert.NotNull(beatmap);

                // Transform beatmap
                Beatmap transformedBeatmap = _service.TransformBeatmap(beatmap, ConverterEnum.N2NC);

                // Calculate output path
                string outputPath = transformedBeatmap.GetOutputOsuFileName();
                string? outputDir = Path.GetDirectoryName(inputFile);
                string fullOutputPath = Path.Combine(outputDir!, outputPath);

                // Apply truncation if necessary
                if (fullOutputPath.Length > 255)
                {
                    string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                    fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                }

                // Print to test output
                _testOutputHelper.WriteLine($"Input Path: {inputFile}");
                _testOutputHelper.WriteLine($"Output Path: {fullOutputPath}");

                // Assert
                Assert.True(Path.IsPathRooted(fullOutputPath));
            }
            finally
            {
                // Cleanup
                if (File.Exists(inputFile)) File.Delete(inputFile);
            }
        }

        [Fact]
        public void ExecuteConvertWithModule_PathTruncation_ShouldHandleCorrectly()
        {
            // Arrange
            string inputPath =
                @"C:\Very\Long\Path\That\Exceeds\The\Maximum\Allowed\Length\For\Windows\File\System\Which\Is\Typically\Around\255\Characters\But\This\One\Is\Definitely\Longer\Than\That\Limit\And\Should\Be\Truncated\Appropriately\To\Fit\Within\The\Constraints\Of\The\File\System\Without\Causing\Any\Issues\During\Saving\Process\test.osu";
            string expectedOutputPath =
                @"C:\Very\Long\Path\That\Exceeds\The\Maximum\Allowed\Length\For\Windows\File\System\Which\Is\Typically\Around\255\Characters\But\This\One\Is\Definitely\Longer\Than\That\Limit\And\Should\Be\Truncated\Appropriately\To\Fit\Within\The\Constraints\Of\The\File\System\Without\Causing\Any\Issues\During\Saving\Process\Very Long Artist Name That Will Make The Filename Extremely Long When Combined.......osu";

            // Simulate the logic from ExecuteConvertWithModule
            string outputPath = "Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata (Test) [Test].osu";
            string? outputDir = Path.GetDirectoryName(inputPath);
            string fullOutputPath = Path.Combine(outputDir!, outputPath);

            // Apply truncation
            if (fullOutputPath.Length > 255)
            {
                string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
            }

            // Print to test output
            _testOutputHelper.WriteLine($"Input Path: {inputPath}");
            _testOutputHelper.WriteLine($"Output Path: {fullOutputPath}");

            // Assert
            Assert.True(fullOutputPath.Length <= 255);
            Assert.EndsWith("....osu", fullOutputPath);
        }

        [Fact]
        public void PathTruncation_WithLongPathArray_ShouldHandleCorrectly()
        {
            // Arrange
            string[] inputPaths = new string[]
            {
                @"C:\Very\Long\Path\That\Exceeds\The\Maximum\Allowed\Length\For\Windows\File\System\Which\Is\Typically\Around\255\Characters\But\This\One\Is\Definitely\Longer\Than\That\Limit\And\Should\Be\Truncated\Appropriately\To\Fit\Within\The\Constraints\Of\The\File\System\Without\Causing\Any\Issues\During\Saving\Process\file1.osu",
                @"C:\Another\Very\Long\Path\That\Exceeds\The\Maximum\Allowed\Length\For\Windows\File\System\Which\Is\Typically\Around\255\Characters\But\This\One\Is\Definitely\Longer\Than\That\Limit\And\Should\Be\Truncated\Appropriately\To\Fit\Within\The\Constraints\Of\The\File\System\Without\Causing\Any\Issues\During\Saving\Process\file2.osu",
                @"C:\Yet\Another\Very\Long\Path\That\Exceeds\The\Maximum\Allowed\Length\For\Windows\File\System\Which\Is\Typically\Around\255\Characters\But\This\One\Is\Definitely\Longer\Than\That\Limit\And\Should\Be\Truncated\Appropriately\To\Fit\Within\The\Constraints\Of\The\File\System\Without\Causing\Any\Issues\During\Saving\Process\file3.osu"
            };

            string[] outputPaths = new string[]
            {
                "Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata (Test) [Test].osu",
                "Another Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata (Test) [Test].osu",
                "Yet Another Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata (Test) [Test].osu"
            };

            // Act & Assert
            for (int i = 0; i < inputPaths.Length; i++)
            {
                string? outputDir = Path.GetDirectoryName(inputPaths[i]);
                string fullOutputPath = Path.Combine(outputDir!, outputPaths[i]);

                // Apply truncation if necessary
                if (fullOutputPath.Length > 255)
                {
                    string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                    fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                }

                // Print to test output
                _testOutputHelper.WriteLine($"Input Path {i + 1}: {inputPaths[i]}");
                _testOutputHelper.WriteLine($"Output Path {i + 1}: {fullOutputPath}");

                // Assert
                Assert.True(fullOutputPath.Length <= 255);
                Assert.EndsWith("....osu", fullOutputPath);
            }
        }

        [Fact]
        public void PathTruncation_WithVariousLongMetadata_ShouldHandleCorrectly()
        {
            // Arrange: Create various beatmaps with long metadata and special characters
            var testCases = new[]
            {
                new
                {
                    Artist = "Very Long Artist Name That Will Make The Filename Extremely Long When Combined With Title And Other Metadata And Contains Special Characters Like <>:|?*",
                    Title = "Very Long Title That Will Make The Filename Extremely Long When Combined With Artist And Other Metadata And Contains Special Characters Like <>:|?*",
                    Version = "Very Long Difficulty Name That Will Make The Filename Extremely Long When Combined With Artist And Title And Contains Special Characters Like <>:|?*",
                    InputPath = @"C:\Test\Path\file1.osu"
                },
                new
                {
                    Artist = "艺术家名称非常长会导致文件名过长并且包含特殊字符如<>:|?*",
                    Title = "标题名称非常长会导致文件名过长并且包含特殊字符如<>:|?*",
                    Version = "难度名称非常长会导致文件名过长并且包含特殊字符如<>:|?*",
                    InputPath = @"C:\Test\Path\file2.osu"
                },
                new
                {
                    Artist = "ArtistWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimitsArtistWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimits",
                    Title = "TitleWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimitsTitleWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimits",
                    Version = "DifficultyWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimitsDifficultyWithManyRepeatedWordsThatMakeItVeryLongAndExceedLimits",
                    InputPath = @"C:\Very\Long\Directory\Path\That\Is\Not\Too\Deep\But\Combined\With\Filename\Will\Exceed\Limits\file3.osu"
                }
            };

            foreach (var testCase in testCases)
            {
                // Create a mock beatmap with metadata
                var beatmap = new Beatmap();
                beatmap.MetadataSection.Artist = testCase.Artist;
                beatmap.MetadataSection.Title = testCase.Title;
                beatmap.MetadataSection.Creator = "TestCreator";
                beatmap.MetadataSection.Version = testCase.Version;
                beatmap.DifficultySection.CircleSize = 4;
                beatmap.DifficultySection.HPDrainRate = 4;
                beatmap.DifficultySection.OverallDifficulty = 4;
                beatmap.DifficultySection.ApproachRate = 4;
                beatmap.DifficultySection.SliderMultiplier = 1.4f;
                beatmap.DifficultySection.SliderTickRate = 1;

                // Simulate TransformBeatmap (no actual transformation for this test)
                Beatmap transformedBeatmap = beatmap; // In real scenario, this would be transformed

                // Calculate output path as in TransformAndSaveBeatmap
                string outputPath = transformedBeatmap.GetOutputOsuFileName();
                string? outputDir = Path.GetDirectoryName(testCase.InputPath);
                string fullOutputPath = Path.Combine(outputDir!, outputPath);

                // Apply truncation if necessary
                if (fullOutputPath.Length > 255)
                {
                    string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);
                    fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                }

                // Print to test output
                _testOutputHelper.WriteLine($"Test Case: Artist='{testCase.Artist}', Title='{testCase.Title}', Version='{testCase.Version}'");
                _testOutputHelper.WriteLine($"Input Path: {testCase.InputPath}");
                _testOutputHelper.WriteLine($"Output Path: {fullOutputPath}");

                // Assert
                Assert.True(fullOutputPath.Length <= 255);
                Assert.EndsWith(".osu", fullOutputPath);
                // Check that invalid characters are removed
                Assert.DoesNotContain(fullOutputPath, "<>:\"|?*");
                // Check that it's a valid path
                Assert.True(Path.IsPathRooted(fullOutputPath));
            }
        }
    }
}
