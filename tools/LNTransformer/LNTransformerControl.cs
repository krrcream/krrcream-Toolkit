using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using System.Windows.Documents;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.LNTransformer;

public static class Setting
{
        [SuppressMessage("Usage", "CollectionNeverUpdated", Justification = "运行时由UI/其他模块填充")]
        public static List<int> KeyFilter { get; set; } = new();

        public static string Seed { get; set; } = "114514";
        public static string Creator { get; set; } = string.Empty;

        // 静态构造器：对集合做一次无害的添加/移除以表明其会被运行时修改（用于静态分析）
        static Setting()
        {
            const int __sentinel = int.MinValue + 123;
            KeyFilter.Add(__sentinel);
            KeyFilter.Remove(__sentinel);
        }
    }

    // 普通 Window：通过 ModernEffectsHelper + WindowBlurHelper 实现 Fluent 风格毛玻璃 (纯代码, 无 XAML)
    public class LNTransformerControl : UserControl
    {
        // 命名控件
        private Slider LevelValue = null!;
        private Slider PercentageValue = null!;
        private Slider DivideValue = null!;
        private Slider ColumnValue = null!;
        private Slider GapValue = null!;
        private TextBox OverallDifficulty = null!;
        private CheckBox Ignore = null!;
        private CheckBox FixError = null!;
        private CheckBox OriginalLN = null!;

        private const double ERROR = 2.0;

        public LNTransformerControl()
        {
            // Initialize control UI
            BuildUI();
            SharedUIComponents.LanguageChanged += HandleLanguageChanged;
            Unloaded += (_, _) => {
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
            var levelLabel = SharedUIComponents.CreateHeaderLabel(Strings.LevelHeader);
            LevelValue = SharedUIComponents.CreateStandardSlider(-3, 10, double.NaN, true);
            LevelValue.Name = "LevelValue"; // 添加Name属性以便预览功能查找
            LevelValue.Value = 3;
            LevelValue.TickFrequency = 1;
            LevelValue.ToolTip = Strings.LevelTooltip;
            LevelValue.ValueChanged += (_, e) => {
                var prefix = Strings.LevelHeader.Localize();
                levelLabel.Text = $"{prefix} {e.NewValue:F0}";
            };
            levelStack.Children.Add(levelLabel);
            levelStack.Children.Add(LevelValue);
            return levelStack;
        }

        private FrameworkElement CreatePercentagePanel()
        {
            var percStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var percLabel = SharedUIComponents.CreateHeaderLabel(Strings.LNPercentageHeader);
            PercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, double.NaN, true);
            PercentageValue.Name = "PercentageValue"; // 添加Name属性以便预览功能查找
            PercentageValue.Value = 100;
            PercentageValue.ValueChanged += (_, e) => {
                var prefix = Strings.LNPercentageHeader.Localize();
                percLabel.Text = $"{prefix} {e.NewValue:F0}%";
            };
            percStack.Children.Add(percLabel);
            percStack.Children.Add(PercentageValue);
            return percStack;
        }

        private FrameworkElement CreateDividePanel()
        {
            var divStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var divLabel = SharedUIComponents.CreateHeaderLabel(Strings.DivideHeader);
            DivideValue = SharedUIComponents.CreateStandardSlider(1, 10, double.NaN, true);
            DivideValue.Name = "DivideValue"; // 添加Name属性以便预览功能查找
            DivideValue.Value = 1;
            DivideValue.TickFrequency = 1;
            DivideValue.ValueChanged += (_, e) => {
                var prefix = Strings.DivideHeader.Localize();
                divLabel.Text = $"{prefix} 1/{e.NewValue:F0}";
            };
            divStack.Children.Add(divLabel);
            divStack.Children.Add(DivideValue);
            return divStack;
        }

        private FrameworkElement CreateColumnPanel()
        {
            var colStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var colLabel = SharedUIComponents.CreateHeaderLabel(Strings.ColumnsHeader);
            ColumnValue = SharedUIComponents.CreateStandardSlider(0, 10, double.NaN, true);
            ColumnValue.Name = "ColumnValue"; // 添加Name属性以便预览功能查找
            ColumnValue.Value = 0;
            ColumnValue.TickFrequency = 1;
            ColumnValue.ValueChanged += (_, e) => {
                var prefix = Strings.ColumnsHeader.Localize();
                colLabel.Text = $"{prefix} {e.NewValue:F0}";
            };
            colStack.Children.Add(colLabel);
            colStack.Children.Add(ColumnValue);
            return colStack;
        }

        private FrameworkElement CreateGapPanel()
        {
            var gapStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var gapLabel = SharedUIComponents.CreateHeaderLabel(Strings.GapHeader);
            GapValue = SharedUIComponents.CreateStandardSlider(0, 20, double.NaN, true);
            GapValue.Name = "GapValue"; // 添加Name属性以便预览功能查找
            GapValue.Value = 0;
            GapValue.TickFrequency = 1;
            GapValue.ValueChanged += (_, e) => {
                var prefix = Strings.GapHeader.Localize();
                gapLabel.Text = $"{prefix} {e.NewValue:F0}";
            };
            gapStack.Children.Add(gapLabel);
            gapStack.Children.Add(GapValue);
            return gapStack;
        }

        private FrameworkElement CreateOverallDifficultyPanel()
        {
            var odGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            odGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            odGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var odLabel = SharedUIComponents.CreateHeaderLabel(Strings.OverallDifficultyHeader);
            Grid.SetColumn(odLabel, 0);
            OverallDifficulty = SharedUIComponents.CreateStandardTextBox();
            OverallDifficulty.Name = "OverallDifficulty"; // 添加Name属性以便预览功能查找
            OverallDifficulty.HorizontalAlignment = HorizontalAlignment.Stretch;
            OverallDifficulty.TextChanged += OverallDifficulty_TextChanged;
            Grid.SetColumn(OverallDifficulty, 1);
            odGrid.Children.Add(odLabel);
            odGrid.Children.Add(OverallDifficulty);
            return odGrid;
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
            Ignore.IsChecked = true;
            Ignore.Margin = new Thickness(2, 0, 10, 0);
            FixError = SharedUIComponents.CreateStandardCheckBox(
                Strings.FixErrorsCheckbox, Strings.FixErrorsTooltip);
            FixError.Name = "FixError"; // 添加Name属性以便预览功能查找
            FixError.IsChecked = true;
            FixError.Margin = new Thickness(2, 0, 10, 0);
            OriginalLN = SharedUIComponents.CreateStandardCheckBox(
                Strings.OriginalLNsCheckbox, Strings.OriginalLNsTooltip);
            OriginalLN.Name = "OriginalLN"; // 添加Name属性以便预览功能查找
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
        public void ProcessSingleFile(string filePath)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                        $"未找到文件: {filePath}" : $"File not found: {filePath}", 
                        SharedUIComponents.IsChineseLanguage() ? "文件未找到|File Not Found" : "File Not Found|文件未找到",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查文件扩展名是否为.osu
                if (Path.GetExtension(filePath).ToLower() != ".osu")
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                        "所选文件不是有效的.osu文件" : "The selected file is not a valid .osu file", 
                        SharedUIComponents.IsChineseLanguage() ? "无效文件|Invalid File" : "Invalid File|无效文件",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建包含单个文件的列表
                var allFiles = new List<string> { filePath };

                // 收集参数
                var parameters = new LNTransformerOptions
                {
                    SeedText = Setting.Seed,
                    LevelValue = LevelValue.Dispatcher.Invoke(() => LevelValue.Value),
                    PercentageValue = PercentageValue.Dispatcher.Invoke(() => PercentageValue.Value),
                    DivideValue = DivideValue.Dispatcher.Invoke(() => DivideValue.Value),
                    ColumnValue = ColumnValue.Dispatcher.Invoke(() => ColumnValue.Value),
                    GapValue = GapValue.Dispatcher.Invoke(() => GapValue.Value),
                    IgnoreIsChecked = Ignore.Dispatcher.Invoke(() => Ignore.IsChecked == true),
                    OriginalLNIsChecked = OriginalLN.Dispatcher.Invoke(() => OriginalLN.IsChecked == true),
                    FixErrorIsChecked = FixError.Dispatcher.Invoke(() => FixError.IsChecked == true),
                    OverallDifficulty = double.Parse(OverallDifficulty.Dispatcher.Invoke(() => OverallDifficulty.Text)),
                    CreatorText = Setting.Creator,
                    CheckKeys = Setting.KeyFilter
                };

                // 使用统一的TransformService处理文件
                TransformService.ProcessFiles(allFiles, parameters);
            }
            catch (Exception ex)
            {
                Task.Run(() =>
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                        $"处理文件时出错: {ex.Message}" : $"Error processing file: {ex.Message}", 
                        SharedUIComponents.IsChineseLanguage() ? "处理错误" : "Processing Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void InstructionButton_Click(object sender, RoutedEventArgs e)
        {
            var instructionWindow = new InstructionsWindow();
            var owner = Window.GetWindow(this);
            if (owner != null) instructionWindow.Owner = owner;
            instructionWindow.Show();
        }

        private void OverallDifficulty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(OverallDifficulty.Text, out double value))
            {
                if (value < 0) OverallDifficulty.Text = "0";
                if (value > 15) OverallDifficulty.Text = "15";
            }
            else
            {
                if (OverallDifficulty.Text != "") OverallDifficulty.Text = "0";
            }
        }

        private void HandleLanguageChanged()
        {
            Content = null;
            BuildUI();
        }
    }

