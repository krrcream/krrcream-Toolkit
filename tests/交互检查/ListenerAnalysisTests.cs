using System;
using System.Collections.Generic;
using System.Reflection;
using krrTools.Tools.Listener;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Utilities;
using krrTools.Beatmaps;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Injector = krrTools.Bindable.Injector;

namespace krrTools.Tests.交互检查
{
    public class ListenerAnalysisTests : IDisposable
    {
        public ListenerAnalysisTests()
        {
            // Setup dependency injection for tests
            var mockEventBus = new Mock<IEventBus>();
            var services     = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            services.AddSingleton<OsuMonitorService>();
            services.AddSingleton<BeatmapAnalysisService>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);
        }

        public void Dispose()
        {
            // Clean up test service provider
            Injector.SetTestServiceProvider(null);
        }

        [Fact]
        public void ListenerViewModel_AnalysisProperties_ShouldInitializeCorrectly()
        {
            // Setup dependency injection for this test
            var mockEventBus = new Mock<IEventBus>();
            var services     = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            services.AddSingleton<OsuMonitorService>();
            services.AddSingleton<BeatmapAnalysisService>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);

            try
            {
                // Arrange & Act
                var viewModel = new ListenerViewModel();

                // Assert - 验证ViewModel的分析属性初始化
                Assert.Equal(0.0, viewModel.XxySR.Value);
                Assert.Equal(-1.0, viewModel.KrrLV.Value);
                Assert.Equal(-1.0, viewModel.YlsLV.Value);
                Assert.Equal(0.0, viewModel.MaxKPS.Value);
                Assert.Equal(0.0, viewModel.AvgKPS.Value);
            }
            finally
            {
                // Clean up
                Injector.SetTestServiceProvider(null);
            }
        }
    }
}
