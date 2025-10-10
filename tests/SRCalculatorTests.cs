using Xunit;
using System.Collections.Generic;
using krrTools.Beatmaps;

namespace krrTools.Tests;

public class SRCalculatorTests
{
    [Fact]
    public void Calculate_EmptyNoteSequence_ThrowsException()
    {
        // Arrange
        var calculator = new SRCalculator();
        var noteSequence = new List<Note>();
        int keyCount = 4;
        double od = 8.0;

        // Act & Assert
        Assert.Throws<System.InvalidOperationException>(() => calculator.Calculate(noteSequence, keyCount, od));
    }

    [Fact]
    public void Calculate_SingleNote_ReturnsPositiveValue()
    {
        // Arrange
        var calculator = new SRCalculator();
        var noteSequence = new List<Note> { new Note(3, 5, 1000) };
        int keyCount = 4;
        double od = 8.0;

        // Act
        var result = calculator.Calculate(noteSequence, keyCount, od);

        // Assert
        Assert.True(result >= 0);
    }

    [Fact]
    public void Calculate_MultipleNotes_ReturnsHigherValue()
    {
        // Arrange
        var calculator = new SRCalculator();
        var singleNote = new List<Note> { new Note(0, 0, 1000) };
        var multipleNotes = new List<Note>
        {
            new Note(0, 0, 1000),
            new Note(1, 0, 1500),
            new Note(2, 0, 2000)
        };
        int keyCount = 4;
        double od = 8.0;

        // Act
        var singleResult = calculator.Calculate(singleNote, keyCount, od);
        var multipleResult = calculator.Calculate(multipleNotes, keyCount, od);

        // Assert
        Assert.True(multipleResult >= singleResult);
    }
}