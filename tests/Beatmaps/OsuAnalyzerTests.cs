using krrTools.Beatmaps;
using Xunit;

namespace krrTools.Tests.Beatmaps;

public class OsuAnalyzerTests
{
    [Fact]
    public void Analyze_InvalidFilePath_ThrowsException()
    {
        // Arrange
        var invalidPath = "nonexistent.osu";

        // Act & Assert
        Assert.Throws<System.IO.FileNotFoundException>(() => OsuAnalyzer.Analyze(invalidPath));
    }
}