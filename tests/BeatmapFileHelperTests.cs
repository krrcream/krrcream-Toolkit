using Xunit;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using krrTools.Beatmaps;

namespace krrTools.Tests;

public class BeatmapFileHelperTests
{
    [Fact]
    public void IsValidOsuFile_ValidOsuFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".osu");
        var osuFile = tempFile + ".osu";

        try
        {
            // Act
            var result = BeatmapFileHelper.IsValidOsuFile(osuFile);

            // Assert
            Assert.True(result);
        }
        finally
        {
            File.Delete(osuFile);
        }
    }

    [Fact]
    public void IsValidOsuFile_InvalidFile_ReturnsFalse()
    {
        // Arrange
        var invalidFile = "nonexistent.txt";

        // Act
        var result = BeatmapFileHelper.IsValidOsuFile(invalidFile);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetOsuFilesCount_SingleOsuFile_Returns1()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".osu");
        var osuFile = tempFile + ".osu";

        try
        {
            var paths = new List<string> { osuFile };

            // Act
            var count = BeatmapFileHelper.GetOsuFilesCount(paths);

            // Assert
            Assert.Equal(1, count);
        }
        finally
        {
            File.Delete(osuFile);
        }
    }

    [Fact]
    public void EnumerateOsuFiles_SingleOsuFile_ReturnsFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".osu");
        var osuFile = tempFile + ".osu";

        try
        {
            var paths = new List<string> { osuFile };

            // Act
            var files = BeatmapFileHelper.EnumerateOsuFiles(paths);

            // Assert
            Assert.Single(files);
            Assert.Equal(osuFile, files.First());
        }
        finally
        {
            File.Delete(osuFile);
        }
    }

    // 添加更多测试，如目录、.osz文件等
}