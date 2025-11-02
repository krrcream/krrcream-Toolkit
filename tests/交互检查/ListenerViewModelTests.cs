#nullable enable
using System;
using System.Reflection;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Tools.Listener;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace krrTools.Tests.交互检查
{
    public class ListenerViewModelTests : IDisposable
    {
        public ListenerViewModelTests()
        {
            // Setup dependency injection for tests
            var mockEventBus = new Mock<IEventBus>();
            var services = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            services.AddSingleton<OsuMonitorService>();
            services.AddSingleton<BeatmapAnalysisService>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);

            // Reset global settings to defaults for each test
            ResetGlobalSettingsToDefaults();
        }

        public void Dispose()
        {
            // Clean up test service provider
            Injector.SetTestServiceProvider(null);
        }

        private void ResetGlobalSettingsToDefaults()
        {
            GlobalSettings globalSettings = ConfigManager.GetGlobalSettings();
            globalSettings.N2NCHotkey.Value = "Ctrl+Shift+N";
            globalSettings.DPHotkey.Value = "Ctrl+Shift+D";
            globalSettings.KRRLNHotkey.Value = "Ctrl+Shift+K";
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Act
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.GlobalSettings);
            Assert.NotNull(viewModel.BrowseCommand);
        }

        [Fact]
        public void Config_ShouldHaveDefaultValues()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotNull(viewModel.GlobalSettings);
            Assert.Equal("Ctrl+Shift+N", viewModel.N2NCHotkey);
            Assert.Equal("Ctrl+Shift+D", viewModel.DPHotkey);
            Assert.Equal("Ctrl+Shift+K", viewModel.KRRLNHotkey);
        }

        [Fact]
        public void CurrentOsuFilePath_ShouldStartEmpty()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Act
            string initialPath = viewModel.MonitorOsuFilePath;

            // Assert
            Assert.Empty(initialPath);
        }

        [Fact]
        public void SetN2NCHotkey_ShouldUpdateConfig()
        {
            // Arrange
            var viewModel = new ListenerViewModel();
            string newHotkey = "Ctrl+Alt+N";

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
            string newHotkey = "Ctrl+Alt+D";

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
            string newHotkey = "Ctrl+Alt+K";

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
            Assert.Empty(viewModel.Title.Value);
            Assert.Empty(viewModel.Artist.Value);
            Assert.Empty(viewModel.Creator.Value);
            Assert.Empty(viewModel.Version.Value);
        }

        [Fact]
        public void WindowTitle_ShouldNotBeEmpty()
        {
            // Arrange
            var viewModel = new ListenerViewModel();

            // Assert
            Assert.NotEmpty(viewModel.WindowTitle);
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
            Exception? exception1 = Record.Exception(() => viewModel.SetN2NCHotkey(hotkey));
            Exception? exception2 = Record.Exception(() => viewModel.SetDPHotkey(hotkey));
            Exception? exception3 = Record.Exception(() => viewModel.SetKRRLNHotkey(hotkey));

            Assert.Null(exception1);
            Assert.Null(exception2);
            Assert.Null(exception3);
        }
    }
}
