using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.DPtool
{
    public class DPToolControl : ToolControlBase<DPToolOptions>
    {
        public event EventHandler? SettingsChanged;

        private readonly DPToolViewModel _viewModel;

        // UI控件引用，用于属性变更时的启用/禁用逻辑
        private UIElement? _keysSlider;
        private UIElement? _lMaxKeysSlider;
        private UIElement? _lMinKeysSlider;
        private UIElement? _rMaxKeysSlider;
        private UIElement? _rMinKeysSlider;

        public DPToolControl() : base(ConverterEnum.DP)
        {
            _viewModel = new DPToolViewModel(Options);
            DataContext = _viewModel;

            BuildTemplatedUI();

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) =>
            {
                SharedUIComponents.LanguageChanged -= OnLanguageChanged;
                DPToolWindow_Closed();
            };
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dc = DataContext;
                Content = null;
                BuildTemplatedUI();
                DataContext = dc;
            }));
        }

        private void DPToolWindow_Closed()
        {
            // Options are automatically saved by ViewModel auto-save functionality
            // No manual saving needed
        }

        private void BuildTemplatedUI()
        {
            // 创建模板化控件，但保持自定义布局
            var modifyKeysCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.ModifySingleSideKeyCount);
            _keysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.SingleSideKeyCount);

            var lMirrorCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.LMirror);
            var lDensityCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.LDensity);
            var lRemoveCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.LRemove);
            _lMaxKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LMaxKeys);
            _lMinKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LMinKeys);

            var rMirrorCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.RMirror);
            var rDensityCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.RDensity);
            var rRemoveCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.RRemove);
            _rMaxKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.RMaxKeys);
            _rMinKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.RMinKeys);

            // Top: Modify keys + keys slider
            modifyKeysCheckBox.Margin = new Thickness(0, 0, 0, 6);

            var modifyKeysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15), HorizontalAlignment = HorizontalAlignment.Stretch };
            modifyKeysPanel.Children.Add(modifyKeysCheckBox);
            modifyKeysPanel.Children.Add(_keysSlider);

            // Placeholder keys panel
            var keysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var keysExpl = SharedUIComponents.CreateHeaderLabel(Strings.DPKeysTooltip);
            keysPanel.Children.Add(keysExpl);

            // Left/Right panels
            // Left
            var leftLabel = SharedUIComponents.CreateHeaderLabel(Strings.DPLeftLabel);
            lMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            lDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            lRemoveCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var leftChecks = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            leftChecks.Children.Add(lMirrorCheckBox);
            leftChecks.Children.Add(lDensityCheckBox);
            leftChecks.Children.Add(lRemoveCheckBox);

            var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            leftPanel.Children.Add(leftLabel);
            leftPanel.Children.Add(leftChecks);
            leftPanel.Children.Add(_lMaxKeysSlider);
            leftPanel.Children.Add(_lMinKeysSlider);

            // Right
            var rightLabel = SharedUIComponents.CreateHeaderLabel(Strings.DPRightLabel);
            rMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            rDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            rRemoveCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var rightChecks = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            rightChecks.Children.Add(rMirrorCheckBox);
            rightChecks.Children.Add(rDensityCheckBox);
            rightChecks.Children.Add(rRemoveCheckBox);

            var rightPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            rightPanel.Children.Add(rightLabel);
            rightPanel.Children.Add(rightChecks);
            rightPanel.Children.Add(_rMaxKeysSlider);
            rightPanel.Children.Add(_rMinKeysSlider);

            // Separator and grid
            var separator = new Border { Background = Brushes.DarkSlateGray, Width = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false };
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(separator, 1);
            Grid.SetColumn(rightPanel, 2);

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(leftPanel);
            grid.Children.Add(separator);
            grid.Children.Add(rightPanel);

            var presetInner = PresetPanelFactory.CreatePresetPanel(nameof(ConverterEnum.DP), () => _viewModel.Options, (opt) =>
            {
                if (opt == null) return;
                var target = _viewModel.Options;
                target.ModifySingleSideKeyCount = opt.ModifySingleSideKeyCount;
                target.SingleSideKeyCount = opt.SingleSideKeyCount;

                target.LMirror = opt.LMirror;
                target.LDensity = opt.LDensity;
                target.LMaxKeys = opt.LMaxKeys;
                target.LMinKeys = opt.LMinKeys;
                target.LRemove = opt.LRemove;

                target.RMirror = opt.RMirror;
                target.RDensity = opt.RDensity;
                target.RMaxKeys = opt.RMaxKeys;
                target.RMinKeys = opt.RMinKeys;
                target.RRemove = opt.RRemove;
            });

            var presetsPanel = SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel.Localize(), presetInner, new Thickness(0, 0, 0, 10));

            // Root stack
            var stackPanel = new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            stackPanel.Children.Add(modifyKeysPanel);
            stackPanel.Children.Add(keysPanel);
            stackPanel.Children.Add(grid);
            stackPanel.Children.Add(presetsPanel);

            // Assign the built UI to the control's content
            Content = stackPanel;

            // Setup property change notifications for preview updates and slider enabling
            if (_viewModel.Options is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (_, e) =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(_viewModel.Options.ModifySingleSideKeyCount):
                            if (_keysSlider != null) _keysSlider.IsEnabled = _viewModel.Options.ModifySingleSideKeyCount;
                            break;
                        case nameof(_viewModel.Options.LDensity):
                            if (_lMaxKeysSlider != null) _lMaxKeysSlider.IsEnabled = _viewModel.Options.LDensity;
                            if (_lMinKeysSlider != null) _lMinKeysSlider.IsEnabled = _viewModel.Options.LDensity;
                            break;
                        case nameof(_viewModel.Options.RDensity):
                            if (_rMaxKeysSlider != null) _rMaxKeysSlider.IsEnabled = _viewModel.Options.RDensity;
                            if (_rMinKeysSlider != null) _rMinKeysSlider.IsEnabled = _viewModel.Options.RDensity;
                            break;
                    }
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                };
            }

            // Set initial enabled state
            if (_keysSlider != null) _keysSlider.IsEnabled = _viewModel.Options.ModifySingleSideKeyCount;
            if (_lMaxKeysSlider != null) _lMaxKeysSlider.IsEnabled = _viewModel.Options.LDensity;
            if (_lMinKeysSlider != null) _lMinKeysSlider.IsEnabled = _viewModel.Options.LDensity;
            if (_rMaxKeysSlider != null) _rMaxKeysSlider.IsEnabled = _viewModel.Options.RDensity;
            if (_rMinKeysSlider != null) _rMinKeysSlider.IsEnabled = _viewModel.Options.RDensity;
        }
    }
}