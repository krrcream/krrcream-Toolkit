
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.KRRLNTransformer;
public class KRRLNTransformerView : ToolViewBase<KRRLNTransformerOptions>
{
    public event EventHandler? SettingsChanged;

    // 命名控件 - 只保留需要手动处理的控件
    private CheckBox AlignCheckBox = null!;
    private Slider AlignValue = null!;
    private CheckBox LNAlignCheckBox = null!;
    private Slider LNAlignValue = null!;
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

    public KRRLNTransformerView() : base(ConverterEnum.KRRLN)
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

    public KRRLNTransformerView(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, options)
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

        // 长度阈值设置 - 使用模板化控件
        var lengthThresholdPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.General.LengthThresholdValue);
        stack.Children.Add(lengthThresholdPanel);

        // 短面条设置区域标题
        var shortHeader = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
        shortHeader.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.KRRShortLNHeader.GetLocalizedString() });
        stack.Children.Add(shortHeader);

        // 短面条设置 - 使用模板化控件
        var shortPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.PercentageValue);
        stack.Children.Add(shortPercPanel);

        var shortLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.LevelValue);
        stack.Children.Add(shortLevelPanel);

        var shortLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.LimitValue);
        stack.Children.Add(shortLimitPanel);

        var shortRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.RandomValue);
        stack.Children.Add(shortRandomPanel);

        // 分隔线
        var separator1 = new Separator { Margin = new Thickness(0, 10, 0, 10) };
        stack.Children.Add(separator1);

        // 长面条设置区域标题
        var longHeader = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 0) };
        longHeader.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.KRRLongLNHeader.GetLocalizedString() });
        stack.Children.Add(longHeader);

        // 长面条设置 - 使用模板化控件
        var longPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.PercentageValue);
        stack.Children.Add(longPercPanel);

        var longLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.LevelValue);
        stack.Children.Add(longLevelPanel);

        var longLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.LimitValue);
        stack.Children.Add(longLimitPanel);

        var longRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.RandomValue);
        stack.Children.Add(longRandomPanel);

        // 分隔线
        var separator2 = new Separator { Margin = new Thickness(0, 10, 0, 10) };
        stack.Children.Add(separator2);

        // 对齐设置 - 需要自定义显示格式，手动处理
        var alignPanel = CreateAlignPanel();
        stack.Children.Add(alignPanel);

        // 处理原始面条复选框 - 使用模板化控件
        var processOriginalPanel = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.General.ProcessOriginalIsChecked);
        stack.Children.Add(processOriginalPanel);

        // OD设置 - 使用模板化控件
        var odPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.General.ODValue);
        stack.Children.Add(odPanel);

        // LN对齐设置 - 需要自定义显示格式，手动处理
        var lnAlignPanel = CreateLNAlignPanel();
        stack.Children.Add(lnAlignPanel);

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
        var label = SharedUIComponents.CreateHeaderLabel("");
        label.SetBinding(TextBlock.TextProperty, new Binding("AlignDisplayText") { Source = _viewModel });
        DockPanel.SetDock(label, Dock.Left);

        AlignCheckBox = SharedUIComponents.CreateStandardCheckBox("");
        AlignCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("Alignment.IsChecked") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        DockPanel.SetDock(AlignCheckBox, Dock.Right);

        AlignValue = SharedUIComponents.CreateStandardSlider(1, 9, 1, true);
        AlignValue.SetBinding(Slider.ValueProperty, new Binding("Alignment.Value") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        AlignValue.Value = 6;
        AlignValue.IsEnabled = false;

        // 绑定对齐复选框的启用/禁用逻辑
        AlignCheckBox.Checked += (_, _) => AlignValue.IsEnabled = true;
        AlignCheckBox.Unchecked += (_, _) => AlignValue.IsEnabled = false;

        panel.Children.Add(label);
        panel.Children.Add(AlignCheckBox);
        stack.Children.Add(panel);
        stack.Children.Add(AlignValue);

        return stack;
    }

    private FrameworkElement CreateLNAlignPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var panel = new DockPanel();
        var label = SharedUIComponents.CreateHeaderLabel("");
        label.SetBinding(TextBlock.TextProperty, new Binding("LNAlignDisplayText") { Source = _viewModel });
        DockPanel.SetDock(label, Dock.Left);

        LNAlignCheckBox = SharedUIComponents.CreateStandardCheckBox("");
        LNAlignCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("LNAlignment.IsChecked") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        DockPanel.SetDock(LNAlignCheckBox, Dock.Right);

        LNAlignValue = SharedUIComponents.CreateStandardSlider(1, 9, 1, true);
        LNAlignValue.SetBinding(Slider.ValueProperty, new Binding("LNAlignment.Value") { Source = _viewModel.Options, Mode = BindingMode.TwoWay });
        LNAlignValue.Value = 6;
        LNAlignValue.IsEnabled = false;

        // 绑定LN对齐复选框的启用/禁用逻辑
        LNAlignCheckBox.Checked += (_, _) => LNAlignValue.IsEnabled = true;
        LNAlignCheckBox.Unchecked += (_, _) => LNAlignValue.IsEnabled = false;

        panel.Children.Add(label);
        panel.Children.Add(LNAlignCheckBox);
        stack.Children.Add(panel);
        stack.Children.Add(LNAlignValue);

        return stack;
    }

    private FrameworkElement CreateSeedPanel()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = SharedUIComponents.CreateHeaderLabel(Strings.SeedButtonLabel);
        Grid.SetColumn(label, 0);

        SeedTextBox = SharedUIComponents.CreateStandardTextBox();
        SeedTextBox.Margin = new Thickness(5, 0, 5, 0);
        SeedTextBox.Text = "114514";
        SeedTextBox.IsReadOnly = false;
        Grid.SetColumn(SeedTextBox, 1);

        var generateButton = SharedUIComponents.CreateStandardButton(Strings.SeedGenerateLabel, Strings.SeedGenerateTooltip);
        generateButton.Click += (_, _) =>
        {
            var random = new Random();
            SeedTextBox.Text = random.Next(0, int.MaxValue).ToString();
        };
        Grid.SetColumn(generateButton, 2);

        grid.Children.Add(label);
        grid.Children.Add(SeedTextBox);
        grid.Children.Add(generateButton);

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
            options.Alignment.IsChecked = Dispatcher.Invoke(() => AlignCheckBox.IsChecked == true);
            options.Alignment.Value = Dispatcher.Invoke(() => AlignValue.Value);

            // LN对齐设置 - 手动处理
            options.LNAlignment.IsChecked = Dispatcher.Invoke(() => LNAlignCheckBox.IsChecked == true);
            options.LNAlignment.Value = Dispatcher.Invoke(() => LNAlignValue.Value);

            // 种子值 - 手动处理
            options.General.SeedText = Dispatcher.Invoke(() => SeedTextBox.Text);
        }
        catch
        {
            // 如果控件未初始化，使用默认值
            options.Alignment.IsChecked = true;
            options.Alignment.Value = 6;
            options.LNAlignment.IsChecked = true;
            options.LNAlignment.Value = 6;
            options.General.SeedText = "114514";
        }

        return options;
    }
}
