using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Data;
using krrTools.Localization;
using krrTools.tools.Shared;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.tools.LNTransformer;

public static class Setting
{
    // 普通 Window：通过 ModernEffectsHelper + WindowBlurHelper 实现 Fluent 风格毛玻璃 (纯代码, 无 XAML)
    public class YLsLNTransformerControl : UserControl
    {
        private readonly YLsLNTransformerViewModel _viewModel = new();

        // 命名控件
        private Slider LevelValue = null!;
        private Slider PercentageValue = null!;
        private Slider DivideValue = null!;
        private Slider ColumnValue = null!;
        private Slider GapValue = null!;
        private Slider OverallDifficultySlider = null!;
        private CheckBox Ignore = null!;
        private CheckBox FixError = null!;
        private CheckBox OriginalLN = null!;

        private const double ERROR = 2.0;

        public YLsLNTransformerControl()
        {
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

        private void BuildUI()
        {
            var root = CreateRootScrollViewer();
            var stack = CreateMainStackPanel();

            // 等级设置
            var levelPanel = CreateLevelPanel();
            stack.Children.Add(levelPanel);

            // 百分比设置
            var percentagePanel = CreatePercentagePanel();
            stack.Children.Add(percentagePanel);

            // 分割设置
            var dividePanel = CreateDividePanel();
            stack.Children.Add(dividePanel);

            // 列设置
            var columnPanel = CreateColumnPanel();
            stack.Children.Add(columnPanel);

            // 间隔设置
            var gapPanel = CreateGapPanel();
            stack.Children.Add(gapPanel);

            // 总体难度设置
            var odPanel = CreateOverallDifficultyPanel();
            stack.Children.Add(odPanel);

            // 复选框设置
            var cbPanel = CreateCheckBoxesPanel();
            stack.Children.Add(cbPanel);

            // 说明链接
            var instrPanel = CreateInstructionPanel();
            stack.Children.Add(instrPanel);

            // 将内容放入滚动容器并设置为控制的内容
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

        private FrameworkElement CreateLevelPanel()
        {
            var levelStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var levelLabel = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.LevelLabel, 0));
            LevelValue = SharedUIComponents.CreateStandardSlider(-3, 10, double.NaN, true);
            LevelValue.Name = "LevelValue"; // 添加Name属性以便预览功能查找
            LevelValue.SetBinding(RangeBase.ValueProperty, new Binding("Options.LevelValue"));
            LevelValue.TickFrequency = 1;
            LevelValue.ToolTip = Strings.LevelTooltip;
            LevelValue.ValueChanged += (_, e) =>
            {
                levelLabel.Text = Strings.FormatLocalized(Strings.LevelLabel, (int)e.NewValue);
            };
            levelStack.Children.Add(levelLabel);
            levelStack.Children.Add(LevelValue);
            return levelStack;
        }

        private FrameworkElement CreatePercentagePanel()
        {
            var percStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var percLabel = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.LNPercentageLabel, 0));
            PercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, double.NaN, true);
            PercentageValue.Name = "PercentageValue"; // 添加Name属性以便预览功能查找
            PercentageValue.SetBinding(RangeBase.ValueProperty, new Binding("Options.PercentageValue"));
            PercentageValue.ValueChanged += (_, e) =>
            {
                percLabel.Text = Strings.FormatLocalized(Strings.LNPercentageLabel, (int)e.NewValue);
            };
            percStack.Children.Add(percLabel);
            percStack.Children.Add(PercentageValue);
            return percStack;
        }

        private FrameworkElement CreateDividePanel()
        {
            var divStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var divLabel = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.DivideLabel, 1));
            DivideValue = SharedUIComponents.CreateStandardSlider(1, 10, double.NaN, true);
            DivideValue.Name = "DivideValue"; // 添加Name属性以便预览功能查找
            DivideValue.SetBinding(RangeBase.ValueProperty, new Binding("Options.DivideValue"));
            DivideValue.TickFrequency = 1;
            DivideValue.ValueChanged += (_, e) =>
            {
                divLabel.Text = Strings.FormatLocalized(Strings.DivideLabel, (int)e.NewValue);
            };
            divStack.Children.Add(divLabel);
            divStack.Children.Add(DivideValue);
            return divStack;
        }

        private FrameworkElement CreateColumnPanel()
        {
            var colStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var colLabel = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.ColumnLabel, 0));
            ColumnValue = SharedUIComponents.CreateStandardSlider(0, 10, double.NaN, true);
            ColumnValue.Name = "ColumnValue"; // 添加Name属性以便预览功能查找
            ColumnValue.SetBinding(RangeBase.ValueProperty, new Binding("Options.ColumnValue"));
            ColumnValue.TickFrequency = 1;
            ColumnValue.ValueChanged += (_, e) =>
            {
                colLabel.Text = Strings.FormatLocalized(Strings.ColumnLabel, (int)e.NewValue);
            };
            colStack.Children.Add(colLabel);
            colStack.Children.Add(ColumnValue);
            return colStack;
        }

        private FrameworkElement CreateGapPanel()
        {
            var gapStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var gapLabel = SharedUIComponents.CreateHeaderLabel(Strings.FormatLocalized(Strings.GapLabel, 0));
            GapValue = SharedUIComponents.CreateStandardSlider(0, 20, double.NaN, true);
            GapValue.Name = "GapValue"; // 添加Name属性以便预览功能查找
            GapValue.SetBinding(RangeBase.ValueProperty, new Binding("Options.GapValue"));
            GapValue.TickFrequency = 1;
            GapValue.ValueChanged += (_, e) =>
            {
                gapLabel.Text = Strings.FormatLocalized(Strings.GapLabel, (int)e.NewValue);
            };
            gapStack.Children.Add(gapLabel);
            gapStack.Children.Add(GapValue);
            return gapStack;
        }

        private FrameworkElement CreateOverallDifficultyPanel()
        {
            var odStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var odLabel = SharedUIComponents.CreateHeaderLabel(Strings.OverallDifficultyHeader);
            var odInner = new Grid();
            odInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            OverallDifficultySlider = SharedUIComponents.CreateStandardSlider(0, 10.0, 0.1, true);
            OverallDifficultySlider.SmallChange = 0.1;
            OverallDifficultySlider.Name = "OverallDifficulty"; // 添加Name属性以便预览功能查找
            OverallDifficultySlider.SetBinding(RangeBase.ValueProperty, new Binding("Options.OverallDifficulty"));
            OverallDifficultySlider.ValueChanged += (_, e) =>
            {
                var prefix = Strings.OverallDifficultyHeader.Localize();
                odLabel.Text = $"{prefix}: {e.NewValue:F1}";
            };
            Grid.SetColumn(OverallDifficultySlider, 0);
            odInner.Children.Add(OverallDifficultySlider);
            odStack.Children.Add(odLabel);
            odStack.Children.Add(odInner);
            return odStack;
        }

        private FrameworkElement CreateCheckBoxesPanel()
        {
            var cbPanel = new StackPanel
            {
                Margin = new Thickness(0, 5, 5, 5),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Ignore = SharedUIComponents.CreateStandardCheckBox(
                Strings.IgnoreCheckbox, Strings.IgnoreTooltip);
            Ignore.Name = "Ignore"; // 添加Name属性
            Ignore.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Options.IgnoreIsChecked"));
            Ignore.Margin = new Thickness(2, 0, 10, 0);
            FixError = SharedUIComponents.CreateStandardCheckBox(
                Strings.FixErrorsCheckbox, Strings.FixErrorsTooltip);
            FixError.Name = "FixError"; // 添加Name属性以便预览功能查找
            FixError.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Options.FixErrorIsChecked"));
            FixError.Margin = new Thickness(2, 0, 10, 0);
            OriginalLN = SharedUIComponents.CreateStandardCheckBox(
                Strings.OriginalLNsCheckbox, Strings.OriginalLNsTooltip);
            OriginalLN.Name = "OriginalLN"; // 添加Name属性以便预览功能查找
            OriginalLN.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Options.OriginalLNIsChecked"));
            OriginalLN.Margin = new Thickness(2, 0, 10, 0);
            cbPanel.Children.Add(Ignore);
            cbPanel.Children.Add(FixError);
            cbPanel.Children.Add(OriginalLN);
            return cbPanel;
        }

        private FrameworkElement CreateInstructionPanel()
        {
            var instr = SharedUIComponents.CreateStandardTextBlock();
            instr.FontSize = SharedUIComponents.ComFontSize + 2;
            instr.Margin = new Thickness(0, 10, 0, 0);
            instr.TextAlignment = TextAlignment.Center;
            var link = new Hyperlink(new Run(Strings.InstructionsLink.Localize()));
            link.Click += InstructionButton_Click;
            instr.Inlines.Add(link);
            return instr;
        }

        // 添加处理单个文件的方法
        public Beatmap? ProcessSingleFile(string filePath)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) return null;

            // 应用转换
            var parameters = _viewModel.Options;

            // Use temporary file approach for transformation
            var tempPath = Path.GetTempFileName() + ".osu";
            try
            {
                beatmap.Save(tempPath);
                var resultPath = TransformService.ProcessSingleFile(tempPath, parameters);
                if (resultPath != null)
                {
                    return BeatmapDecoder.Decode(resultPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                var resultPath = tempPath.Replace(".osu", "") + ".osu"; // ProcessSingleFile adds .osu
                if (File.Exists(resultPath)) File.Delete(resultPath);
            }

            return null;
        }

        public string GetOutputFileName(string inputPath, Beatmap beatmap)
        {
            return beatmap.GetOsuFileName() + ".osu";
        }

        private void InstructionButton_Click(object sender, RoutedEventArgs e)
        {
            var instructionWindow = new InstructionsWindow();
            var owner = Window.GetWindow(this);
            if (owner != null) instructionWindow.Owner = owner;
            instructionWindow.Show();
        }

        private void HandleLanguageChanged()
        {
            // Save current values
            double level = LevelValue.Value;
            double perc = PercentageValue.Value;
            double divide = DivideValue.Value;
            double column = ColumnValue.Value;
            double gap = GapValue.Value;
            double od = OverallDifficultySlider.Value;
            bool ignore = Ignore.IsChecked ?? false;
            bool fixError = FixError.IsChecked ?? false;
            bool originalLN = OriginalLN.IsChecked ?? false;

            // Rebuild UI
            Content = null;
            BuildUI();

            // Restore values
            LevelValue.Value = level;
            PercentageValue.Value = perc;
            DivideValue.Value = divide;
            ColumnValue.Value = column;
            GapValue.Value = gap;
            OverallDifficultySlider.Value = od;
            Ignore.IsChecked = ignore;
            FixError.IsChecked = fixError;
            OriginalLN.IsChecked = originalLN;
        }
    }
}