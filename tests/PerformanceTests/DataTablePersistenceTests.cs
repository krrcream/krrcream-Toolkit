using System;
using System.Reflection;
using krrTools.Tools.KRRLVAnalysis;
using krrTools.Bindable;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests.PerformanceTests
{
    public class DataTablePersistenceTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DataTablePersistenceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            Logger.SetConsoleOutputEnabled(false);

            // Setup dependency injection for tests
            var mockEventBus = new Mock<IEventBus>();
            var services = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);
        }

        public void Dispose()
        {
            Logger.SetConsoleOutputEnabled(true);
            Injector.SetTestServiceProvider(null);
        }
    }
}
