using Xunit;
using krrTools.Beatmaps;

namespace krrTools.Tests;

public class OsuAnalyzerTests
{
    [Fact]
    public void Analyze_InvalidFilePath_ThrowsException()
    {
        // Arrange
        var analyzer = new OsuAnalyzer();
        var invalidPath = "nonexistent.osu";

        // Act & Assert
        Assert.Throws<System.IO.FileNotFoundException>(() => analyzer.Analyze(invalidPath));
    }
}