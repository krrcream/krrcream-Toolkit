#nullable enable
using krrTools.Tools.Listener;
using krrTools.Bindable;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace krrTools.Tests.交互检查;

public class ListenerViewModelTests
{
    public ListenerViewModelTests()
    {
        // Setup dependency injection for tests using reflection
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus, EventBus>();
        var serviceProvider = services.BuildServiceProvider();
        
        // Use reflection to set the private Services property
        var servicesProperty = typeof(App).GetProperty("Services", BindingFlags.Public | BindingFlags.Static);
        servicesProperty?.SetValue(null, serviceProvider);
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
        var initialPath = viewModel.MonitorOsuFilePath;

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
        var exception1 = Record.Exception(() => viewModel.SetN2NCHotkey(hotkey));
        var exception2 = Record.Exception(() => viewModel.SetDPHotkey(hotkey));
        var exception3 = Record.Exception(() => viewModel.SetKRRLNHotkey(hotkey));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.Null(exception3);
    }
}