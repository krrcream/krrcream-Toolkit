#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Configuration;
using krrTools.Tools.Preview;
using krrTools.Utilities;
using Moq;
using Xunit;

namespace krrTools.Tests.交互检查;

public class FileDropZoneViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange - Create instances in STA thread for WPF components
            static ConverterEnum getActiveTabTag()
            {
                return ConverterEnum.N2NC;
            }

            var fileDispatcher = new FileDispatcher();
            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                GetActiveTabTag = getActiveTabTag
            };

            // Assert
            Assert.False(viewModel.IsConversionEnabled);
            Assert.NotNull(viewModel.DisplayText);
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

            ConverterEnum getActiveTabTag()
            {
                return ConverterEnum.N2NC;
            }

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                PreviewDual = previewDual,
                GetActiveTabTag = getActiveTabTag
            };

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
    public void SetFiles_WithNullFiles_ShouldDisableConversion()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var mockPreviewViewModel = new Mock<PreviewViewModel>();
            var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
            var fileDispatcher = new FileDispatcher();

            static ConverterEnum getActiveTabTag()
            {
                return ConverterEnum.N2NC;
            }

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                PreviewDual = previewDual,
                GetActiveTabTag = getActiveTabTag
            };

            // First enable conversion, then test disabling
            viewModel.SetFiles(["test.osu"]);

            // Act
            viewModel.SetFiles(null);

            // Assert
            Assert.False(viewModel.IsConversionEnabled);
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

            ConverterEnum getActiveTabTag()
            {
                return ConverterEnum.N2NC;
            }

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                PreviewDual = previewDual,
                GetActiveTabTag = getActiveTabTag
            };

            var propertyChangedEvents = new List<PropertyChangedEventArgs>();
            viewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

            // Act
            viewModel.SetFiles(["test.osu"]);

            // Assert
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.DisplayText));
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.IsConversionEnabled));
        });
    }

    [Fact]
    public void DisplayText_ShouldUpdateWhenFilesChange()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var mockPreviewViewModel = new Mock<PreviewViewModel>();
            var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
            var fileDispatcher = new FileDispatcher();

            static ConverterEnum getActiveTabTag()
            {
                return ConverterEnum.N2NC;
            }

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                PreviewDual = previewDual,
                GetActiveTabTag = getActiveTabTag
            };

            var initialText = viewModel.DisplayText;

            // Act
            viewModel.SetFiles(["test1.osu", "test2.osu"]);
            var updatedText = viewModel.DisplayText;

            // Assert
            Assert.NotEqual(initialText, updatedText);
            Assert.Contains("2", updatedText); // Should indicate 2 files
        });
    }
}