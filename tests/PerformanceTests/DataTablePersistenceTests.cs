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
            var services     = new ServiceCollection();
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

        [Fact]
        public void PerformMemoryCleanup_ShouldNotClearDataTable()
        {
            // 创建ViewModel实例
            var viewModel = new KRRLVAnalysisViewModel();

            // 向OsuFiles集合添加一些测试数据
            var testItem1 = new KRRLVAnalysisItem { Title = "Test Song 1", Phase = AnalysisStatus.Completed };
            var testItem2 = new KRRLVAnalysisItem { Title = "Test Song 2", Phase = AnalysisStatus.Completed };

            viewModel.OsuFiles.Value.Add(testItem1);
            viewModel.OsuFiles.Value.Add(testItem2);

            int itemCountBefore = viewModel.OsuFiles.Value.Count;
            _testOutputHelper.WriteLine($"清理前数据表项目数: {itemCountBefore}");

            // 使用反射调用私有的PerformMemoryCleanup方法
            MethodInfo method = typeof(KRRLVAnalysisViewModel).GetMethod("PerformMemoryCleanup",
                                                                         BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);

            // 调用内存清理方法
            method.Invoke(viewModel, null);

            int itemCountAfter = viewModel.OsuFiles.Value.Count;
            _testOutputHelper.WriteLine($"清理后数据表项目数: {itemCountAfter}");

            // 验证数据表没有被清空
            Assert.Equal(itemCountBefore, itemCountAfter);
            Assert.Equal(2, itemCountAfter);
            Assert.Equal("Test Song 1", viewModel.OsuFiles.Value[0].Title);
            Assert.Equal("Test Song 2", viewModel.OsuFiles.Value[1].Title);

            _testOutputHelper.WriteLine("✅ 验证通过：内存清理不会清空数据表内容");

            viewModel.Dispose();
        }

        [Fact]
        public void DataTable_ShouldPreserveCompletedResults()
        {
            // Setup dependency injection for this test
            var mockEventBus = new Mock<IEventBus>();
            var services     = new ServiceCollection();
            services.AddSingleton(mockEventBus.Object);
            services.AddSingleton<StateBarManager>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Injector.SetTestServiceProvider(serviceProvider);

            try
            {
                var viewModel = new KRRLVAnalysisViewModel();

                // 模拟完成的分析项目
                var completedItem = new KRRLVAnalysisItem
                {
                    Title  = "Completed Song",
                    Artist = "Test Artist",
                    KRR_LV = 5.67,
                    YLs_LV = 8.92,
                    XXY_SR = 4.23,
                    Phase  = AnalysisStatus.Completed
                };

                viewModel.OsuFiles.Value.Add(completedItem);

                // 验证数据完整性
                Assert.Single(viewModel.OsuFiles.Value);
                Assert.Equal("Completed Song", viewModel.OsuFiles.Value[0].Title);
                Assert.Equal("Test Artist", viewModel.OsuFiles.Value[0].Artist);
                Assert.Equal(5.67, viewModel.OsuFiles.Value[0].KRR_LV);
                Assert.Equal(8.92, viewModel.OsuFiles.Value[0].YLs_LV);
                Assert.Equal(4.23, viewModel.OsuFiles.Value[0].XXY_SR);
                Assert.Equal("√", viewModel.OsuFiles.Value[0].Status);
                Assert.Equal(AnalysisStatus.Completed, viewModel.OsuFiles.Value[0].Phase);

                _testOutputHelper.WriteLine("✅ 验证通过：数据表可以正确保存完整的分析结果");

                viewModel.Dispose();
            }
            finally
            {
                // Clean up
                Injector.SetTestServiceProvider(null);
            }
        }
    }
}
