using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.DPtool
{
    public class DPToolView : ToolViewBase<DPToolOptions>
    {
        private readonly DPToolViewModel _viewModel;

        public event EventHandler? SettingsChanged;

        public DPToolView() : base(ConverterEnum.DP)
        {
            _viewModel = new DPToolViewModel(Options);
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            // OD设置 - 带勾选的滑条
            var changeKeyPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ModifyKeys);
            
            // 创建模板化控件，但保持自定义布局
       
            var lMirrorCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.LMirror);
            var lDensityCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.LDensity);
            var lMaxKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LMaxKeys);
            var lMinKeysSlider = SettingsBinder.CreateTemplatedSliderWithDynamicMax(_viewModel.Options, o => o.LMinKeys, 
                _viewModel, nameof(_viewModel.LMinKeysMaximum));

            var rMirrorCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.RMirror);
            var rDensityCheckBox = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.RDensity);
            var rMaxKeysSlider = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.RMaxKeys);
            var rMinKeysSlider = SettingsBinder.CreateTemplatedSliderWithDynamicMax(_viewModel.Options, o => o.RMinKeys, 
                _viewModel, nameof(_viewModel.RMinKeysMaximum));

            // Placeholder keys panel
            // var keysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            // var keysExpl = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            // keysExpl.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Strings.DPKeysTooltip.GetLocalizedString() });
            // keysPanel.Children.Add(keysExpl);

            // Left
            var leftLabel = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            leftLabel.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = Strings.DPLeftLabel.GetLocalizedString() });
            lMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            lDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var leftChecks = new StackPanel
            {
                Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            leftChecks.Children.Add(lMirrorCheckBox);
            leftChecks.Children.Add(lDensityCheckBox);
    

            var leftPanel = new StackPanel
                { Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            leftPanel.Children.Add(leftLabel);
            leftPanel.Children.Add(leftChecks);
            leftPanel.Children.Add(lMaxKeysSlider);
            leftPanel.Children.Add(lMinKeysSlider);

            // Right
            var rightLabel = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            rightLabel.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = Strings.DPRightLabel.GetLocalizedString() });
            rMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            rDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var rightChecks = new StackPanel
            {
                Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            rightChecks.Children.Add(rMirrorCheckBox);
            rightChecks.Children.Add(rDensityCheckBox);

            var rightPanel = new StackPanel
                { Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            rightPanel.Children.Add(rightLabel);
            rightPanel.Children.Add(rightChecks);
            rightPanel.Children.Add(rMaxKeysSlider);
            rightPanel.Children.Add(rMinKeysSlider);

            // Separator and grid
            var separator = new Border
            {
                Background = Brushes.DarkSlateGray, Width = 2, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false
            };
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

            var presetInner = PresetPanelFactory.CreatePresetPanel(nameof(ConverterEnum.DP), () => _viewModel.Options,
                (opt) =>
                {
                    if (opt == null) return;
                    var target = _viewModel.Options;

                    target.LMirror.Value = opt.LMirror.Value;
                    target.LDensity.Value = opt.LDensity.Value;
                    target.LMaxKeys.Value = opt.LMaxKeys.Value;
                    target.LMinKeys.Value = opt.LMinKeys.Value;
                    target.LRemove.Value = opt.LRemove.Value;

                    target.RMirror.Value = opt.RMirror.Value;
                    target.RDensity.Value = opt.RDensity.Value;
                    target.RMaxKeys.Value = opt.RMaxKeys.Value;
                    target.RMinKeys.Value = opt.RMinKeys.Value;
                    target.RRemove.Value = opt.RRemove.Value;
                });

            var presetsPanel =
                SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel, presetInner, new Thickness(0, 0, 0, 10));

            // Root stack
            var stackPanel = new StackPanel
            {
                Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
         
            stackPanel.Children.Add(changeKeyPanel);
            stackPanel.Children.Add(grid);
            stackPanel.Children.Add(presetsPanel);

            // Assign the built UI to the control's content
            Content = stackPanel;
        }

        protected void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}