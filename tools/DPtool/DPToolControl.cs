using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Tools.Shared;
using krrTools.tools.Shared;

namespace krrTools.tools.DPtool
{
    public class DPToolControl : UserControl
    {
        private enum DPOptionKey
        {
            SingleSideKeyCount,
            ModifySingleSideKeyCount,
            LMirror,
            LDensity,
            LMaxKeys,
            LMinKeys,
            RMirror,
            RDensity,
            RMaxKeys,
            RMinKeys
        }

        private CheckBox? ModifyKeysCheckBox, LMirrorCheckBox, LDensityCheckBox, RMirrorCheckBox, RDensityCheckBox;
        private Slider? KeysSlider, LMaxKeysSlider, LMinKeysSlider, RMaxKeysSlider, RMinKeysSlider;

        private readonly DPToolViewModel _viewModel = new();
        private IEnumSettingsProvider? _enumProvider;

        public DPToolControl()
        {
            DataContext = _viewModel;
            try
            {
                BuildDPToolUI();
                SetupBindings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DPToolControl initialization error: {ex.Message}");
                Content = new TextBlock
                {
                    Text = (SharedUIComponents.IsChineseLanguage() ? "DP Tool 加载失败: " : "DP Tool failed to load: ") + ex.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                return;
            }
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
                    BuildDPToolUI();
                    SetupBindings();
                    DataContext = dc;
            }));
        }

        private void DPToolWindow_Closed()
        {
            try
            {
                OptionsManager.SaveOptions(OptionsManager.DPToolName, OptionsManager.OptionsFileName, _viewModel.Options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save DPTool options: {ex.Message}");
            }
        }

        private void BuildDPToolUI()
        {
            var enumProvider = new EnumSettingsProviderDelegate(
                getter: key =>
                {
                    try { return _viewModel.Options.GetType().GetProperty(key.ToString())?.GetValue(_viewModel.Options); }
                    catch (Exception ex) { Debug.WriteLine($"EnumProvider getter error: {ex.Message}"); return null; }
                },
                setter: (key, value) =>
                {
                    try
                    {
                        var prop = _viewModel.Options.GetType().GetProperty(key.ToString());
                        if (prop != null)
                        {
                            var converted = value == null ? null : Convert.ChangeType(value, prop.PropertyType);
                            prop.SetValue(_viewModel.Options, converted);
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"EnumProvider setter error: {ex.Message}"); }
                }
            );
            // store provider for SetupBindings
            _enumProvider = enumProvider;

            // Top: Modify keys + keys slider
            ModifyKeysCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.DPModifyKeysCheckbox, Strings.DPModifyKeysTooltip);
            ModifyKeysCheckBox.Margin = new Thickness(0, 0, 0, 6);

            var keysSettings = new SettingsSlider<double>
            {
                LabelText = Strings.DPKeysLabel,
                TooltipText = Strings.DPKeysTooltip,
                EnumProvider = enumProvider,
                EnumKey = DPOptionKey.SingleSideKeyCount,
                Min = 1,
                Max = 10,
                TickFrequency = 1,
                KeyboardStep = 1
            };
            KeysSlider = keysSettings.InnerSlider;

            var modifyKeysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15), HorizontalAlignment = HorizontalAlignment.Stretch };
            modifyKeysPanel.Children.Add(ModifyKeysCheckBox);
            modifyKeysPanel.Children.Add(keysSettings);

            // Placeholder keys panel
            var keysPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var keysExpl = SharedUIComponents.CreateHeaderLabel(Strings.DPKeysTooltip);
            keysPanel.Children.Add(keysExpl);

            // Left/Right panels
            // Left
            var leftLabel = SharedUIComponents.CreateHeaderLabel(Strings.DPLeftLabel);
            LMirrorCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.DPMirrorLabel, Strings.DPMirrorTooltipLeft);
            LMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            LDensityCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.DPDensityLabel, Strings.DPDensityTooltipLeft);
            LDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var leftChecks = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            leftChecks.Children.Add(LMirrorCheckBox);
            leftChecks.Children.Add(LDensityCheckBox);

            var lMaxSettings = new SettingsSlider<double>
            {
                LabelText = Strings.DPLeftMaxKeysLabel,
                TooltipText = Strings.DPLeftMaxKeysLabel,
                EnumProvider = enumProvider,
                EnumKey = DPOptionKey.LMaxKeys,
                Min = 0,
                Max = 5,
                TickFrequency = 1,
                KeyboardStep = 1
            };

            var lMinSettings = new SettingsSlider<double>
            {
                LabelText = Strings.DPLeftMinKeysLabel,
                TooltipText = Strings.DPLeftMinKeysLabel,
                EnumProvider = enumProvider,
                EnumKey = DPOptionKey.LMinKeys,
                Min = 0,
                Max = 5,
                TickFrequency = 1,
                KeyboardStep = 1
            };

            LMaxKeysSlider = lMaxSettings.InnerSlider;
            LMinKeysSlider = lMinSettings.InnerSlider;

            var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            leftPanel.Children.Add(leftLabel);
            leftPanel.Children.Add(leftChecks);
            leftPanel.Children.Add(lMaxSettings);
            leftPanel.Children.Add(lMinSettings);

            // Right
            var rightLabel = SharedUIComponents.CreateHeaderLabel(Strings.DPRightLabel);
            RMirrorCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.DPMirrorLabel, Strings.DPMirrorTooltipRight);
            RMirrorCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            RDensityCheckBox = SharedUIComponents.CreateStandardCheckBox(Strings.DPDensityLabel, Strings.DPDensityTooltipRight);
            RDensityCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            var rightChecks = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            rightChecks.Children.Add(RMirrorCheckBox);
            rightChecks.Children.Add(RDensityCheckBox);

            var rMaxSettings = new SettingsSlider<double>
            {
                LabelText = Strings.DPRightMaxKeysLabel,
                TooltipText = Strings.DPRightMaxKeysLabel,
                EnumProvider = enumProvider,
                EnumKey = DPOptionKey.RMaxKeys,
                Min = 0,
                Max = 5,
                TickFrequency = 1,
                KeyboardStep = 1
            };

            var rMinSettings = new SettingsSlider<double>
            {
                LabelText = Strings.DPRightMinKeysLabel,
                TooltipText = Strings.DPRightMinKeysLabel,
                EnumProvider = enumProvider,
                EnumKey = DPOptionKey.RMinKeys,
                Min = 0,
                Max = 5,
                TickFrequency = 1,
                KeyboardStep = 1
            };

            RMaxKeysSlider = rMaxSettings.InnerSlider;
            RMinKeysSlider = rMinSettings.InnerSlider;

            var rightPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            rightPanel.Children.Add(rightLabel);
            rightPanel.Children.Add(rightChecks);
            rightPanel.Children.Add(rMaxSettings);
            rightPanel.Children.Add(rMinSettings);

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

            // Presets panel (preserve existing factory usage)
            var presetInner = PresetPanelFactory.CreatePresetPanel(OptionsManager.DPToolName, () => _viewModel.Options, (opt) =>
            {
                if (opt == null) return;
                var target = _viewModel.Options;
                target.ModifySingleSideKeyCount = opt.ModifySingleSideKeyCount;
                target.SingleSideKeyCount = opt.SingleSideKeyCount;

                target.Left.Mirror = opt.Left.Mirror;
                target.Left.Density = opt.Left.Density;
                target.Left.MaxKeys = opt.Left.MaxKeys;
                target.Left.MinKeys = opt.Left.MinKeys;

                target.Right.Mirror = opt.Right.Mirror;
                target.Right.Density = opt.Right.Density;
                target.Right.MaxKeys = opt.Right.MaxKeys;
                target.Right.MinKeys = opt.Right.MinKeys;
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
        }

        public void ProcessSingleFile(string filePath)
        {
            try
            {
                try { _viewModel.IsProcessing = true; } catch (Exception ex) { Debug.WriteLine($"Failed to set IsProcessing=true: {ex.Message}"); }

                FileProcessingHelper.ValidateAndRunWithPackaging(filePath, (fp) =>
                {
                    var dp = new DP();
                    string newFilepath = dp.ProcessFile(fp, _viewModel.Options);
                    return newFilepath;
                }, openOsz: false, onCompleted: () => { try { _viewModel.IsProcessing = false; } catch (Exception ex) { Debug.WriteLine($"Failed to set IsProcessing=false in onCompleted: {ex.Message}"); } });
            }
            catch (Exception ex)
            {
                try { _viewModel.IsProcessing = false; } catch (Exception iex) { Debug.WriteLine($"Failed to set IsProcessing=false in catch: {iex.Message}"); }
                MessageBox.Show(("Error processing file: {0}".Localize()).Replace("{0}", ex.Message), ("Processing Error|处理错误".Localize()), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupBindings()
        {
            // Bind toggles: prefer enum-provider when available (keeps enum-key binding consistent across tools)
            if (_enumProvider != null)
            {
                // enum provider path
                SettingsBinder.BindToggle(ModifyKeysCheckBox!, _enumProvider, DPOptionKey.ModifySingleSideKeyCount);
                SettingsBinder.BindToggle(LMirrorCheckBox!, _enumProvider, DPOptionKey.LMirror);
                SettingsBinder.BindToggle(LDensityCheckBox!, _enumProvider, DPOptionKey.LDensity);
                SettingsBinder.BindToggle(RMirrorCheckBox!, _enumProvider, DPOptionKey.RMirror);
                SettingsBinder.BindToggle(RDensityCheckBox!, _enumProvider, DPOptionKey.RDensity);
            }
            else
            {
                // fallback to object-based binding (existing options instance)
                SettingsBinder.BindToggle(ModifyKeysCheckBox!, _viewModel.Options, "ModifySingleSideKeyCount");
                SettingsBinder.BindToggle(LMirrorCheckBox!, _viewModel.Options, "LMirror");
                SettingsBinder.BindToggle(LDensityCheckBox!, _viewModel.Options, "LDensity");
                SettingsBinder.BindToggle(RMirrorCheckBox!, _viewModel.Options, "RMirror");
                SettingsBinder.BindToggle(RDensityCheckBox!, _viewModel.Options, "RDensity");
            }

            // Ensure sliders initial enabled state
            try
            {
                KeysSlider!.IsEnabled = _viewModel.Options.ModifySingleSideKeyCount;
                LMaxKeysSlider!.IsEnabled = _viewModel.Options.LDensity;
                LMinKeysSlider!.IsEnabled = _viewModel.Options.LDensity;
                RMaxKeysSlider!.IsEnabled = _viewModel.Options.RDensity;
                RMinKeysSlider!.IsEnabled = _viewModel.Options.RDensity;
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to set initial slider enabled state: {ex.Message}"); }

            // single subscription to options changes
            if (_viewModel.Options is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (_, e) =>
                {
                    try
                    {
                        switch (e.PropertyName)
                        {
                            case nameof(_viewModel.Options.ModifySingleSideKeyCount):
                                if (KeysSlider != null) KeysSlider.IsEnabled = _viewModel.Options.ModifySingleSideKeyCount;
                                break;
                            case nameof(_viewModel.Options.LDensity):
                                if (LMaxKeysSlider != null) LMaxKeysSlider.IsEnabled = _viewModel.Options.LDensity;
                                if (LMinKeysSlider != null) LMinKeysSlider.IsEnabled = _viewModel.Options.LDensity;
                                break;
                            case nameof(_viewModel.Options.RDensity):
                                if (RMaxKeysSlider != null) RMaxKeysSlider.IsEnabled = _viewModel.Options.RDensity;
                                if (RMinKeysSlider != null) RMinKeysSlider.IsEnabled = _viewModel.Options.RDensity;
                                break;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Options change handler error: {ex.Message}"); }
                };
            }
        }
    }
}
