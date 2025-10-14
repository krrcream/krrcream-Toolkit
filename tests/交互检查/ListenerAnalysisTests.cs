using System.Collections.Generic;
using System.Reflection;
using krrTools.Tools.Listener;
using Xunit;

namespace krrTools.Tests.交互检查;

public class ListenerAnalysisTests
{
    [Theory]
    [InlineData(1.0, 3.6198)]
    [InlineData(2.0, 7.2396)]
    [InlineData(3.0, 4.5)] // 在拟合公式范围内 (LOWER_BOUND = 2.76...)
    [InlineData(11.0, 31.2446)] // 在上界范围内 (UPPER_BOUND = 10.55...)，实际公式结果
    [InlineData(0.0, double.NaN)] // 边界情况
    [InlineData(15.0, double.NaN)] // 超出范围
    public void CalculateYlsLevel_ShouldReturnCorrectValues(double xxyStarRating, double expectedResult)
    {
        // Act - 使用反射访问私有方法进行测试
        var method = typeof(ListenerViewModel).GetMethod("CalculateYlsLevel",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method); // 确保方法存在

        var result = (double)method.Invoke(null, [xxyStarRating]);

        // Assert
        if (double.IsNaN(expectedResult))
            Assert.True(double.IsNaN(result), $"Expected NaN for input {xxyStarRating}, but got {result}");
        else
            Assert.Equal(expectedResult, result, 3); // 精确到3位小数
    }

    [Fact]
    public void ListenerViewModel_AnalysisProperties_ShouldInitializeCorrectly()
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

    [Fact]
    public void ListenerViewModel_AnalysisProperties_ShouldSupportPropertyChanges()
    {
        // Arrange
        var viewModel = new ListenerViewModel();
        var propertyChangedEvents = new List<string>();

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                propertyChangedEvents.Add(e.PropertyName);
        };

        // Act
        // viewModel.XxySR = 5.5;
        // viewModel.KrrLV = 10.2;
        // viewModel.YlsLV = 7.8;
        // viewModel.MaxKPS = 12.0;
        // viewModel.AvgKPS = 6.5;

        // Assert
        Assert.Contains("XxySR", propertyChangedEvents);
        Assert.Contains("KrrLV", propertyChangedEvents);
        Assert.Contains("YlsLV", propertyChangedEvents);
        Assert.Contains("MaxKPS", propertyChangedEvents);
        Assert.Contains("AvgKPS", propertyChangedEvents);
    }
}