using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.N2NC
{
    public class N2NCView : ToolViewBase<N2NCOptions>
    {
        private readonly N2NCViewModel _viewModel;
        private readonly Dictionary<KeySelectionFlags, CheckBox> checkboxMap = new();
        private FrameworkElement seedPanel => SettingsBinder.CreateSeedPanel(_viewModel, x => x.Seed);

        private UIElement? TargetKeysSlider;
        private UIElement? MaxKeysSlider;
        private UIElement? MinKeysSlider;
        private UIElement? TransformSpeedSlider;

        public event EventHandler? SettingsChanged;

        public N2NCView() : base(ConverterEnum.N2NC)
        {
            _viewModel = new N2NCViewModel(Options);
            DataContext = _viewModel;
            BuildTemplatedUI();
        }

        private void BuildTemplatedUI()
        {
            var scrollViewer = CreateScrollViewer();

            var grid = new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };

            var rowMargin = new Thickness(0, 6, 0, 6);

            // 创建模板化控件 - 绑定到Options的Bindable属性
            TargetKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.TargetKeys);
            MaxKeysSlider = SettingsBinder.CreateTemplatedSliderWithDynamicMax(_viewModel.Options, o => o.MaxKeys, 
                _viewModel, nameof(_viewModel.MaxKeysMaximum));
            MinKeysSlider = SettingsBinder.CreateTemplatedSliderWithDynamicMax(_viewModel.Options, o => o.MinKeys, 
                _viewModel, nameof(_viewModel.MinKeysMaximum));
            TransformSpeedSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.TransformSpeed, null,
                N2NCViewModel.TransformSpeedSlotDict);

            grid.Children.Add(TargetKeysSlider);
            grid.Children.Add(MaxKeysSlider);
            grid.Children.Add(MinKeysSlider);
            grid.Children.Add(TransformSpeedSlider);

            var seedGrid = new Grid();
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            seedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(seedPanel);

            // Key selection panel
            var keysWrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemHeight = 33 };
            var flagOrder = new[]
            {
                KeySelectionFlags.K4, KeySelectionFlags.K5, KeySelectionFlags.K6, KeySelectionFlags.K7,
                KeySelectionFlags.K8, KeySelectionFlags.K9, KeySelectionFlags.K10, KeySelectionFlags.K10Plus
            };
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

            var selectAllButton = SharedUIComponents.CreateStandardButton("Select All|全选");
            selectAllButton.Width = 100;
            selectAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                {
                    kvp.Value.IsChecked = true;
                    SetKeySelectionFlag(kvp.Key, true);
                }
            };

            var clearAllButton = SharedUIComponents.CreateStandardButton("Clear All|清空");
            clearAllButton.Width = 100;
            clearAllButton.Click += (_, _) =>
            {
                foreach (var kvp in checkboxMap)
                {
                    kvp.Value.IsChecked = false;
                    SetKeySelectionFlag(kvp.Key, false);
                }
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            buttonPanel.Children.Add(selectAllButton);
            buttonPanel.Children.Add(clearAllButton);

            var keysMainPanel = new StackPanel();
            keysMainPanel.Children.Add(keysWrap);
            keysMainPanel.Children.Add(buttonPanel);

            checkboxMap.Clear();
            foreach (var child in keysWrap.Children)
                if (child is CheckBox cb)
                {
                    var flag = flagOrder[checkboxMap.Count];
                    checkboxMap[flag] = cb;
                }

            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(N2NCViewModel.KeySelection))
                    foreach (var kvp in checkboxMap)
                        kvp.Value.IsChecked = GetKeySelectionFlag(kvp.Key);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };

            var filterLabel = Strings.Localize(Strings.FilterLabel);

            void UpdateFilterLabel()
            {
                filterLabel = Strings.Localize(Strings.FilterLabel);
            }

            SharedUIComponents.LanguageChanged += UpdateFilterLabel;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateFilterLabel;

            var keysPanel = SharedUIComponents.CreateLabeledRow(filterLabel, keysMainPanel, rowMargin);
            grid.Children.Add(keysPanel);

            // Presets panel
            var presetsBorder = PresetPanelFactory.CreatePresetPanel(
                "N2NC",
                () => _viewModel.GetConversionOptions(),
                (opt) =>
                {
                    if (opt == null) return;
                    _viewModel.TargetKeys = opt.TargetKeys.Value;
                    _viewModel.TransformSpeed = opt.TransformSpeed.Value;
                    _viewModel.Seed = opt.Seed;
                    if (opt.SelectedKeyFlags.HasValue)
                    {
                        _viewModel.KeySelection = opt.SelectedKeyFlags.Value;
                    }
                    else if (opt.SelectedKeyTypes != null)
                    {
                        var flags = KeySelectionFlags.None;
                        foreach (var k in opt.SelectedKeyTypes)
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

                        _viewModel.KeySelection = flags;
                    }
                }
            );

            var presetsPanel = SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel, presetsBorder, rowMargin);
            grid.Children.Add(presetsPanel);

            scrollViewer.Content = grid;
            Content = scrollViewer;
        }

        private ScrollViewer CreateScrollViewer()
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
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



        // private void GenerateSeedButton_Click(object sender, RoutedEventArgs e)
        // {
        //     Random random = new Random();
        //     int newSeed = random.Next();
        //     // 更新ViewModel和绑定的TextBox（双向绑定将保持它们同步）
        //     _viewModel.Seed = newSeed;
        //     if (SeedTextBox != null) SeedTextBox.Text = newSeed.ToString();
        // }
    }
}