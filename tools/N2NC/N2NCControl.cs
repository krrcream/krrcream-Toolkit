using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.N2NC
{
    public class N2NCControl : UserControl
    {
        private Slider? TargetKeysSlider;
        private Slider? MaxKeysSlider;
        private Slider? MinKeysSlider;
        private TextBox? SeedTextBox;
        private Button? GenerateSeedButton;
        private readonly Dictionary<KeySelectionFlags, CheckBox> checkboxMap = new();
        private readonly N2NCViewModel _viewModel = new();


        public N2NCControl()
        {
            // Initialize view and bindings
            BuildConverterUI();
            this.DataContext = _viewModel;
            // Subscribe to language change to rebuild UI on demand
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // Unsubscribe when this control is unloaded
            this.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            try
            {
                // Rebuild UI on UI thread to refresh labels/tooltips
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var dm = this.DataContext;
                        this.Content = null;
                        BuildConverterUI();
                        this.DataContext = dm;
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"N2NCWindow inner rebuild failed: {ex.Message}"); }
                }));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"N2NCWindow OnLanguageChanged failed: {ex.Message}"); }
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
            this.Content = scrollViewer;

            // 事件处理
            SetupEventHandlers();
        }

        private ScrollViewer CreateScrollViewer()
        {
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private Grid CreateMainGrid()
        {
            var grid = new Grid { Margin = new Thickness(0) };
            for (int i = 0; i < 8; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return grid;
        }

        private FrameworkElement CreateTargetKeysPanel(Thickness rowMargin)
        {
            // Target keys
            var label = Strings.Localize(Strings.N2NCTargetKeysLabel);
            void UpdateTargetKeysLabel() => label = Strings.Localize(Strings.N2NCTargetKeysLabel);
            
            var targetKeysSlider = SharedUIComponents.CreateStandardSlider(1, 15, 30, true);
            targetKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding("TargetKeys") { Source = _viewModel });
            
            var targetKeysText = SharedUIComponents.CreateStandardTextBlock();
            targetKeysText.SetBinding(TextBlock.TextProperty, new Binding("TargetKeys") { Source = _viewModel, StringFormat = "{0}" });
            
            var targetKeysPanel = new StackPanel { Orientation = Orientation.Horizontal };
            targetKeysPanel.Children.Add(targetKeysText);
            
            var targetKeysRow = SharedUIComponents.CreateLabeledRow(label, targetKeysPanel, rowMargin);
            SharedUIComponents.LanguageChanged += UpdateTargetKeysLabel;
            this.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateTargetKeysLabel;

            return targetKeysRow;
        }

        private FrameworkElement CreateMaxKeysPanel(Thickness rowMargin)
        {
            var maxInner = new Grid();
            maxInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            maxInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            MaxKeysSlider = SharedUIComponents.CreateStandardSlider(1, 18, 24, true);
            MaxKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.MaxKeys)) { Mode = BindingMode.TwoWay });
            MaxKeysSlider.ValueChanged += MaxKeysSlider_ValueChanged;
            Grid.SetColumn(MaxKeysSlider, 0);
            maxInner.Children.Add(MaxKeysSlider);
            var maxValue = SharedUIComponents.CreateStandardTextBlock();
            maxValue.SetBinding(TextBlock.TextProperty, new Binding(nameof(N2NCViewModel.MaxKeys)) { StringFormat = "{0:0}" });
            Grid.SetColumn(maxValue, 1);
            maxInner.Children.Add(maxValue);
            var label = Strings.N2NCMaxKeysLabel.Localize();
            return SharedUIComponents.CreateLabeledRow(label, maxInner, rowMargin);
        }

        private FrameworkElement CreateMinKeysPanel(Thickness rowMargin)
        {
            var minInner = new Grid();
            minInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            minInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            MinKeysSlider = SharedUIComponents.CreateStandardSlider(1, 18, 18, true);
            MinKeysSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.MinKeys)) { Mode = BindingMode.TwoWay });
            Grid.SetColumn(MinKeysSlider, 0);
            minInner.Children.Add(MinKeysSlider);
            var minValue = SharedUIComponents.CreateStandardTextBlock();
            minValue.SetBinding(TextBlock.TextProperty, new Binding(nameof(N2NCViewModel.MinKeys)) { StringFormat = "{0:0}" });
            Grid.SetColumn(minValue, 1);
            minInner.Children.Add(minValue);
            var label = Strings.N2NCMinKeysLabel.Localize();
            return SharedUIComponents.CreateLabeledRow(label, minInner, rowMargin);
        }

        private FrameworkElement CreateTransformSpeedPanel(Thickness rowMargin)
        {
            var transformInner = new Grid();
            transformInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            transformInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // 创建整数档位滑块 (1-8)，每个档位对应一个节拍值
            var transformSlider = SharedUIComponents.CreateStandardSlider(1, 8, 1, true); // 整数档位，启用刻度对齐
            transformSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(N2NCViewModel.TransformSpeedSlot)) { Mode = BindingMode.TwoWay });
            Grid.SetColumn(transformSlider, 0);
            transformInner.Children.Add(transformSlider);
            var transformDisplay = SharedUIComponents.CreateStandardTextBlock();
            transformDisplay.SetBinding(TextBlock.TextProperty, new Binding(nameof(N2NCViewModel.TransformSpeedDisplay)));
            Grid.SetColumn(transformDisplay, 1);
            transformInner.Children.Add(transformDisplay);
            var label = Strings.N2NCTransformSpeedLabel.Localize();
            return SharedUIComponents.CreateLabeledRow(label, transformInner, rowMargin);
        }

        private FrameworkElement CreateSeedPanel(Thickness rowMargin)
        {
            SeedTextBox = SharedUIComponents.CreateStandardTextBox();
            SeedTextBox.Width = 160;
            SeedTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(N2NCViewModel.Seed)) { Mode = BindingMode.TwoWay });
            GenerateSeedButton = SharedUIComponents.CreateStandardButton(Strings.N2NCGenerateSeedLabel.Localize());
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
            
            return SharedUIComponents.CreateLabeledRow("种子:", seedGrid, rowMargin);
        }

        private FrameworkElement CreateKeySelectionPanel(Thickness rowMargin)
        {
            var keysWrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemHeight = 28 };
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
                var cb = SharedUIComponents.CreateStandardCheckBoxWithTooltip(flagLabels[flag], flagLabels[flag]);
                cb.IsChecked = GetKeySelectionFlag(flag);
                cb.Checked += (_, _) => SetKeySelectionFlag(flag, true);
                cb.Unchecked += (_, _) => SetKeySelectionFlag(flag, false);
                keysWrap.Children.Add(cb);
            }

            // 添加全选/取消全选按钮
            var selectAllButton = SharedUIComponents.CreateStandardButton("全选");
            selectAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                    kvp.Value.IsChecked = true;
            };

            var clearAllButton = SharedUIComponents.CreateStandardButton("清空");
            clearAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                    kvp.Value.IsChecked = false;
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
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
            };

            var filterLabel = Strings.Localize(Strings.FilterLabel);
            void UpdateFilterLabel() => filterLabel = Strings.Localize(Strings.FilterLabel);
            SharedUIComponents.LanguageChanged += UpdateFilterLabel;
            this.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateFilterLabel;
            
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
                OptionsConstants.ConverterToolName,
                () => _viewModel.GetConversionOptions(),
                (opt) =>
                {
                    if (opt == null) return;
                    // Apply options to viewmodel (copy fields)
                    _viewModel.TargetKeys = opt.TargetKeys;
                    _viewModel.MaxKeys = opt.MaxKeys;
                    _viewModel.MinKeys = opt.MinKeys;
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

        // 添加获取预设显示名称的方法
        private string GetPresetDisplayName(PresetKind preset)
        {
            return SharedUIComponents.GetLocalizedEnumDisplayName(preset);
        }

        private void ApplyPreset(PresetKind preset)
        {
            switch (preset)
            {
                case PresetKind.Default:
                    _viewModel.TargetKeys = 4;
                    _viewModel.MaxKeys = 8;
                    _viewModel.MinKeys = 4;
                    _viewModel.TransformSpeed = 1.0;
                    _viewModel.Seed = 0;
                    _viewModel.KeySelection = KeySelectionFlags.K4 | KeySelectionFlags.K5 | KeySelectionFlags.K6 | KeySelectionFlags.K7 | KeySelectionFlags.K8;
                    break;
                case PresetKind.TenK:
                    _viewModel.TargetKeys = 6;
                    _viewModel.MaxKeys = 10;
                    _viewModel.MinKeys = 6;
                    _viewModel.TransformSpeed = 2.0;
                    _viewModel.Seed = 0;
                    _viewModel.KeySelection = KeySelectionFlags.K6 | KeySelectionFlags.K7 | KeySelectionFlags.K8 | KeySelectionFlags.K9 | KeySelectionFlags.K10;
                    break;
                case PresetKind.EightK:
                    _viewModel.TargetKeys = 4;
                    _viewModel.MaxKeys = 7;
                    _viewModel.MinKeys = 4;
                    _viewModel.TransformSpeed = 0.5;
                    _viewModel.Seed = 0;
                    _viewModel.KeySelection = KeySelectionFlags.K4 | KeySelectionFlags.K5 | KeySelectionFlags.K6 | KeySelectionFlags.K7;
                    break;
                case PresetKind.SevenK:
                    _viewModel.TargetKeys = 8;
                    _viewModel.MaxKeys = 10;
                    _viewModel.MinKeys = 8;
                    _viewModel.TransformSpeed = 1.5;
                    _viewModel.Seed = 0;
                    _viewModel.KeySelection = KeySelectionFlags.K8 | KeySelectionFlags.K9 | KeySelectionFlags.K10 | KeySelectionFlags.K10Plus;
                    break;
            }
        }

        // 保存当前选项
        private void ConverterWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                var opt = _viewModel.GetConversionOptions();
                opt.Validate();
                OptionsService.SaveOptions(OptionsConstants.ConverterToolName, OptionsConstants.OptionsFileName, opt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save converter options: {ex.Message}");
            }
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
        public string? ProcessSingleFile(string filePath, bool openOsz = false)
        {
            return N2NCService.ProcessSingleFile(filePath, _viewModel.GetConversionOptions(), openOsz);
        }
    }
}
