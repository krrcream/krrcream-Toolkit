using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Data;
using krrTools.Localization;
using krrTools.UI;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.N2NC
{
    public class LabelConverter : IMultiValueConverter
    {
        // TODO: 这个转换器可以放到一个公共位置供所有控件使用, 但是用法不好，
        //另一种实现方法是DP工具中的OnLanguageChanged()，后续统一
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is [string labelStr, _, ..])
            {
                string valStr;
                if (values[1] == DependencyProperty.UnsetValue)
                {
                    valStr = "0";
                }
                else
                {
                    valStr = values[1].ToString() ?? "0";
                }
                if (labelStr.Contains("{0}"))
                {
                    return Strings.FormatLocalized(labelStr, valStr);
                }
                else
                {
                    return Strings.Localize(labelStr) + ": " + valStr;
                }
            }
            
            if (values is [string lbl, ..])
            {
                return Strings.Localize(lbl);
            }
            
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class N2NCControl : ToolControlBase<N2NCOptions>
    {
        public event EventHandler? SettingsChanged;
        private Slider? TargetKeysSlider;
        private Slider? MaxKeysSlider;
        private Slider? MinKeysSlider;
        private TextBox? SeedTextBox;
        private Button? GenerateSeedButton;
        private readonly Dictionary<KeySelectionFlags, CheckBox> checkboxMap = new();
        private readonly N2NCViewModel _viewModel;


        public N2NCControl() : base(ConverterEnum.N2NC)
        {
            _viewModel = new N2NCViewModel(Options);
            DataContext = _viewModel;
            BuildConverterUI();
            // Options are now loaded automatically via DI
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        public N2NCControl(N2NCOptions options) : base(ConverterEnum.N2NC, options)
        {
            _viewModel = new N2NCViewModel(options);
            DataContext = _viewModel;
            BuildConverterUI();
            // Options are now loaded automatically via DI
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dm = DataContext;
                Content = null;
                DataContext = dm;
                BuildConverterUI();
            }));
        }

        private void BuildConverterUI()
        {
            // Build control UI (no window-specific initialization)
            var scrollViewer = CreateScrollViewer();
            
            var grid = new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            var rowMargin = new Thickness(0, 6, 0, 6);

            // 创建UI控件
            var targetPanel = CreateTargetKeysPanel(rowMargin);
            grid.Children.Add(targetPanel);

            var maxPanel = CreateMaxKeysPanel(rowMargin);
            grid.Children.Add(maxPanel);

            var minPanel = CreateMinKeysPanel(rowMargin);
            grid.Children.Add(minPanel);

            var transformPanel = CreateTransformSpeedPanel(rowMargin);
            grid.Children.Add(transformPanel);

            var seedPanel = CreateSeedPanel(rowMargin);
            grid.Children.Add(seedPanel);

            var keysPanel = CreateKeySelectionPanel(rowMargin);
            grid.Children.Add(keysPanel);

            var presetsBorder = CreatePresetsPanel(rowMargin);
            grid.Children.Add(presetsBorder);

            // 组装界面 - use ScrollViewer directly as top-level Content (removed extra Grid layer)
            scrollViewer.Content = grid;
            Content = scrollViewer;

            // 事件处理
            SetupEventHandlers();
        }

        private ScrollViewer CreateScrollViewer()
        {
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private FrameworkElement CreateTargetKeysPanel(Thickness rowMargin)
        {
            // Target keys
            var targetKeysSlider = SharedUIComponents.CreateStandardSlider(1, 18, double.NaN, true);
            targetKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding("TargetKeys") { Source = _viewModel });
            TargetKeysSlider = targetKeysSlider;

            var sliderPanel = new Grid();
            sliderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(targetKeysSlider, 0);
            sliderPanel.Children.Add(targetKeysSlider);

            var labelRow = new Grid();
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var targetLabel = SharedUIComponents.CreateHeaderLabel("");
            targetLabel.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(targetLabel, 0);
            labelRow.Children.Add(targetLabel);

            // 绑定标签文本为 "本地化标签: 值"
            // 实现不友好，后续参考DP或预设的本地化
            var targetBinding = new MultiBinding
            {
                StringFormat = "{0}"
            };
            targetBinding.Bindings.Add(new Binding("Value") { Source = new LocalizedStringHelper.LocalizedString(Strings.N2NCTargetKeysTemplate) });
            targetBinding.Bindings.Add(new Binding("TargetKeys") { Source = _viewModel });
            targetBinding.Converter = new LabelConverter();
            targetLabel.SetBinding(TextBlock.TextProperty, targetBinding);

            // 主面板：标签行 + 滑条行
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            mainPanel.Children.Add(labelRow);
            mainPanel.Children.Add(sliderPanel);
            return mainPanel;
        }        private FrameworkElement CreateMaxKeysPanel(Thickness rowMargin)
        {
            var maxInner = new Grid();
            maxInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            MaxKeysSlider = SharedUIComponents.CreateStandardSlider(1, 18, double.NaN, true);
            MaxKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.MaxKeys)) { Mode = BindingMode.TwoWay, Source = _viewModel });
            MaxKeysSlider.SetBinding(RangeBase.MaximumProperty, new Binding(nameof(N2NCViewModel.MaxKeysMaximum)) { Mode = BindingMode.OneWay, Source = _viewModel });
            Grid.SetColumn(MaxKeysSlider, 0);
            maxInner.Children.Add(MaxKeysSlider);

            // 创建标签行：标签 + 数值 合并为一个标签显示 "标签: 值"
            var labelRow = new Grid();
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var maxLabel = SharedUIComponents.CreateHeaderLabel("");
            maxLabel.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(maxLabel, 0);
            labelRow.Children.Add(maxLabel);
            // 绑定标签文本为 "本地化标签: 值"
            var maxBinding = new MultiBinding
            {
                StringFormat = "{0}"
            };
            maxBinding.Bindings.Add(new Binding("Value") { Source = new LocalizedStringHelper.LocalizedString(Strings.N2NCMaxKeysTemplate) });
            maxBinding.Bindings.Add(new Binding(nameof(N2NCViewModel.MaxKeys)) { Source = _viewModel });
            maxBinding.Converter = new LabelConverter();
            maxLabel.SetBinding(TextBlock.TextProperty, maxBinding);

            // 主面板：标签行 + 滑条行
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            mainPanel.Children.Add(labelRow);
            mainPanel.Children.Add(maxInner);
            return mainPanel;
        }

        private FrameworkElement CreateMinKeysPanel(Thickness rowMargin)
        {
            var minInner = new Grid();
            minInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            MinKeysSlider = SharedUIComponents.CreateStandardSlider(1, 18, double.NaN, true);
            MinKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.MinKeys)) { Mode = BindingMode.TwoWay, Source = _viewModel });
            MinKeysSlider.SetBinding(RangeBase.MaximumProperty, new Binding(nameof(N2NCViewModel.MinKeysMaximum)) { Mode = BindingMode.OneWay, Source = _viewModel });
            Grid.SetColumn(MinKeysSlider, 0);
            minInner.Children.Add(MinKeysSlider);

            // 创建标签行：标签 + 数值 合并为一个标签显示 "标签: 值"
            var labelRow = new Grid();
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var minLabel = SharedUIComponents.CreateHeaderLabel("");
            minLabel.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(minLabel, 0);
            labelRow.Children.Add(minLabel);
            // 绑定标签文本为 "本地化标签: 值"
            var minBinding = new MultiBinding
            {
                StringFormat = "{0}"
            };
            minBinding.Bindings.Add(new Binding("Value") { Source = new LocalizedStringHelper.LocalizedString(Strings.N2NCMinKeysTemplate) });
            minBinding.Bindings.Add(new Binding(nameof(N2NCViewModel.MinKeys)) { Source = _viewModel });
            minBinding.Converter = new LabelConverter();
            minLabel.SetBinding(TextBlock.TextProperty, minBinding);

            // 主面板：标签行 + 滑条行
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            mainPanel.Children.Add(labelRow);
            mainPanel.Children.Add(minInner);
            return mainPanel;
        }

        private FrameworkElement CreateTransformSpeedPanel(Thickness rowMargin)
        {
            var transformInner = new Grid();
            transformInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // 创建整数档位滑块 (1-8)，每个档位对应一个节拍值
            var transformSlider = SharedUIComponents.CreateStandardSlider(1, 8, double.NaN, true); // 整数档位，启用刻度对齐
            transformSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.TransformSpeedSlot)) { Mode = BindingMode.TwoWay, Source = _viewModel });
            Grid.SetColumn(transformSlider, 0);
            transformInner.Children.Add(transformSlider);

            // 创建标签行：标签 + 数值
            var labelRow = new Grid();
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var transformLabel = SharedUIComponents.CreateHeaderLabel("");
            transformLabel.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(transformLabel, 0);
            labelRow.Children.Add(transformLabel);
            // 绑定标签文本为 "本地化标签: 值"
            var transformBinding = new MultiBinding
            {
                StringFormat = "{0}"
            };
            transformBinding.Bindings.Add(new Binding("Value") { Source = new LocalizedStringHelper.LocalizedString(Strings.N2NCTransformSpeedTemplate) });
            transformBinding.Bindings.Add(new Binding(nameof(N2NCViewModel.TransformSpeedDisplay)) { Source = _viewModel });
            transformBinding.Converter = new LabelConverter();
            transformLabel.SetBinding(TextBlock.TextProperty, transformBinding);

            // 主面板：标签行 + 滑条行
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            mainPanel.Children.Add(labelRow);
            mainPanel.Children.Add(transformInner);
            return mainPanel;
        }

        private FrameworkElement CreateSeedPanel(Thickness rowMargin)
        {
            SeedTextBox = SharedUIComponents.CreateStandardTextBox();
            SeedTextBox.Width = 160;
            SeedTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(N2NCViewModel.Seed)) { Mode = BindingMode.TwoWay, Source = _viewModel });
            GenerateSeedButton = SharedUIComponents.CreateStandardButton(Strings.N2NCGenerateSeedLabel.Localize());
            GenerateSeedButton.Width = 100; // 设置固定宽度以保持按钮大小一致
            GenerateSeedButton.Click += GenerateSeedButton_Click;
            GenerateSeedButton.ToolTip = Strings.N2NCGenerateSeedTooltip.Localize();
            
            // 创建一个Grid来实现右侧对齐的布局
            var seedGrid = new Grid();
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 左侧弹性空间
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // 固定宽度的文本框
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // 10像素间隔
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮自适应宽度
            
            // 设置控件位置
            SeedTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(SeedTextBox, 1);
            Grid.SetColumn(GenerateSeedButton, 3);
            
            seedGrid.Children.Add(SeedTextBox);
            seedGrid.Children.Add(GenerateSeedButton);
            
            return SharedUIComponents.CreateLabeledRow("Seed|种子", seedGrid, rowMargin);
        }

        private FrameworkElement CreateKeySelectionPanel(Thickness rowMargin)
        {
            var keysWrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemHeight = 33 };
            var flagOrder = new[] { KeySelectionFlags.K4, KeySelectionFlags.K5, KeySelectionFlags.K6, KeySelectionFlags.K7, KeySelectionFlags.K8, KeySelectionFlags.K9, KeySelectionFlags.K10, KeySelectionFlags.K10Plus };
            var flagLabels = new Dictionary<KeySelectionFlags, string>
            {
                [KeySelectionFlags.K4] = "4K",
                [KeySelectionFlags.K5] = "5K",
                [KeySelectionFlags.K6] = "6K",
                [KeySelectionFlags.K7] = "7K",
                [KeySelectionFlags.K8] = "8K",
                [KeySelectionFlags.K9] = "9K",
                [KeySelectionFlags.K10] = "10K",
                [KeySelectionFlags.K10Plus] = "10K+"
            };

            foreach (var flag in flagOrder)
            {
                var cb = SharedUIComponents.CreateStandardCheckBox(flagLabels[flag], flagLabels[flag]);
                cb.IsChecked = GetKeySelectionFlag(flag);
                cb.Checked += (_, _) => SetKeySelectionFlag(flag, true);
                cb.Unchecked += (_, _) => SetKeySelectionFlag(flag, false);
                keysWrap.Children.Add(cb);
            }

            // 添加全选/取消全选按钮
            var selectAllButton = SharedUIComponents.CreateStandardButton("Select All|全选");
            selectAllButton.Width = 100; // 设置固定宽度以保持按钮大小一致
            selectAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                {
                    kvp.Value.IsChecked = true;
                    SetKeySelectionFlag(kvp.Key, true);
                }
            };

            var clearAllButton = SharedUIComponents.CreateStandardButton("Clear All|清空");
            clearAllButton.Width = 100; // 设置固定宽度以保持按钮大小一致
            clearAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                {
                    kvp.Value.IsChecked = false;
                    SetKeySelectionFlag(kvp.Key, false);
                }
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            buttonPanel.Children.Add(selectAllButton);
            buttonPanel.Children.Add(clearAllButton);

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(keysWrap);
            mainPanel.Children.Add(buttonPanel);

            // 保存复选框引用以便全选/清空功能使用
            checkboxMap.Clear();
            foreach (var child in keysWrap.Children)
            {
                if (child is CheckBox cb)
                {
                    var flag = flagOrder[checkboxMap.Count];
                    checkboxMap[flag] = cb;
                }
            }

            // 添加监听KeySelection属性变化的处理程序
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(N2NCViewModel.KeySelection))
                {
                    foreach (var kvp in checkboxMap)
                        kvp.Value.IsChecked = GetKeySelectionFlag(kvp.Key);
                }
                // Trigger settings changed event for preview updates
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };

            var filterLabel = Strings.Localize(Strings.FilterLabel);
            void UpdateFilterLabel() => filterLabel = Strings.Localize(Strings.FilterLabel);
            SharedUIComponents.LanguageChanged += UpdateFilterLabel;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateFilterLabel;
            
            return SharedUIComponents.CreateLabeledRow(filterLabel, mainPanel, rowMargin);
        }

        // 添加获取键位选择标志的方法
        private bool GetKeySelectionFlag(KeySelectionFlags flag)
        {
            return (_viewModel.KeySelection & flag) == flag;
        }

        // 添加设置键位选择标志的方法
        private void SetKeySelectionFlag(KeySelectionFlags flag, bool value)
        {
            if (value)
                _viewModel.KeySelection |= flag;
            else
                _viewModel.KeySelection &= ~flag;
            // 直接设置属性，自动触发通知
            var currentSelection = _viewModel.KeySelection;
            _viewModel.KeySelection = currentSelection;
        }


        private FrameworkElement CreatePresetsPanel(Thickness rowMargin)
        {
            // Use shared PresetPanelFactory to manage presets for ConverterOptions
            FrameworkElement panel = PresetPanelFactory.CreatePresetPanel(
                "N2NC",
                () => _viewModel.GetConversionOptions(),
                (opt) =>
                {
                    if (opt == null) return;
                    // Apply options to viewmodel (copy fields)
                    _viewModel.TargetKeys = opt.TargetKeys;
                    _viewModel.TransformSpeed = opt.TransformSpeed;
                    _viewModel.Seed = opt.Seed;
                    if (opt.SelectedKeyFlags.HasValue)
                    {
                        _viewModel.KeySelection = opt.SelectedKeyFlags.Value;
                    }
                    else if (opt.SelectedKeyTypes != null)
                    {
                        // Convert list to flags
                        var flags = KeySelectionFlags.None;
                        foreach (var k in opt.SelectedKeyTypes)
                        {
                            switch (k)
                            {
                                case 4: flags |= KeySelectionFlags.K4; break;
                                case 5: flags |= KeySelectionFlags.K5; break;
                                case 6: flags |= KeySelectionFlags.K6; break;
                                case 7: flags |= KeySelectionFlags.K7; break;
                                case 8: flags |= KeySelectionFlags.K8; break;
                                case 9: flags |= KeySelectionFlags.K9; break;
                                case 10: flags |= KeySelectionFlags.K10; break;
                                default: flags |= KeySelectionFlags.K10Plus; break;
                            }
                        }
                        _viewModel.KeySelection = flags;
                    }
                }
            );

            return SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel, panel, rowMargin);
        }

        private void SetupEventHandlers()
        {
            // 可以在这里添加事件处理逻辑
        }

        private void TargetKeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 保持数值状态同步并确保最大/最小滑块有效
            int newTarget = Math.Max(1, (int)Math.Round(e.NewValue));
            try
            {
                if (Math.Abs(_viewModel.TargetKeys - newTarget) > 0)
                    _viewModel.TargetKeys = newTarget;

                // 确保MaxKeys至少等于TargetKeys
                if (MaxKeysSlider != null)
                {
                    if (MaxKeysSlider.Value < newTarget)
                        MaxKeysSlider.Value = newTarget;
                }

                // 确保MinKeys最大值最多为当前MaxKeys值
                if (MinKeysSlider != null && MaxKeysSlider != null)
                {
                    MinKeysSlider.Maximum = MaxKeysSlider.Value;
                    // 限制MinKeys到允许范围
                    if (_viewModel.MinKeys > MinKeysSlider.Maximum)
                        _viewModel.MinKeys = (int)MinKeysSlider.Maximum;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TargetKeysSlider_ValueChanged 错误: {ex.Message}");
            }
        }

        private void MaxKeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // 当Max更改时，确保Min的最大值反映它并限制
                if (MinKeysSlider != null)
                {
                    MinKeysSlider.Maximum = e.NewValue;
                    if (_viewModel.MinKeys > MinKeysSlider.Maximum)
                        _viewModel.MinKeys = (int)MinKeysSlider.Maximum;
                }

                // 如果Max降到Target以下，则减少Target以适应
                if (TargetKeysSlider != null && TargetKeysSlider.Value > e.NewValue)
                {
                    TargetKeysSlider.Value = e.NewValue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MaxKeysSlider_ValueChanged 错误: {ex.Message}");
            }
        }

        private void GenerateSeedButton_Click(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            int newSeed = random.Next();
            // 更新ViewModel和绑定的TextBox（双向绑定将保持它们同步）
            _viewModel.Seed = newSeed;
            if (SeedTextBox != null) SeedTextBox.Text = newSeed.ToString();
        }

        // 添加处理单个文件的方法：返回生成的 .osz 路径（成功）或 null（失败）
        public Beatmap? ProcessSingleFile(string filePath)
        {
            return N2NCService.ProcessSingleFile(filePath, _viewModel.GetConversionOptions());
        }

        public string GetOutputFileName(string inputPath, ManiaBeatmap beatmap)
        {
            return beatmap.GetOsuFileName() + ".osu";
        }
    }
}
