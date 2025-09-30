
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.KRRLNTransformer;
public class KRRLNTransformerControl : ToolControlBase<KRRLNTransformerOptions>
{
    public event EventHandler? SettingsChanged;

    // 命名控件
    private Slider ShortPercentageValue = null!;
    private Slider ShortLevelValue = null!;
    private Slider ShortLimitValue = null!;
    private Slider ShortRandomValue = null!;
    private Slider LongPercentageValue = null!;
    private Slider LongLevelValue = null!;
    private Slider LongLimitValue = null!;
    private Slider LongRandomValue = null!;
    private CheckBox AlignCheckBox = null!;
    private Slider AlignValue = null!;
    private CheckBox ProcessOriginalCheckBox = null!;
    private Slider ODValue = null!;
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
        SharedUIComponents.LanguageChanged += HandleLanguageChanged;
        Unloaded += (_, _) =>
        {
            SharedUIComponents.LanguageChanged -= HandleLanguageChanged;
            Content = null;
        };
    }

    public KRRLNTransformerControl(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, options)
    {
        _viewModel = new KRRLNTransformerViewModel(options);
        DataContext = _viewModel;
        // Initialize control UI
        BuildUI();
        SharedUIComponents.LanguageChanged += HandleLanguageChanged;
        Unloaded += (_, _) =>
        {
            SharedUIComponents.LanguageChanged -= HandleLanguageChanged;
            Content = null;
        };
    }

// 修改BuildUI方法
    private void BuildUI()
    {
        var root = CreateRootScrollViewer();
        var stack = CreateMainStackPanel();

        // 短面条设置区域标题
        var shortHeader = SharedUIComponents.CreateHeaderLabel(Strings.KRRShortLNHeader);
        shortHeader.FontWeight = FontWeights.Bold;
        stack.Children.Add(shortHeader);

        // 短面条百分比设置
        var shortPercPanel = CreateShortPercentagePanel();
        stack.Children.Add(shortPercPanel);

        // 短面条长度等级设置
        var shortLevelPanel = CreateShortLevelPanel();
        stack.Children.Add(shortLevelPanel);

        // 短面条限制设置
        var shortLimitPanel = CreateShortLimitPanel();
        stack.Children.Add(shortLimitPanel);

        // 短面条随机程度设置
        var shortRandomPanel = CreateShortRandomPanel();
        stack.Children.Add(shortRandomPanel);

        // 长面条设置区域标题
        var longHeader = SharedUIComponents.CreateHeaderLabel(Strings.KRRLongLNHeader);
        longHeader.FontWeight = FontWeights.Bold;
        longHeader.Margin = new Thickness(0, 15, 0, 0);
        stack.Children.Add(longHeader);

        // 长面条百分比设置
        var longPercPanel = CreateLongPercentagePanel();
        stack.Children.Add(longPercPanel);

        // 长面条长度等级设置
        var longLevelPanel = CreateLongLevelPanel();
        stack.Children.Add(longLevelPanel);

        // 长面条限制设置
        var longLimitPanel = CreateLongLimitPanel();
        stack.Children.Add(longLimitPanel);

        // 长面条随机程度设置
        var longRandomPanel = CreateLongRandomPanel();
        stack.Children.Add(longRandomPanel);

        // 对齐设置
        var alignPanel = CreateAlignPanel();
        stack.Children.Add(alignPanel);

        // 处理原始面条复选框
        var processOriginalPanel = CreateProcessOriginalPanel();
        stack.Children.Add(processOriginalPanel);

        // OD设置
        var odPanel = CreateODPanel();
        stack.Children.Add(odPanel);

        // SEED输入框
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

    private FrameworkElement CreateShortPercentagePanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(
            Strings.FormatLocalized(Strings.KRRShortPercentageLabel, 0));
        ShortPercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
        ShortPercentageValue.Value = 100;
        ShortPercentageValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRShortPercentageLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRShortPercentageLabel, (int)ShortPercentageValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(ShortPercentageValue);
        return stack;
    }

    private FrameworkElement CreateShortLevelPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRShortLevelLabel, 0));
        ShortLevelValue = SharedUIComponents.CreateStandardSlider(0, 10, 1, true);
        ShortLevelValue.Value = 5;
        ShortLevelValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRShortLevelLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRShortLevelLabel, (int)ShortLevelValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(ShortLevelValue);
        return stack;
    }

    private FrameworkElement CreateShortLimitPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRShortLimitLabel, 0));
        ShortLimitValue = SharedUIComponents.CreateStandardSlider(0, 20, 1, true);
        ShortLimitValue.Value = 20;
        ShortLimitValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRShortLimitLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRShortLimitLabel, (int)ShortLimitValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(ShortLimitValue);
        return stack;
    }

    private FrameworkElement CreateShortRandomPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRShortRandomLabel, 0));
        ShortRandomValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
        ShortRandomValue.Value = 0;
        ShortRandomValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRShortRandomLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRShortRandomLabel, (int)ShortRandomValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(ShortRandomValue);
        return stack;
    }

    private FrameworkElement CreateLongPercentagePanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRLongPercentageLabel,
            0));
        LongPercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
        LongPercentageValue.Value = 100;
        LongPercentageValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRLongPercentageLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRLongPercentageLabel, (int)LongPercentageValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(LongPercentageValue);
        return stack;
    }

    private FrameworkElement CreateLongLevelPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRLongLevelLabel, 0));
        LongLevelValue = SharedUIComponents.CreateStandardSlider(0, 10, 1, true);
        LongLevelValue.Value = 5;
        LongLevelValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRLongLevelLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRLongLevelLabel, (int)LongLevelValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(LongLevelValue);
        return stack;
    }

    private FrameworkElement CreateLongLimitPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRLongLimitLabel, 0));
        LongLimitValue = SharedUIComponents.CreateStandardSlider(0, 20, 1, true);
        LongLimitValue.Value = 20;
        LongLimitValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRLongLimitLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRLongLimitLabel, (int)LongLimitValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(LongLimitValue);
        return stack;
    }

    private FrameworkElement CreateLongRandomPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRLongRandomLabel, 0));
        LongRandomValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
        LongRandomValue.Value = 0;
        LongRandomValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRLongRandomLabel, (int)e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRLongRandomLabel, (int)LongRandomValue.Value);
        stack.Children.Add(label);
        stack.Children.Add(LongRandomValue);
        return stack;
    }

    private FrameworkElement CreateAlignPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var panel = new DockPanel();
        AlignCheckBox = SharedUIComponents.CreateStandardCheckBox("");
        AlignCheckBox.IsChecked = true;
        DockPanel.SetDock(AlignCheckBox, Dock.Left);

        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRAlignLabel, ""));
        AlignValue = SharedUIComponents.CreateStandardSlider(1, 9, 1, true);
        AlignValue.Value = 6;
        AlignValue.IsEnabled = false;
        AlignCheckBox.Checked += (_, _) =>
        {
            AlignValue.IsEnabled = true;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        AlignCheckBox.Unchecked += (_, _) =>
        {
            AlignValue.IsEnabled = false;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };

        AlignValue.ValueChanged += (_, e) =>
        {
            var key = (int)e.NewValue;
            if (AlignValuesDict.TryGetValue(key, out var value))
                label.Text = Strings.FormatLocalized(Strings.KRRAlignLabel, value);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };

        var initialKey = (int)AlignValue.Value;
        if (AlignValuesDict.TryGetValue(initialKey, out var value1))
            label.Text = Strings.FormatLocalized(Strings.KRRAlignLabel, value1);

        panel.Children.Add(AlignCheckBox);
        panel.Children.Add(label);
        stack.Children.Add(panel);
        stack.Children.Add(AlignValue);

        return stack;
    }

    private FrameworkElement CreateProcessOriginalPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        ProcessOriginalCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.KRRProcessOriginalLabel);
        ProcessOriginalCheckBox.IsChecked = false;
        ProcessOriginalCheckBox.Checked += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        ProcessOriginalCheckBox.Unchecked += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        stack.Children.Add(ProcessOriginalCheckBox);
        return stack;
    }

    private FrameworkElement CreateODPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var panel = new DockPanel();

        var label = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.KRRODLabel, 0));
        ODValue = SharedUIComponents.CreateStandardSlider(0, 10, 0.1, true);
        ODValue.Value = 0;
        ODValue.ValueChanged += (_, e) =>
        {
            label.Text = Strings.FormatLocalized(Strings.KRRODLabel, e.NewValue);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        label.Text = Strings.FormatLocalized(Strings.KRRODLabel, ODValue.Value);

        panel.Children.Add(label);
        stack.Children.Add(panel);
        stack.Children.Add(ODValue);

        return stack;
    }

    private FrameworkElement CreateSeedPanel()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = SharedUIComponents.CreateHeaderLabel(Strings.KRRSeedLabel);
        Grid.SetColumn(label, 0);

        SeedTextBox = SharedUIComponents.CreateStandardTextBox();
        SeedTextBox.Text = "114514";
        SeedTextBox.IsReadOnly = false;
        Grid.SetColumn(SeedTextBox, 1);

        grid.Children.Add(label);
        grid.Children.Add(SeedTextBox);

        return grid;
    }

    // 添加处理单个文件的方法
    public Beatmap ProcessSingleFile(string filePath)
    {
        var parameters = GetOptions();
        var LN = new KRRLN();

        return LN.ProcessFiles(filePath, parameters);
    }

    public string GetOutputFileName(string inputPath, ManiaBeatmap beatmap)
    {
        return Path.GetFileNameWithoutExtension(inputPath) + "_KRRLN.osu";
    }


    private void HandleLanguageChanged()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Content = null;
            BuildUI();
        }));
    }

    public KRRLNTransformerOptions GetOptions()
    {
        try
        {
            return new KRRLNTransformerOptions
            {
                // 短面条设置
                ShortPercentageValue = Dispatcher.Invoke(() => ShortPercentageValue.Value),
                ShortLevelValue = Dispatcher.Invoke(() => ShortLevelValue.Value),
                ShortLimitValue = Dispatcher.Invoke(() => ShortLimitValue.Value),
                ShortRandomValue = Dispatcher.Invoke(() => ShortRandomValue.Value),

                // 长面条设置
                LongPercentageValue = Dispatcher.Invoke(() => LongPercentageValue.Value),
                LongLevelValue = Dispatcher.Invoke(() => LongLevelValue.Value),
                LongLimitValue = Dispatcher.Invoke(() => LongLimitValue.Value),
                LongRandomValue = Dispatcher.Invoke(() => LongRandomValue.Value),

                // 对齐设置
                AlignIsChecked = Dispatcher.Invoke(() => AlignCheckBox.IsChecked == true),
                AlignValue = Dispatcher.Invoke(() => AlignValue.Value),

                // 处理原始面条
                ProcessOriginalIsChecked = Dispatcher.Invoke(() => ProcessOriginalCheckBox.IsChecked == true),

                // OD设置
                ODValue = Dispatcher.Invoke(() => ODValue.Value),

                // 种子值
                SeedText = Dispatcher.Invoke(() => SeedTextBox.Text),
            };
        }
        catch
        {
            // 如果控件未初始化，返回默认选项
            return new KRRLNTransformerOptions
            {
                ShortPercentageValue = 50,
                ShortLevelValue = 5,
                ShortLimitValue = 20,
                ShortRandomValue = 50,
                LongPercentageValue = 50,
                LongLevelValue = 5,
                LongLimitValue = 20,
                LongRandomValue = 50,
                AlignIsChecked = false,
                AlignValue = 4,
                ProcessOriginalIsChecked = false,
                ODValue = 8,
                SeedText = "114514"
            };
        }
    }
}
