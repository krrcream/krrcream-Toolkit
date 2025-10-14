#nullable enable
using System;
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Utilities;
using Moq;
using OsuParsers.Beatmaps;
using Xunit;

namespace krrTools.Tests.Utilities;

public class ConverterProcessorTests
{
    private readonly Mock<IModuleManager> _mockModuleManager;
    private readonly Func<IToolOptions>? _mockOptionsProvider;

    public ConverterProcessorTests()
    {
        _mockModuleManager = new Mock<IModuleManager>();
        _mockOptionsProvider = () => Mock.Of<IToolOptions>();
    }

    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Act
        var processor = new ConverterProcessor(_mockModuleManager.Object, _mockOptionsProvider);

        // Assert
        Assert.Equal(_mockModuleManager.Object, processor.ModuleScheduler);
        Assert.Equal(_mockOptionsProvider, processor.OptionsProvider);
    }

    [Fact]
    public void Constructor_WithNullModuleManager_ShouldNotThrow()
    {
        // Act & Assert
        var processor = new ConverterProcessor(null!, _mockOptionsProvider);
        Assert.NotNull(processor);
    }

    [Fact]
    public void Constructor_WithNullOptionsProvider_ShouldNotThrow()
    {
        // Act & Assert
        var processor = new ConverterProcessor(_mockModuleManager.Object);
        Assert.NotNull(processor);
        Assert.Null(processor.OptionsProvider);
    }

    [Fact]
    public void BuildConvertedVisual_WithNullModuleTool_ShouldReturnErrorTextBlock()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var processor = new ConverterProcessor(_mockModuleManager.Object);
            var beatmap = new Beatmap();

            // Act
            var result = processor.BuildConvertedVisual(beatmap);

            // Assert
            Assert.IsType<TextBlock>(result);
            var textBlock = result as TextBlock;
            Assert.Equal("ModuleTool == null", textBlock?.Text);
        });
    }

    [Fact]
    public void BuildConvertedVisual_WithNullModuleScheduler_ShouldReturnErrorTextBlock()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var processor = new ConverterProcessor(null!)
            {
                ModuleTool = ConverterEnum.N2NC
            };
            var beatmap = new Beatmap();

            // Act
            var result = processor.BuildConvertedVisual(beatmap);

            // Assert
            Assert.IsType<TextBlock>(result);
            var textBlock = result as TextBlock;
            Assert.Equal("ModuleScheduler == null", textBlock?.Text);
        });
    }

    [Fact]
    public void BuildConvertedVisual_WithToolNotFound_ShouldReturnErrorTextBlock()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            _mockModuleManager.Setup(m => m.GetToolByName("N2NC")).Returns((IToolModule?)null);
            var processor = new ConverterProcessor(_mockModuleManager.Object)
            {
                ModuleTool = ConverterEnum.N2NC
            };
            var beatmap = new Beatmap();

            // Act
            var result = processor.BuildConvertedVisual(beatmap);

            // Assert
            Assert.IsType<TextBlock>(result);
            var textBlock = result as TextBlock;
            Assert.Equal("Tool not found", textBlock?.Text);
        });
    }

    [Fact]
    public void ModuleTool_Property_ShouldBeSettable()
    {
        // Arrange
        var processor = new ConverterProcessor(_mockModuleManager.Object);

        // Act & Assert
        processor.ModuleTool = ConverterEnum.N2NC;
        Assert.Equal(ConverterEnum.N2NC, processor.ModuleTool);

        processor.ModuleTool = ConverterEnum.DP;
        Assert.Equal(ConverterEnum.DP, processor.ModuleTool);

        processor.ModuleTool = null;
        Assert.Null(processor.ModuleTool);
    }

    [Theory]
    [InlineData(ConverterEnum.N2NC)]
    [InlineData(ConverterEnum.DP)]
    [InlineData(ConverterEnum.KRRLN)]
    public void ModuleTool_ShouldAcceptAllConverterEnumValues(ConverterEnum converterEnum)
    {
        // Arrange
        var processor = new ConverterProcessor(_mockModuleManager.Object);

        // Act
        processor.ModuleTool = converterEnum;

        // Assert
        Assert.Equal(converterEnum, processor.ModuleTool);
    }
}