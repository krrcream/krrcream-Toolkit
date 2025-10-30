#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.Preview;
using krrTools.Utilities;
using Moq;
using Xunit;

namespace krrTools.Tests.交互检查
{
    public class FileDropZoneViewModelTests
    {
        private readonly Mock<IModuleManager> _mockModuleManager;
        private readonly Mock<IEventBus> _mockEventBus;

        public FileDropZoneViewModelTests()
        {
            _mockModuleManager = new Mock<IModuleManager>();
            _mockEventBus = new Mock<IEventBus>();
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange - Create instances in STA thread for WPF components
            static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);
            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };

            // Assert
            Assert.False(viewModel.IsConversionEnabled);
            Assert.NotNull(viewModel.DisplayText);
        }

        [Fact]
        public void SetFiles_WithValidFiles_ShouldEnableConversion()
        {
            // Arrange
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);

            ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };

            string[] testFiles = new[] { "test1.osu", "test2.osu" };
            bool filesDroppedEventFired = false;
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
        }

        [Fact]
        public void SetFiles_WithNullFiles_ShouldDisableConversion()
        {
            // Arrange
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);

            static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };

            // First enable conversion, then test disabling
            viewModel.SetFiles(["test.osu"]);

            // Act
            viewModel.SetFiles(null);

            // Assert
            Assert.False(viewModel.IsConversionEnabled);
        }

        [Fact]
        public void PropertyChanged_ShouldFireWhenFilesChange()
        {
            // Arrange
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);

            ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };

            var propertyChangedEvents = new List<PropertyChangedEventArgs>();
            viewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

            // Act
            viewModel.SetFiles(["test.osu"]);

            // Assert
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.DisplayText));
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.IsConversionEnabled));
        }

        [Fact]
        public void DisplayText_ShouldUpdateWhenFilesChange()
        {
            // Arrange
            var fileDispatcher = new FileDispatcher(_mockModuleManager.Object);

            static ConverterEnum getActiveTabTag() => ConverterEnum.N2NC;

            var viewModel = new FileDropZoneViewModel(fileDispatcher)
            {
                EventBus = _mockEventBus.Object,
                GetActiveTabTag = getActiveTabTag
            };

            string initialText = viewModel.DisplayText;

            // Act
            viewModel.SetFiles(["test1.osu", "test2.osu"]);
            string updatedText = viewModel.DisplayText;

            // Assert
            Assert.NotEqual(initialText, updatedText);
            Assert.Contains("2", updatedText); // Should indicate 2 files
        }
    }
}
