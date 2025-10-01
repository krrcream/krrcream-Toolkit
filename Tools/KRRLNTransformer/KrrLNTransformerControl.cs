
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.KRRLNTransformer;
public class KRRLNTransformerControl : ToolControlBase<KRRLNTransformerOptions>
{
    public event EventHandler? SettingsChanged;

    // 命名控件 - 只保留需要手动处理的控件
    private CheckBox AlignCheckBox = null!;
    private Slider AlignValue = null!;
    private TextBox SeedTextBox = null!;

    private readonly KRRLNTransformerViewModel _viewModel;

    private readonly Dictionary<int, string> AlignValuesDict = new Dictionary<int, string>
    {
        { 1, "1/16" },
        { 2, "1/8" },
        { 3, "1/7" },
        { 4, "1/6" },
        { 5, "1/5" },
        { 6, "1/4" },
        { 7, "1/3" },
        { 8, "1/2" },
        { 9, "1/1" }
    };

    public KRRLNTransformerControl() : base(ConverterEnum.KRRLN)
    {
        _viewModel = new KRRLNTransformerViewModel(Options);
        DataContext = _viewModel;
        // Initialize control UI
        BuildUI();
        Unloaded += (_, _) =>
        {
            Content = null;
        };
    }

    public KRRLNTransformerControl(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, options)
    {
        _viewModel = new KRRLNTransformerViewModel(options);
        DataContext = _viewModel;
        // Initialize control UI
        BuildUI();
        Unloaded += (_, _) =>
        {
            Content = null;
        };
    }

// 修改BuildUI方法
    private void BuildUI()
    {
        var root = CreateRootScrollViewer();
        var stack = CreateMainStackPanel();

        // 短面条设置区域标题
        var shortHeader = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
        shortHeader.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.KRRShortLNHeader.GetLocalizedString() });
        stack.Children.Add(shortHeader);

        // 短面条设置 - 使用模板化控件
        var shortPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortPercentageValue);
        stack.Children.Add(shortPercPanel);

        var shortLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortLevelValue);
        stack.Children.Add(shortLevelPanel);

        var shortLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortLimitValue);
        stack.Children.Add(shortLimitPanel);

        var shortRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortRandomValue);
        stack.Children.Add(shortRandomPanel);

        // 长面条设置区域标题
        var longHeader = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 0) };
        longHeader.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.KRRLongLNHeader.GetLocalizedString() });
        stack.Children.Add(longHeader);

        // 长面条设置 - 使用模板化控件
        var longPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongPercentageValue);
        stack.Children.Add(longPercPanel);

        var longLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongLevelValue);
        stack.Children.Add(longLevelPanel);

        var longLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongLimitValue);
        stack.Children.Add(longLimitPanel);

        var longRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongRandomValue);
        stack.Children.Add(longRandomPanel);

        // 对齐设置 - 需要自定义显示格式，手动处理
        var alignPanel = CreateAlignPanel();
        stack.Children.Add(alignPanel);

        // 处理原始面条复选框 - 使用模板化控件
        var processOriginalPanel = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.ProcessOriginalIsChecked);
        stack.Children.Add(processOriginalPanel);

        // OD设置 - 使用模板化控件
        var odPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ODValue);
        stack.Children.Add(odPanel);

        // SEED输入框 - 使用模板化控件，但需要特殊处理
        var seedPanel = CreateSeedPanel();
        stack.Children.Add(seedPanel);

        root.Content = stack;
        Content = root;
    }

    private ScrollViewer CreateRootScrollViewer()
    {
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private StackPanel CreateMainStackPanel()
    {
        return new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private FrameworkElement CreateAlignPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var panel = new DockPanel();
        AlignCheckBox = SharedUIComponents.CreateStandardCheckBox("");
        AlignCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("AlignIsChecked") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        DockPanel.SetDock(AlignCheckBox, Dock.Left);

        var label = SharedUIComponents.CreateHeaderLabel("");
        label.SetBinding(TextBlock.TextProperty, new Binding("AlignDisplayText") { Source = _viewModel });
        AlignValue = SharedUIComponents.CreateStandardSlider(1, 9, 1, true);
        AlignValue.SetBinding(Slider.ValueProperty, new Binding("AlignValue") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        AlignValue.Value = 6;
        AlignValue.IsEnabled = false;

        // 绑定对齐复选框的启用/禁用逻辑
        AlignCheckBox.Checked += (_, _) => AlignValue.IsEnabled = true;
        AlignCheckBox.Unchecked += (_, _) => AlignValue.IsEnabled = false;

        AlignValue.ValueChanged += (_, _) => { };



        panel.Children.Add(AlignCheckBox);
        panel.Children.Add(label);
        stack.Children.Add(panel);
        stack.Children.Add(AlignValue);

        return stack;
    }

    private FrameworkElement CreateSeedPanel()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = SharedUIComponents.CreateHeaderLabel(Strings.SeedButtonLabel);
        Grid.SetColumn(label, 0);

        SeedTextBox = SharedUIComponents.CreateStandardTextBox();
        SeedTextBox.Text = "114514";
        SeedTextBox.IsReadOnly = false;
        Grid.SetColumn(SeedTextBox, 1);

        grid.Children.Add(label);
        grid.Children.Add(SeedTextBox);

        return grid;
    }

    public KRRLNTransformerOptions GetOptions()
    {
        // 从ViewModel的Options获取大部分值（模板化控件自动绑定）
        var options = _viewModel.Options;

        // 手动处理未使用模板化控件的属性
        try
        {
            // 对齐设置 - 手动处理
            options.AlignIsChecked = Dispatcher.Invoke(() => AlignCheckBox.IsChecked == true);
            options.AlignValue = Dispatcher.Invoke(() => AlignValue.Value);

            // 种子值 - 手动处理
            options.SeedText = Dispatcher.Invoke(() => SeedTextBox.Text);
        }
        catch
        {
            // 如果控件未初始化，使用默认值
            options.AlignIsChecked = true;
            options.AlignValue = 6;
            options.SeedText = "114514";
        }

        return options;
    }
}
