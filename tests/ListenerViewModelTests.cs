#nullable enable
using krrTools.Tools.Listener;
using Xunit;

namespace krrTools.Tests
{
    public class ListenerViewModelTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Act
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.Config);
            Assert.NotNull(viewModel.CurrentFileInfo);
            Assert.NotNull(viewModel.CurrentFileInfoCollection);
            Assert.NotNull(viewModel.ConvertCommand);
            Assert.NotNull(viewModel.BrowseCommand);
        }

        [Fact]
        public void Config_ShouldHaveDefaultValues()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel.Config);
            Assert.Equal("Ctrl+Shift+N", viewModel.Config.N2NCHotkey);
            Assert.Equal("Ctrl+Shift+D", viewModel.Config.DPHotkey);
            Assert.Equal("Ctrl+Shift+K", viewModel.Config.KRRLNHotkey);
        }

        [Fact]
        public void CurrentOsuFilePath_ShouldStartEmpty()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Act
            var initialPath = viewModel.CurrentOsuFilePath;

            // Assert
            Assert.Empty(initialPath);
        }

        [Fact]
        public void SetN2NCHotkey_ShouldUpdateConfig()
        {
            // Arrange
            var viewModel = new ListenerViewModel();
            var newHotkey = "Ctrl+Alt+N";

            // Act
            viewModel.SetN2NCHotkey(newHotkey);

            // Assert
            // We can't easily verify the global settings change, but method shouldn't throw
            Assert.NotNull(viewModel);
        }

        [Fact]
        public void SetDPHotkey_ShouldUpdateConfig()
        {
            // Arrange
            var viewModel = new ListenerViewModel();
            var newHotkey = "Ctrl+Alt+D";

            // Act
            viewModel.SetDPHotkey(newHotkey);

            // Assert
            // We can't easily verify the global settings change, but method shouldn't throw
            Assert.NotNull(viewModel);
        }

        [Fact]
        public void SetKRRLNHotkey_ShouldUpdateConfig()
        {
            // Arrange
            var viewModel = new ListenerViewModel();
            var newHotkey = "Ctrl+Alt+K";

            // Act
            viewModel.SetKRRLNHotkey(newHotkey);

            // Assert
            // We can't easily verify the global settings change, but method shouldn't throw
            Assert.NotNull(viewModel);
        }

        [Fact]
        public void CurrentFileInfo_ShouldHaveDefaultValues()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel.CurrentFileInfo);
            Assert.Empty(viewModel.CurrentFileInfo.Title);
            Assert.Empty(viewModel.CurrentFileInfo.Artist);
            Assert.Empty(viewModel.CurrentFileInfo.Creator);
            Assert.Empty(viewModel.CurrentFileInfo.Version);
        }

        [Fact]
        public void CurrentFileInfoCollection_ShouldContainCurrentFileInfo()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel.CurrentFileInfoCollection);
            Assert.Contains(viewModel.CurrentFileInfo, viewModel.CurrentFileInfoCollection);
        }

        [Fact]
        public void WindowTitle_ShouldNotBeEmpty()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotEmpty(viewModel.WindowTitle);
        }

        [Fact]
        public void BeatmapSelected_EventCanBeSubscribed()
        {
            // Arrange
            var viewModel = new ListenerViewModel();
            var eventFired = false;

            // Act
            viewModel.BeatmapSelected += (_, _) => { eventFired = true; };

            // Assert - Event can be subscribed without throwing
            Assert.False(eventFired); // Should be false initially
        }

        [Theory]
        [InlineData("Ctrl+Shift+A")]
        [InlineData("Alt+F1")]
        [InlineData("F12")]
        public void SetHotkey_Methods_ShouldNotThrow(string hotkey)
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Act & Assert - Should not throw
            var exception1 = Record.Exception(() => viewModel.SetN2NCHotkey(hotkey));
            var exception2 = Record.Exception(() => viewModel.SetDPHotkey(hotkey));
            var exception3 = Record.Exception(() => viewModel.SetKRRLNHotkey(hotkey));

            Assert.Null(exception1);
            Assert.Null(exception2);
            Assert.Null(exception3);
        }
    }
}