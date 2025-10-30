#nullable enable
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.Preview;
using krrTools.Utilities;
using Moq;
using Xunit;

namespace krrTools.Tests.交互检查
{
    public class FileDropZoneSimpleTests
    {
        private readonly Mock<IModuleManager> _mockModuleManager;
        private readonly Mock<IEventBus> _mockEventBus;

        public FileDropZoneSimpleTests()
        {
            _mockModuleManager = new Mock<IModuleManager>();
            _mockEventBus = new Mock<IEventBus>();
        }

        // 在STA线程中创建实例，避免Mock问题
        private FileDropZoneViewModel CreateViewModel()
        {
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);

            static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            return new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };
        }

        // 创建FileDropZone实例用于测试
        private FileDropZone CreateFileDropZone()
        {
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);
            var dropZone = new FileDropZone(fileDispatcher, true); // 使用跳过注入的构造函数
            // 手动设置ViewModel的EventBus，避免依赖注入
            dropZone.ViewModel.EventBus = _mockEventBus.Object;
            return dropZone;
        }

        [Fact]
        public void Constructor_ShouldCreateFileDropZone()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                FileDropZoneViewModel viewModel = CreateViewModel();

                // Act
                FileDropZone dropZone = CreateFileDropZone();
                dropZone.SetViewModel(viewModel);

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
                FileDropZoneViewModel viewModel = CreateViewModel();

                // Act
                FileDropZone dropZone = CreateFileDropZone();
                dropZone.SetViewModel(viewModel);

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
                FileDropZoneViewModel viewModel = CreateViewModel();

                // Act
                FileDropZone dropZone = CreateFileDropZone();
                dropZone.SetViewModel(viewModel);

                // Assert
                Assert.Equal(viewModel, dropZone.DataContext);
            });
        }

        [Fact]
        public void ViewModel_DisplayText_ShouldUpdateWhenFilesChange()
        {
            STATestHelper.RunInSTA(() =>
            {
                // Arrange
                FileDropZoneViewModel viewModel = CreateViewModel();
                FileDropZone dropZone = CreateFileDropZone();
                dropZone.SetViewModel(viewModel);
                string initialText = viewModel.DisplayText;

                // Act
                viewModel.SetFiles(["test1.osu", "test2.osu"]);
                string updatedText = viewModel.DisplayText;

                // Assert
                Assert.NotEqual(initialText, updatedText);
                Assert.Contains("2", updatedText); // Should show file count
            });
        }
    }
}
