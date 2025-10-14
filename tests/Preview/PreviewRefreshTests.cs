using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Tools.N2NC;
using krrTools.Tools.Preview;
using Moq;
using Xunit;

namespace krrTools.Tests.Preview;

public class TestPreviewViewModel : PreviewViewModel
{
    public bool Refreshed { get; private set; }

    public TestPreviewViewModel(IEventBus eventBus)
    {
        // Manually inject EventBus for testing
        EventBus = eventBus;
    }

    public override void TriggerRefresh()
    {
        Refreshed = true;
        base.TriggerRefresh();
    }
}

public class MockPreviewProcessor : IPreviewProcessor
{
    public ConverterEnum? ModuleTool { get; set; }
    private int _callCount;

    public FrameworkElement BuildOriginalVisual(OsuParsers.Beatmaps.Beatmap input)
    {
        return new Canvas { Name = "Original" };
    }

    public FrameworkElement BuildConvertedVisual(OsuParsers.Beatmaps.Beatmap input)
    {
        _callCount++;
        var canvas = new Canvas { Name = $"Converted_{_callCount}" };
        // Add some notes to simulate changes
        for (var i = 0; i < _callCount; i++) canvas.Children.Add(new Rectangle { Width = 10, Height = 10 });
        return canvas;
    }
}

public class PreviewRefreshTests
{
    [Fact]
    public async Task PreviewRefreshesAndNotesChangeWhenSettingsChange()
    {
        await STATestHelper.RunInSTAAsync(async () =>
        {
            // Arrange
            var eventBus = new Mock<IEventBus>();
            var handlers = new List<Action<SettingsChangedEvent>>();
            eventBus.Setup(e => e.Subscribe(It.IsAny<Action<SettingsChangedEvent>>()))
                .Returns(() => new Mock<IDisposable>().Object)
                .Callback<Action<SettingsChangedEvent>>(h => handlers.Add(h));

            var processor = new MockPreviewProcessor();
            var viewModel = new TestPreviewViewModel(eventBus.Object);
            viewModel.SetProcessor(processor);

            // Load a sample beatmap
            viewModel.LoadBuiltInSample();

            // Wait for initial refresh
            await Task.Delay(100); // Simple wait, in real test might need better synchronization

            var initialConvertedVisual = viewModel.ConvertedVisual as Canvas;
            Assert.NotNull(initialConvertedVisual);
            var initialNoteCount = initialConvertedVisual.Children.Count;

            // Act: Simulate settings change
            var settingsEvent = new SettingsChangedEvent
            {
                PropertyName = "TargetKeys",
                SettingsType = typeof(N2NCOptions),
                NewValue = 7 // Change to 7 keys
            };
            handlers.ForEach(h => h(settingsEvent));

            // Wait for refresh
            await Task.Delay(100);

            // Assert: ConvertedVisual should be updated and notes should have changed
            var updatedConvertedVisual = viewModel.ConvertedVisual as Canvas;
            Assert.NotNull(updatedConvertedVisual);
            var updatedNoteCount = updatedConvertedVisual.Children.Count;

            Assert.True(initialNoteCount != updatedNoteCount, "Notes should have changed after settings update");
            Assert.True(updatedNoteCount > initialNoteCount, "Notes should have increased or changed");
        });
    }

    [Fact]
    public void PreviewRefreshesWhenSettingsChange()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var eventBus = new Mock<IEventBus>();
            var handlers = new List<Action<SettingsChangedEvent>>();
            eventBus.Setup(e => e.Subscribe(It.IsAny<Action<SettingsChangedEvent>>()))
                .Returns(() => new Mock<IDisposable>().Object)
                .Callback<Action<SettingsChangedEvent>>(h => handlers.Add(h));
            var processor = new Mock<IPreviewProcessor>();
            var viewModel = new TestPreviewViewModel(eventBus.Object);
            viewModel.SetProcessor(processor.Object);

            // Act: Simulate settings change by publishing SettingsChangedEvent
            var settingsEvent = new SettingsChangedEvent
            {
                PropertyName = "TargetKeys",
                SettingsType = typeof(N2NCOptions),
                NewValue = 7
            };
            handlers.ForEach(h => h(settingsEvent));

            // Assert: TriggerRefresh should have been called
            Assert.True(viewModel.Refreshed, "Preview should refresh when relevant settings change");
        });
    }
}