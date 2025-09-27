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

namespace krrTools.tools.KRRLNTransformer;

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
    public class KRRLNTransformerControl : UserControl
    {
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

        private Dictionary<int, string> AlignValuesDict = new Dictionary<int, string>
        {
            {1, "1/16"},
            {2, "1/8"},
            {3, "1/7"},
            {4, "1/6"},
            {5, "1/5"},
            {6, "1/4"},
            {7, "1/3"},
            {8, "1/2"},
            {9, "1/1"}
        };

        private const double ERROR = 2.0;

        public KRRLNTransformerControl()
        {
            // Initialize control UI
            BuildUI();
            SharedUIComponents.LanguageChanged += HandleLanguageChanged;
            Unloaded += (_, _) => {
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
            var shortHeader = SharedUIComponents.CreateHeaderLabel("短面");
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
            var longHeader = SharedUIComponents.CreateHeaderLabel("长面");
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
            var label = SharedUIComponents.CreateHeaderLabel("百分比");
            ShortPercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
            ShortPercentageValue.Value = 100;
            ShortPercentageValue.ValueChanged += (_, e) => {
                label.Text = $"百分比 ({e.NewValue:F0})";
            };
            label.Text = $"百分比 ({ShortPercentageValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(ShortPercentageValue);
            return stack;
        }

        private FrameworkElement CreateShortLevelPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("长度等级");
            ShortLevelValue = SharedUIComponents.CreateStandardSlider(0, 10, 1, true);
            ShortLevelValue.Value = 5;
            ShortLevelValue.ValueChanged += (_, e) => {
                label.Text = $"长度等级 ({e.NewValue:F0})";
            };
            label.Text = $"长度等级 ({ShortLevelValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(ShortLevelValue);
            return stack;
        }

        private FrameworkElement CreateShortLimitPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("限制");
            ShortLimitValue = SharedUIComponents.CreateStandardSlider(0, 20, 1, true);
            ShortLimitValue.Value = 20;
            ShortLimitValue.ValueChanged += (_, e) => {
                label.Text = $"限制 ({e.NewValue:F0})";
            };
            label.Text = $"限制 ({ShortLimitValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(ShortLimitValue);
            return stack;
        }

        private FrameworkElement CreateShortRandomPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("随机程度");
            ShortRandomValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
            ShortRandomValue.Value = 0;
            ShortRandomValue.ValueChanged += (_, e) => {
                label.Text = $"随机程度 ({e.NewValue:F0})";
            };
            label.Text = $"随机程度 ({ShortRandomValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(ShortRandomValue);
            return stack;
        }

        private FrameworkElement CreateLongPercentagePanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("百分比");
            LongPercentageValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
            LongPercentageValue.Value = 100;
            LongPercentageValue.ValueChanged += (_, e) => {
                label.Text = $"百分比 ({e.NewValue:F0})";
            };
            label.Text = $"百分比 ({LongPercentageValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(LongPercentageValue);
            return stack;
        }

        private FrameworkElement CreateLongLevelPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("长度等级");
            LongLevelValue = SharedUIComponents.CreateStandardSlider(0, 10, 1, true);
            LongLevelValue.Value = 5;
            LongLevelValue.ValueChanged += (_, e) => {
                label.Text = $"长度等级 ({e.NewValue:F0})";
            };
            label.Text = $"长度等级 ({LongLevelValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(LongLevelValue);
            return stack;
        }

        private FrameworkElement CreateLongLimitPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("限制");
            LongLimitValue = SharedUIComponents.CreateStandardSlider(0, 20, 1, true);
            LongLimitValue.Value = 20;
            LongLimitValue.ValueChanged += (_, e) => {
                label.Text = $"限制 ({e.NewValue:F0})";
            };
            label.Text = $"限制 ({LongLimitValue.Value:F0})";
            stack.Children.Add(label);
            stack.Children.Add(LongLimitValue);
            return stack;
        }

        private FrameworkElement CreateLongRandomPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = SharedUIComponents.CreateHeaderLabel("随机程度");
            LongRandomValue = SharedUIComponents.CreateStandardSlider(0, 100, 1, true);
            LongRandomValue.Value = 0;
            LongRandomValue.ValueChanged += (_, e) => {
                label.Text = $"随机程度 ({e.NewValue:F0})";
            };
            label.Text = $"随机程度 ({LongRandomValue.Value:F0})";
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
            
            var label = SharedUIComponents.CreateHeaderLabel("对齐");
            AlignValue = SharedUIComponents.CreateStandardSlider(1, 9, 1, true);
            AlignValue.Value = 6;
            AlignValue.IsEnabled = false;
            AlignCheckBox.Checked += (_, _) => AlignValue.IsEnabled = true;
            AlignCheckBox.Unchecked += (_, _) => AlignValue.IsEnabled = false;
            
            AlignValue.ValueChanged += (_, e) => {
                var key = (int)e.NewValue;
                if (AlignValuesDict.ContainsKey(key))
                    label.Text = $"对齐 ({AlignValuesDict[key]})";
            };
            
            var initialKey = (int)AlignValue.Value;
            if (AlignValuesDict.ContainsKey(initialKey))
                label.Text = $"对齐 ({AlignValuesDict[initialKey]})";
            
            panel.Children.Add(AlignCheckBox);
            panel.Children.Add(label);
            stack.Children.Add(panel);
            stack.Children.Add(AlignValue);
            
            return stack;
        }

        private FrameworkElement CreateProcessOriginalPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            ProcessOriginalCheckBox = SharedUIComponents.CreateStandardCheckBox("处理原始面条");
            ProcessOriginalCheckBox.IsChecked = false;
            stack.Children.Add(ProcessOriginalCheckBox);
            return stack;
        }

        private FrameworkElement CreateODPanel()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var panel = new DockPanel();
            
            var label = SharedUIComponents.CreateHeaderLabel("OD");
            ODValue = SharedUIComponents.CreateStandardSlider(0, 10, 0.1, true);
            ODValue.Value = 0;
            ODValue.ValueChanged += (_, e) => {
                label.Text = $"OD ({e.NewValue:F1})";
            };
            label.Text = $"OD ({ODValue.Value:F1})";
            
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
            
            var label = SharedUIComponents.CreateHeaderLabel("SEED");
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

                var parameters = new KRRLNTransformerOptions
                {
                    // 短面条设置
                    ShortPercentageValue = ShortPercentageValue.Dispatcher.Invoke(() => ShortPercentageValue.Value),
                    ShortLevelValue = ShortLevelValue.Dispatcher.Invoke(() => ShortLevelValue.Value),
                    ShortLimitValue = ShortLimitValue.Dispatcher.Invoke(() => ShortLimitValue.Value),
                    ShortRandomValue = ShortRandomValue.Dispatcher.Invoke(() => ShortRandomValue.Value),
    
                    // 长面条设置
                    LongPercentageValue = LongPercentageValue.Dispatcher.Invoke(() => LongPercentageValue.Value),
                    LongLevelValue = LongLevelValue.Dispatcher.Invoke(() => LongLevelValue.Value),
                    LongLimitValue = LongLimitValue.Dispatcher.Invoke(() => LongLimitValue.Value),
                    LongRandomValue = LongRandomValue.Dispatcher.Invoke(() => LongRandomValue.Value),
    
                    // 对齐设置
                    AlignIsChecked = AlignCheckBox.Dispatcher.Invoke(() => AlignCheckBox.IsChecked == true),
                    AlignValue = AlignValue.Dispatcher.Invoke(() => AlignValue.Value),
    
                    // 处理原始面条
                    ProcessOriginalIsChecked = ProcessOriginalCheckBox.Dispatcher.Invoke(() => ProcessOriginalCheckBox.IsChecked == true),
    
                    // OD设置
                    ODValue = ODValue.Dispatcher.Invoke(() => ODValue.Value),
    
                    // 种子值
                    SeedText = SeedTextBox.Dispatcher.Invoke(() => SeedTextBox.Text),
    
                    
                };
                var LN = new KRRLN();  
                
                var beatmap = LN.ProcessFiles(filePath,parameters);
                
                string? dir = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                string outputPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + "_KRRLN.osu");
                File.WriteAllText(outputPath, beatmap.ToString());

                // 检查文件是否实际生成
                if (!File.Exists(outputPath))
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                        $"转换完成但文件未找到: {outputPath}" : 
                        $"Conversion completed but file not found: {outputPath}", 
                        SharedUIComponents.IsChineseLanguage() ? "文件未生成|File Not Generated" : "File Not Generated|文件未生成",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 显示成功消息
                MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                    $"文件转换成功！\n输出文件: {outputPath}" : 
                    $"File conversion successful!\nOutput file: {outputPath}", 
                    SharedUIComponents.IsChineseLanguage() ? "转换成功|Conversion Successful" : "Conversion Successful|转换成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

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
        

        private void HandleLanguageChanged()
        {
            Content = null;
            BuildUI();
        }

        public KRRLNTransformerOptions GetOptions()
        {
            try
            {
                return new KRRLNTransformerOptions
                {
                    // 短面条设置
                    ShortPercentageValue = ShortPercentageValue.Value,
                    ShortLevelValue = ShortLevelValue.Value,
                    ShortLimitValue = ShortLimitValue.Value,
                    ShortRandomValue = ShortRandomValue.Value,

                    // 长面条设置
                    LongPercentageValue = LongPercentageValue.Value,
                    LongLevelValue = LongLevelValue.Value,
                    LongLimitValue = LongLimitValue.Value,
                    LongRandomValue = LongRandomValue.Value,

                    // 对齐设置
                    AlignIsChecked = AlignCheckBox.IsChecked == true,
                    AlignValue = AlignValue.Value,

                    // 处理原始面条
                    ProcessOriginalIsChecked = ProcessOriginalCheckBox.IsChecked == true,

                    // OD设置
                    ODValue = ODValue.Value,

                    // 种子值
                    SeedText = SeedTextBox.Text,
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

