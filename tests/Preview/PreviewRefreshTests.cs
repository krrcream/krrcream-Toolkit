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

namespace krrTools.Tests.Preview
{
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
            for (int i = 0; i < _callCount; i++) canvas.Children.Add(new Rectangle { Width = 10, Height = 10 });
            return canvas;
        }
    }
}
