#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Configuration;
using krrTools.Tools.Preview;
using krrTools.UI;
using krrTools.Utilities;
using Moq;
using Xunit;

namespace krrTools.Tests
{
    public class FileDropZoneViewModelSimpleTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange - Create instances in STA thread for WPF components
                var mockPreviewViewModel = new Mock<PreviewViewModel>();
                var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
                var fileDispatcher = new FileDispatcher();
                static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

                var viewModel = new FileDropZoneViewModel(previewDual, fileDispatcher, getActiveTabTag);

                // Assert
                Assert.False(viewModel.IsConversionEnabled);
                Assert.NotEmpty(viewModel.DisplayText);
            });
        }

        [Fact]
        public void SetFiles_WithValidFiles_ShouldEnableConversion()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var mockPreviewViewModel = new Mock<PreviewViewModel>();
                var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
                var fileDispatcher = new FileDispatcher();
                ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

                var viewModel = new FileDropZoneViewModel(previewDual, fileDispatcher, getActiveTabTag);
                
                var testFiles = new[] { "test1.osu", "test2.osu" };
                var filesDroppedEventFired = false;
                string[]? droppedFiles = null;

                viewModel.FilesDropped += (_, files) =>
                {
                    filesDroppedEventFired = true;
                    droppedFiles = files;
                };

                // Act
                viewModel.SetFiles(testFiles);

                // Assert
                Assert.True(viewModel.IsConversionEnabled);
                Assert.True(filesDroppedEventFired);
                Assert.Equal(testFiles, droppedFiles);
            });
        }

        [Fact]
        public void PropertyChanged_ShouldFireWhenFilesChange()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var mockPreviewViewModel = new Mock<PreviewViewModel>();
                var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
                var fileDispatcher = new FileDispatcher();
                ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

                var viewModel = new FileDropZoneViewModel(previewDual, fileDispatcher, getActiveTabTag);
                
                var propertyChangedEvents = new List<PropertyChangedEventArgs>();
                viewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

                // Act
                viewModel.SetFiles(["test.osu"]);

                // Assert
                Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.DisplayText));
                Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.IsConversionEnabled));
            });
        }
    }
}