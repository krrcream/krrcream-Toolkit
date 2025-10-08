#nullable enable
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Tools.Preview;
using Moq;
using OsuParsers.Beatmaps;
using Xunit;

namespace krrTools.Tests
{
    public class IPreviewProcessorTests
    {
        [Fact]
        public void ModuleTool_Property_ShouldBeSettable()
        {
            // Arrange
            var processor = new Mock<IPreviewProcessor>();
            processor.SetupProperty(p => p.ModuleTool);

            // Act & Assert
            processor.Object.ModuleTool = ConverterEnum.N2NC;
            Assert.Equal(ConverterEnum.N2NC, processor.Object.ModuleTool);

            processor.Object.ModuleTool = ConverterEnum.DP;
            Assert.Equal(ConverterEnum.DP, processor.Object.ModuleTool);

            processor.Object.ModuleTool = null;
            Assert.Null(processor.Object.ModuleTool);
        }

        [Fact]
        public void BuildOriginalVisual_ShouldReturnFrameworkElement()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var processor = new Mock<IPreviewProcessor>();
                var beatmap = new Mock<Beatmap>().Object;
                var expectedElement = new TextBlock { Text = "Original" };
                processor.Setup(p => p.BuildOriginalVisual(beatmap)).Returns(expectedElement);

                // Act
                var result = processor.Object.BuildOriginalVisual(beatmap);

                // Assert
                Assert.Equal(expectedElement, result);
                processor.Verify(p => p.BuildOriginalVisual(beatmap), Times.Once);
            });
        }

        [Fact]
        public void BuildConvertedVisual_ShouldReturnFrameworkElement()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var processor = new Mock<IPreviewProcessor>();
                var beatmap = new Mock<Beatmap>().Object;
                var expectedElement = new TextBlock { Text = "Converted" };
                processor.Setup(p => p.BuildConvertedVisual(beatmap)).Returns(expectedElement);

                // Act
                var result = processor.Object.BuildConvertedVisual(beatmap);

                // Assert
                Assert.Equal(expectedElement, result);
                processor.Verify(p => p.BuildConvertedVisual(beatmap), Times.Once);
            });
        }

        [Theory]
        [InlineData(ConverterEnum.N2NC)]
        [InlineData(ConverterEnum.DP)]
        [InlineData(ConverterEnum.KRRLN)]
        public void ModuleTool_ShouldAcceptAllConverterEnumValues(ConverterEnum converterEnum)
        {
            // Arrange
            var processor = new Mock<IPreviewProcessor>();
            processor.SetupProperty(p => p.ModuleTool);

            // Act
            processor.Object.ModuleTool = converterEnum;

            // Assert
            Assert.Equal(converterEnum, processor.Object.ModuleTool);
        }

        [Fact]
        public void BuildOriginalVisual_WithDifferentBeatmaps_ShouldCallCorrectly()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var processor = new Mock<IPreviewProcessor>();
                var beatmap1 = new Mock<Beatmap>().Object;
                var beatmap2 = new Mock<Beatmap>().Object;
                var element1 = new Grid();
                var element2 = new Canvas();

                processor.Setup(p => p.BuildOriginalVisual(beatmap1)).Returns(element1);
                processor.Setup(p => p.BuildOriginalVisual(beatmap2)).Returns(element2);

                // Act
                var result1 = processor.Object.BuildOriginalVisual(beatmap1);
                var result2 = processor.Object.BuildOriginalVisual(beatmap2);

                // Assert
                Assert.Equal(element1, result1);
                Assert.Equal(element2, result2);
                processor.Verify(p => p.BuildOriginalVisual(beatmap1), Times.Once);
                processor.Verify(p => p.BuildOriginalVisual(beatmap2), Times.Once);
            });
        }

        [Fact]
        public void BuildConvertedVisual_WithDifferentBeatmaps_ShouldCallCorrectly()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var processor = new Mock<IPreviewProcessor>();
                var beatmap1 = new Mock<Beatmap>().Object;
                var beatmap2 = new Mock<Beatmap>().Object;
                var element1 = new Grid();
                var element2 = new Canvas();

                processor.Setup(p => p.BuildConvertedVisual(beatmap1)).Returns(element1);
                processor.Setup(p => p.BuildConvertedVisual(beatmap2)).Returns(element2);

                // Act
                var result1 = processor.Object.BuildConvertedVisual(beatmap1);
                var result2 = processor.Object.BuildConvertedVisual(beatmap2);

                // Assert
                Assert.Equal(element1, result1);
                Assert.Equal(element2, result2);
                processor.Verify(p => p.BuildConvertedVisual(beatmap1), Times.Once);
                processor.Verify(p => p.BuildConvertedVisual(beatmap2), Times.Once);
            });
        }
    }
}