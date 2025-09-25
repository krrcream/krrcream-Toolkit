using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using krrTools.Tools.OsuParser;
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
            this.Unloaded += (_, _) => {
                SharedUIComponents.LanguageChanged -= HandleLanguageChanged;
                this.Content = null;
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
            
            this.Content = root;
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
            var levelLabel = SharedUIComponents.CreateHeaderLabel(Strings.Localize(Strings.LevelHeader));
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
                var prefix = SharedUIComponents.IsChineseLanguage() ? Strings.LNPercentageHeader.Split('|')[1] : Strings.LNPercentageHeader.Split('|')[0];
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
                var prefix = SharedUIComponents.IsChineseLanguage() ? Strings.DivideHeader.Split('|')[1] : Strings.DivideHeader.Split('|')[0];
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
                var prefix = SharedUIComponents.IsChineseLanguage() ? Strings.ColumnsHeader.Split('|')[1] : Strings.ColumnsHeader.Split('|')[0];
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
                var prefix = SharedUIComponents.IsChineseLanguage() ? Strings.GapHeader.Split('|')[1] : Strings.GapHeader.Split('|')[0];
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
            OverallDifficulty.Text = "0";
            OverallDifficulty.Width = 100;
            OverallDifficulty.Height = 30;
            OverallDifficulty.Padding = new Thickness(0);
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
            Ignore = SharedUIComponents.CreateStandardCheckBoxWithTooltip(
                Strings.IgnoreCheckbox, Strings.IgnoreTooltip);
            Ignore.Name = "Ignore"; // 添加Name属性
            Ignore.IsChecked = true;
            Ignore.Margin = new Thickness(2, 0, 10, 0);
            FixError = SharedUIComponents.CreateStandardCheckBoxWithTooltip(
                Strings.FixErrorsCheckbox, Strings.FixErrorsTooltip);
            FixError.Name = "FixError"; // 添加Name属性以便预览功能查找
            FixError.IsChecked = true;
            FixError.Margin = new Thickness(2, 0, 10, 0);
            OriginalLN = SharedUIComponents.CreateStandardCheckBoxWithTooltip(
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
            var link = new Hyperlink(new Run(SharedUIComponents.IsChineseLanguage() ? Strings.InstructionsLink.Split('|')[1] : Strings.InstructionsLink.Split('|')[0]));
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
                        SharedUIComponents.IsChineseLanguage() ? "文件未找到" : "File Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查文件扩展名是否为.osu
                if (Path.GetExtension(filePath).ToLower() != ".osu")
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                        "所选文件不是有效的.osu文件" : "The selected file is not a valid .osu file", 
                        SharedUIComponents.IsChineseLanguage() ? "无效文件" : "Invalid File",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Task.Run(() =>
                {
                    // 创建包含单个文件的列表
                    var allFiles = new List<string> { filePath };

                    // 收集参数
                    var parameters = new LNTransformParameters
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

                    var osuFiles = OsuFileProcessor.ReadMultipleFiles(allFiles, (line) =>
                    {
                        if (line.StartsWith("Mode"))
                        {
                            string str = line.Substring(line.IndexOf(':') + 1).Trim();
                            if (str != "3")
                            {
                                return true;
                            }
                        }

                        if (line.StartsWith("CircleSize"))
                        {
                            string str = line.Substring(line.IndexOf(':') + 1).Trim();
                            if (int.TryParse(str, out int cs))
                            {
                                if (cs > 10)
                                {
                                    return true;
                                }

                                if (parameters.CheckKeys.Count > 0 && !parameters.CheckKeys.Contains(cs))
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (parameters.IgnoreIsChecked && line.StartsWith("Creator"))
                            {
                                var str = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (str.Contains("LNTransformer"))
                                {
                                    return true;
                                }
                            }

                            if (parameters.IgnoreIsChecked && line.StartsWith("Version"))
                            {
                                var str = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (str.Contains("[LN"))
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    });

                    foreach (var osuFile in osuFiles)
                    {
                        try
                        {
                            ApplyToBeatmap(osuFile, parameters);
                            string newFilepath = osuFile.path + "\\" + osuFile.FileName + ".osu";
                            // 仅当监听器（监视器）打开时才打包为.osz；否则保留.osu文件
                            if (Listener.ListenerControl.IsOpen)
                            {
                                OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath);
                            }
                            else
                            {
                                // 不做任何操作：.osu文件已通过ApplyToBeatmap/Save保存到源文件夹中
                            }
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            Task.Run(() =>
                            {
                                MessageBox.Show(
                                    $"处理 {osuFile.OriginalFile!.FullName} 时出错: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}",
                                    "转换错误", MessageBoxButton.OK, MessageBoxImage.Error,
                                    MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            });
#endif
                        }
                    }

                    Task.Run(() =>
                    {
                        MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? 
                            "文件处理成功！" : "File processed successfully!", 
                            SharedUIComponents.IsChineseLanguage() ? "成功" : "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                });
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

        private void ApplyToBeatmap(OsuFileV14 osu, LNTransformParameters parameters)
        {
            bool canBeConverted = !(IsConverted(osu) && parameters.IgnoreIsChecked);

            if (!canBeConverted)
            {
                return;
            }

            var Rng = new Random(int.TryParse(parameters.SeedText, out var seed) ? seed : 114514);
            var originalLNObjects = new List<ManiaHitObject>();
            var afterObjects = new List<ManiaHitObject>();

            var timeDivide = 4 * Math.Pow(2, -parameters.DivideValue);
            var transformColumnNum = (int)parameters.ColumnValue;
            var gapValue = (int)parameters.GapValue;
            var percentage = parameters.PercentageValue / 100.0;
            var level = parameters.LevelValue;
            var originalLNIsChecked = parameters.OriginalLNIsChecked;

            if (parameters.FixErrorIsChecked)
            {
                osu.Recalculate();
            }

            if (Math.Abs(parameters.OverallDifficulty - 0) > 1e-6)
            {
                osu.General.OverallDifficulty = parameters.OverallDifficulty;
            }

            if (!string.IsNullOrEmpty(parameters.CreatorText))
            {
                osu.Metadata.Creator = parameters.CreatorText + " & LNTransformer";
            }
            else
            {
                osu.Metadata.Creator += " & LNTransformer";
            }

            osu.Metadata.Difficulty += " [LN Level " + parameters.LevelValue + "]";

            foreach (var obj in osu.HitObjects)
            {
                if (Math.Abs(obj.StartTime - obj.EndTime) < 1e-6)
                {
                    afterObjects.Add(obj);
                }
                else
                {
                    originalLNObjects.Add(obj);
                    if (originalLNIsChecked)
                    {
                        afterObjects.Add(obj);
                    }
                }
            }

            var resultObjects = new List<ManiaHitObject>();
            var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
            int keys = (int)osu.General.CircleSize;
            int maxGap = gapValue;
            int gap = maxGap;

            if (transformColumnNum > keys) transformColumnNum = keys;

            var randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng,
                transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

            foreach (var timeGroup in afterObjects.OrderBy(h => h.StartTime).GroupBy(h => h.StartTime))
            {
                foreach (var note in timeGroup)
                    if (originalLNSet.Contains(note) && originalLNIsChecked)
                        resultObjects.Add(note);
                    else if (randomColumnSet.Contains(note.Column) && Math.Abs(note.StartTime - note.EndTime) > 1e-6)
                        resultObjects.Add(note);
                    else
                        resultObjects.Add(note.ToNote());

                gap--;
                if (gap == 0)
                {
                    randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum)
                        .ToHashSet();
                    gap = maxGap;
                }
            }

            osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
            osu.WriteFile();
        }

        // Transform：根据分布生成长条，返回原始长条列表
        private List<ManiaHitObject> Transform(Random Rng, double mu, double sigmaDivisor, double divide,
            OsuFileV14 osu,
            List<ManiaHitObject> newObjects,
            IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue,
            bool fixErrorIsChecked, int? divide2 = null, double? mu2 = null, double? mu1Dmu2 = null)
        {
            var originalLNObjects = new List<ManiaHitObject>();
            var newColumnObjects = new List<ManiaHitObject>();
            var locations = column.OrderBy(h => h.StartTime).ToList();

            if (locations.Count == 0)
            {
                return originalLNObjects;
            }

            for (var i = 0; i < locations.Count - 1; i++)
            {
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime; // 两个音之间的完整间隔
                var duration = GetDurationByDistribution(Rng, osu, locations[i].StartTime, fullDuration, mu,
                    sigmaDivisor, divide, divide2, mu2, mu1Dmu2);

                var obj = locations[i];
                obj.Column = column.Key;
                if (originalLNIsChecked && Math.Abs(locations[i].StartTime - locations[i].EndTime) > 1e-6)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && !double.IsNaN(duration))
                {
                    var endTime = obj.StartTime + duration;
                    if (fixErrorIsChecked)
                    {
                        var point = osu.TimingPointAt(endTime);
                        endTime = Helper.PreciseTime(endTime, point.BeatLength, point.Time);
                    }

                    obj.EndTime = (int)endTime;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            // 处理该列最后一个音
            if (Math.Abs(locations[^1].StartTime - locations[^1].EndTime) <= ERROR + 1e-6 ||
                Rng.Next(100) >= percentageValue)
                newColumnObjects.Add(locations[^1].ToNote());
            else
                newColumnObjects.Add(locations[^1]);

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        private double GetDurationByDistribution(Random Rng, OsuFileV14 osu, int startTime, double limitDuration,
            double mu, double sigmaDivisor, double divide, int? divide2 = null, double? mu2 = null,
            double? mu1Dmu2 = null)
        {
            var beatLength = osu.TimingPointAt(startTime).BeatLength; // 节拍长度
            var timeDivide = beatLength / divide;
            var flag = true; // 是否可转换为长条
            var sigma = timeDivide / sigmaDivisor;
            var timeNum = (int)Math.Round(limitDuration / timeDivide, 0);
            var duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu / 100, sigma));

            if (mu1Dmu2.HasValue)
                if (Rng.Next(100) >= mu1Dmu2.Value && mu2.HasValue)
                {
                    timeDivide = beatLength / (divide2 ?? (int)divide);
                    sigma = timeDivide / sigmaDivisor;
                    timeNum = (int)Math.Round(limitDuration / timeDivide, 0);
                    duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu2.Value / 100, sigma));
                }

            if (Math.Abs(mu + 1.0) < 1e-6)
            {
                if (timeNum < 1)
                {
                    duration = timeDivide;
                }
                else
                {
                    var rdTime = Rng.Next(1, timeNum);
                    duration = rdTime * timeDivide;
                    duration = TimeRound(timeDivide, duration);
                }
            }

            if (duration > limitDuration - timeDivide)
            {
                duration = limitDuration - timeDivide;
                duration = TimeRound(timeDivide, duration);
            }

            if (duration <= timeDivide) duration = timeDivide;

            if (duration >= limitDuration - ERROR) // 过长则认为不可转换
                flag = false;

            return flag ? duration : double.NaN;
        }

        private List<ManiaHitObject> Invert(OsuFileV14 osu, List<ManiaHitObject> newObjects, Random Rng,
            IGrouping<int, ManiaHitObject> column, double divideValue, bool originalLNIsChecked, double percentageValue,
            bool fixErrorIsChecked)
        {
            var locations = column.OrderBy(h => h.StartTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (var i = 0; i < locations.Count - 1; i++)
            {
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
                var beatLength = osu.TimingPointAt(locations[i + 1].StartTime).BeatLength;
                var flag = true;
                var duration = fullDuration - beatLength / divideValue;

                if (duration < beatLength / divideValue) duration = beatLength / divideValue;

                if (duration > fullDuration - 3) flag = false;

                var obj = locations[i];
                obj.Column = column.Key;

                if (originalLNIsChecked && Math.Abs(locations[i].StartTime - locations[i].EndTime) > 1e-6)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && flag)
                {
                    var endTime = locations[i].StartTime + duration;
                    if (fixErrorIsChecked)
                        endTime = Helper.PreciseTime(endTime, osu.TimingPointAt(endTime).BeatLength,
                            osu.TimingPoints.First().Time);

                    obj.EndTime = (int)endTime;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            double lastStartTime = locations[^1].StartTime;
            double lastEndTime = locations[^1].EndTime;
            if (originalLNIsChecked && Math.Abs(lastStartTime - lastEndTime) > 1e-6)
            {
                var obj = locations[^1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else
            {
                var obj = locations[^1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj.ToNote());
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        private void TrueRandom(List<ManiaHitObject> newObjects, Random Rng,
            IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue)
        {
            var locations = column.OrderBy(h => h.StartTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // 长条的完整持续时间
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;

                double duration = Rng.Next((int)fullDuration) + Rng.NextDouble();
                while (duration > fullDuration)
                    duration--;

                var obj = locations[i];
                obj.Column = column.Key;

                if (originalLNIsChecked && locations[i].StartTime != locations[i].EndTime)
                {
                    newColumnObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue)
                {
                    obj.EndTime = obj.StartTime + (int)duration;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            newObjects.AddRange(newColumnObjects);
        }

        private void AfterTransform(List<ManiaHitObject> afterObjects, List<ManiaHitObject> originalLNObjects,
            OsuFileV14 osu, Random Rng, int transformColumnNum, bool originalLNIsChecked, int gapValue)
        {
            var resultObjects = new List<ManiaHitObject>();
            var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
            int keys = (int)osu.General.CircleSize;
            int maxGap = gapValue;
            int gap = maxGap;

            if (transformColumnNum > keys) transformColumnNum = keys;

            var randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng,
                transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

            foreach (var timeGroup in afterObjects.OrderBy(h => h.StartTime).GroupBy(h => h.StartTime))
            {
                foreach (var note in timeGroup)
                    if (originalLNSet.Contains(note) && originalLNIsChecked)
                        resultObjects.Add(note);
                    else if (randomColumnSet.Contains(note.Column) && Math.Abs(note.StartTime - note.EndTime) > 1e-6)
                        resultObjects.Add(note);
                    else
                        resultObjects.Add(note.ToNote());

                gap--;
                if (gap == 0)
                {
                    randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum)
                        .ToHashSet();
                    gap = maxGap;
                }
            }

            osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
        }

        private double RandDistribution(Random Rng, double u, double d)
        {
            if (d <= 0) return u;

            var u1 = Rng.NextDouble();
            var u2 = Rng.NextDouble();
            var z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
            var x = u + d * z;
            return x;
        }

        private double TimeRound(double timeDivide, double num)
        {
            var remainder = num % timeDivide;
            if (remainder < timeDivide / 2)
                return num - remainder;
            return num + timeDivide - remainder;
        }

        private bool IsConverted(OsuFileV14 osu)
        {
            var creator = osu.Metadata.Creator;
            if (!string.IsNullOrEmpty(creator) && creator.Contains("LNTransformer")) return true;

            var difficulty = osu.Metadata.Difficulty;
            if (!string.IsNullOrEmpty(difficulty) && Regex.IsMatch(difficulty, @"\[LN.*\]")) return true;

            return false;
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
            this.Content = null;
            BuildUI();
        }
    }

