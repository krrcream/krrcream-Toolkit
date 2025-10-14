#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;
using krrTools.Tools.Preview;
using Moq;
using Xunit;

namespace krrTools.Tests.转换功能检查;

/// <summary>
/// 谱面转换模块测试 - 专注于设置响应、事件通知和随机一致性
/// </summary>
public class BeatmapTransformationSettingsTests
{
    [Fact]
    public void N2NCOptions_PropertyChanged_ShouldFireEvents()
    {
        // Arrange
        var options = new N2NCOptions();
        var propertyChangedEvents = new List<PropertyChangedEventArgs>();

        options.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

        // Act
        options.TargetKeys.Value = 8;
        options.TransformSpeed.Value = 3.0;
        options.Seed = 66666;

        // Assert
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.TargetKeys));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.TransformSpeed));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.Seed));

        // 验证值确实已更新
        Assert.Equal(8, options.TargetKeys.Value);
        Assert.Equal(3.0, options.TransformSpeed.Value);
        Assert.Equal(66666, options.Seed);
    }

    [Fact]
    public void N2NCOptions_SameSeed_ShouldRetainValue()
    {
        // Arrange
        var options1 = new N2NCOptions { Seed = 12345 };
        var options2 = new N2NCOptions { Seed = 12345 };

        // Assert
        Assert.Equal(options1.Seed, options2.Seed);
        Assert.Equal(12345, options1.Seed);
        Assert.Equal(12345, options2.Seed);
    }

    [Fact]
    public void N2NCOptions_NullSeed_ShouldBeHandled()
    {
        // Arrange & Act
        var options = new N2NCOptions { Seed = null };

        // Assert
        Assert.Null(options.Seed);

        // Act - 设置非null值
        options.Seed = 99999;
        Assert.Equal(99999, options.Seed);
    }

    [Fact]
    public void KRRLNTransformerOptions_NestedSettings_ShouldFireEvents()
    {
        // Arrange
        var options = new KRRLNTransformerOptions();
        var propertyChangedEvents = new List<PropertyChangedEventArgs>();

        // 监听主选项的事件
        options.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

        // Act
        options.ShortPercentage.Value = 75.0;
        options.LongLevel.Value = 8.0;
        options.ShortRandom.Value = 25.0;

        // Assert
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.ShortPercentage));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.LongLevel));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.ShortRandom));

        // 验证值确实已更新
        Assert.Equal(75.0, options.ShortPercentage.Value);
        Assert.Equal(8.0, options.LongLevel.Value);
        Assert.Equal(25.0, options.ShortRandom.Value);
    }

    [Fact]
    public void DPToolOptions_BooleanSettings_ShouldFireEvents()
    {
        // Arrange
        var options = new DPToolOptions();
        var propertyChangedEvents = new List<PropertyChangedEventArgs>();

        options.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

        // Act
        options.SingleSideKeyCount.Value = 8;
        options.LMirror.Value = true;
        options.LDensity.Value = true; // 从默认false改为true才会触发事件

        // Assert
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.SingleSideKeyCount));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.LMirror));
        Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(options.LDensity));

        // 验证值确实已更新
        Assert.Equal(8, options.SingleSideKeyCount.Value);
        Assert.True(options.LMirror.Value);
        Assert.True(options.LDensity.Value);
    }

    [Fact]
    public void N2NCViewModel_SettingsChange_ShouldTriggerPropertyChanged()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var options = new N2NCOptions();
            options.TargetKeys.Value = 7;
            options.TransformSpeed.Value = 2.0;
            options.Seed = 12345;
            var viewModel = new N2NCViewModel(options);
            var propertyChangedEvents = new List<PropertyChangedEventArgs>();

            viewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);
            //
            // // Act
            viewModel.TargetKeys = 10; // 修改设置

            // Assert
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.TargetKeys));
            Assert.Equal(10, viewModel.TargetKeys); // 设置应该已更新
            Assert.Equal(7, options.TargetKeys.Value); // 底层选项应该是设置的值
        });
    }

    [Fact]
    public void N2NCViewModel_TransformSpeed_ShouldUpdateRelatedProperties()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var options = new N2NCOptions();
            options.TransformSpeed.Value = 2.0;
            var viewModel = new N2NCViewModel(options);
            var propertyChangedEvents = new List<PropertyChangedEventArgs>();

            viewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);
            //
            // // Act
            viewModel.TransformSpeed = 4.0;

            // Assert
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == nameof(viewModel.TransformSpeed));
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == "TransformSpeedDisplay");
            Assert.Contains(propertyChangedEvents, e => e.PropertyName == "TransformSpeedSlot");

            // Assert.Equal(4.0, viewModel.TransformSpeed);
            Assert.Equal(4.0, options.TransformSpeed.Value);
        });
    }

    [Fact]
    public void N2NCOptions_ValidationConstraints_ShouldBeEnforced()
    {
        // Arrange & Act & Assert
        var options = new N2NCOptions();

        // 测试键位范围限制（通过Option特性定义的Min/Max）
        Assert.InRange(options.TargetKeys.Value, 1, 18);

        options.TargetKeys.Value = 5;
        Assert.Equal(5, options.TargetKeys.Value);

        // 测试转换速度范围限制  
        Assert.InRange(options.TransformSpeed.Value, 1, 8);

        // 种子值可以为null或任意整数
        options.Seed = null;
        Assert.Null(options.Seed);

        options.Seed = -999999;
        Assert.Equal(-999999, options.Seed);
    }

    [Fact]
    public void OptionsClasses_MultipleInstancesWithSameSettings_ShouldBeIndependent()
    {
        // Arrange
        var options1 = new N2NCOptions();
        options1.TargetKeys.Value = 7;
        options1.Seed = 12345;
        var options2 = new N2NCOptions();
        options2.TargetKeys.Value = 7;
        options2.Seed = 12345;

        var events1 = new List<PropertyChangedEventArgs>();
        var events2 = new List<PropertyChangedEventArgs>();

        options1.PropertyChanged += (_, e) => events1.Add(e);
        options2.PropertyChanged += (_, e) => events2.Add(e);

        // Act
        options1.TargetKeys.Value = 10; // 只修改options1

        // Assert
        Assert.Equal(10, options1.TargetKeys.Value); // options1已更新
        Assert.Equal(7, options2.TargetKeys.Value); // options2保持不变

        Assert.Single(events1); // options1触发了一个事件
        Assert.Empty(events2); // options2没有事件
    }

    [Theory]
    [InlineData(4, 7, 12345)]
    [InlineData(4, 10, 54321)]
    [InlineData(6, 8, 99999)]
    public void N2NCOptions_ParameterizedSettings_ShouldRetainValues(int originalKeys, int targetKeys, int seed)
    {
        // Arrange & Act
        var options = new N2NCOptions();
        options.TargetKeys.Value = targetKeys;
        options.TransformSpeed.Value = 2.0;
        options.Seed = seed; // Assert
        Assert.Equal(targetKeys, options.TargetKeys.Value);
        Assert.Equal(2.0, options.TransformSpeed.Value);
        Assert.Equal(seed, options.Seed);
    }

    [Fact]
    public void PreviewViewModel_ProcessorChange_ShouldUpdateProperties()
    {
        STATestHelper.RunInSTA(() =>
        {
            // Arrange
            var previewViewModel = new PreviewViewModel();
            var mockProcessor = new Mock<IPreviewProcessor>();
            var propertyChangedEvents = new List<PropertyChangedEventArgs>();

            previewViewModel.PropertyChanged += (_, e) => propertyChangedEvents.Add(e);

            // Act
            previewViewModel.SetProcessor(mockProcessor.Object);

            // Assert
            Assert.Equal(mockProcessor.Object, previewViewModel.Processor);

            // 应该触发相关属性变化事件
            // 注意：具体的属性名可能需要根据实际实现调整
            Assert.NotEmpty(propertyChangedEvents);
        });
    }

    [Fact]
    public void AllOptionsClasses_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act & Assert
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new N2NCOptions());
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new KRRLNTransformerOptions());
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new DPToolOptions());

        // 测试直接的 Bindable<T> 属性
        var krrOptions = new KRRLNTransformerOptions();
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.ShortPercentage);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.LongLevel);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.LengthThreshold);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.Alignment);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.LNAlignment);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(krrOptions.ProcessOriginalIsChecked);
    }
}