using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.DPtool
{
    public class DPToolView : ToolViewBase<DPToolOptions>
    {
        private readonly DPToolViewModel _viewModel;

        // UI控件引用，用于属性变更时的启用/禁用逻辑
        private UIElement? _keysSlider;
        private UIElement? _lMaxKeysSlider;
        private UIElement? _lMinKeysSlider;
        private UIElement? _rMaxKeysSlider;
        private UIElement? _rMinKeysSlider;

        public event EventHandler? SettingsChanged;
        
        public DPToolView() : base(ConverterEnum.DP)
        {
            _viewModel = new DPToolViewModel(Options);
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            // 创建模板化控件，但保持自定义布局
            // var modifyKeysCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.ModifySingleSideKeyCount);
            _keysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.SingleSideKeyCount, o => o.ModifySingleSideKeyCount);

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
            // modifyKeysCheckBox.Margin = new Thickness(0, 0, 0, 6);

            var modifyKeysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15), HorizontalAlignment = HorizontalAlignment.Stretch };
            // modifyKeysPanel.Children.Add(modifyKeysCheckBox);
            modifyKeysPanel.Children.Add(_keysSlider);

            // Placeholder keys panel
            var keysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var keysExpl = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            keysExpl.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.DPKeysTooltip.GetLocalizedString() });
            keysPanel.Children.Add(keysExpl);

            // Left/Right panels
            // Left
            var leftLabel = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            leftLabel.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.DPLeftLabel.GetLocalizedString() });
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
            var rightLabel = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            rightLabel.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.DPRightLabel.GetLocalizedString() });
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

            var presetsPanel = SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel, presetInner, new Thickness(0, 0, 0, 10));

            // Root stack
            var stackPanel = new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            stackPanel.Children.Add(modifyKeysPanel);
            stackPanel.Children.Add(keysPanel);
            stackPanel.Children.Add(grid);
            stackPanel.Children.Add(presetsPanel);

            // Assign the built UI to the control's content
            Content = stackPanel;
        }

        protected virtual void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}