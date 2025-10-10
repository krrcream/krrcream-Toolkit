#nullable enable
using krrTools.Configuration;
using krrTools.Tools.Preview;
using krrTools.UI;
using krrTools.Utilities;
using Moq;
using Xunit;

namespace krrTools.Tests.UI
{
    public class FileDropZoneSimpleTests
    {
        // 在STA线程中创建实例，避免Mock问题
        private FileDropZoneViewModel CreateViewModel()
        {
            var mockPreviewViewModel = new Mock<PreviewViewModel>();
            var previewDual = new PreviewViewDual(mockPreviewViewModel.Object);
            var fileDispatcher = new FileDispatcher();
            static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            return new FileDropZoneViewModel(previewDual, fileDispatcher, getActiveTabTag);
        }

        [Fact]
        public void Constructor_ShouldCreateFileDropZone()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                
                // Act
                var dropZone = new FileDropZone(viewModel);

                // Assert
                Assert.NotNull(dropZone);
                Assert.True(dropZone.AllowDrop);
                Assert.Equal(viewModel, dropZone.DataContext);
            });
        }

        [Fact]
        public void Constructor_ShouldSetAllowDrop()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                
                // Act
                var dropZone = new FileDropZone(viewModel);

                // Assert
                Assert.True(dropZone.AllowDrop);
            });
        }

        [Fact]
        public void Constructor_ShouldSetDataContext()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                
                // Act
                var dropZone = new FileDropZone(viewModel);

                // Assert
                Assert.Equal(viewModel, dropZone.DataContext);
            });
        }

        [Fact]
        public void Constructor_ShouldSetHeight()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                
                // Act
                var dropZone = new FileDropZone(viewModel);

                // Assert
                Assert.True(dropZone.Height > 0);
            });
        }

        [Fact]
        public void ViewModel_IsConversionEnabled_ShouldReflectFileAvailability()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                var dropZone = new FileDropZone(viewModel);
                
                // Initially should be disabled
                Assert.False(viewModel.IsConversionEnabled);
                
                // Act - Set files
                viewModel.SetFiles(["test.osu"]);
                
                // Assert - Should be enabled
                Assert.True(viewModel.IsConversionEnabled);
                
                // Act - Clear files
                viewModel.SetFiles(null);
                
                // Assert - Should be disabled again
                Assert.False(viewModel.IsConversionEnabled);
            });
        }

        [Fact]
        public void ViewModel_DisplayText_ShouldUpdateWhenFilesChange()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                var viewModel = CreateViewModel();
                var dropZone = new FileDropZone(viewModel);
                var initialText = viewModel.DisplayText;

                // Act
                viewModel.SetFiles(["test1.osu", "test2.osu"]);
                var updatedText = viewModel.DisplayText;

                // Assert
                Assert.NotEqual(initialText, updatedText);
                Assert.Contains("2", updatedText); // Should show file count
            });
        }
    }
}