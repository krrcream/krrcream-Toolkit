using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using krrTools.Configuration;
using krrTools.Localization;

namespace krrTools.UI;

// 一个带标签和滑块的设置控件，支持双语标签和数据绑定
public class SettingsSlider : StackPanel
{
    public TextBlock Label { get; private set; }
    public Slider InnerSlider { get; private set; }

    private string _labelText = string.Empty;

    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            if (_isInitialized)
            {
                UpdateLabelWithValue(InnerSlider.Value);
            }
        }
    }

    public string TooltipText { get; set; } = string.Empty;
    public object? Source { get; set; }
    public string? Path { get; set; }

    // Enum-based provider support
    public IEnumSettingsProvider? EnumProvider { get; set; }
    public Enum? EnumKey { get; set; }
    public double Min { get; set; } = 1;
    public double Max { get; set; } = 100;
    public double TickFrequency { get; set; } = double.NaN;
    public double KeyboardStep { get; set; } = 1.0;

    private bool _isInitialized;
    private DispatcherTimer? _debounceTimer;
    private double _pendingValue;

    public SettingsSlider()
    {
        Margin = new Thickness(0, 0, 0, 10);
        Label = new TextBlock
        {
            FontSize = SharedUIComponents.HeaderFontSize,
            FontWeight = FontWeights.Bold
        };
        InnerSlider = new Slider();
        Children.Add(Label);
        Children.Add(InnerSlider);
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounceTimer.Tick += OnDebounceTimerTick;
        Loaded += SettingsSlider_Loaded;
        IsEnabledChanged += SettingsSlider_IsEnabledChanged;

        // Listen to language changes to update labels
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void SettingsSlider_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (!string.IsNullOrEmpty(LabelText))
        {
            // Remove the initial TextBlock and create proper bilingual label
            Children.Remove(Label);
            Label = new TextBlock { FontSize = SharedUIComponents.HeaderFontSize, FontWeight = FontWeights.Bold };
            Children.Insert(0, Label);
            UpdateLabelWithValue(InnerSlider.Value); // Set initial label text
        }

        if (!string.IsNullOrEmpty(TooltipText))
            ToolTipService.SetToolTip(this, TooltipText);

        InnerSlider.Minimum = Min;
        InnerSlider.Maximum = Max;
        InnerSlider.LargeChange = 1.0;
        if (!double.IsNaN(TickFrequency)) InnerSlider.TickFrequency = TickFrequency;
        InnerSlider.SmallChange = KeyboardStep;
        InnerSlider.IsSnapToTickEnabled = !double.IsNaN(TickFrequency);

        // First prefer enum-provider binding when supplied
        if (EnumProvider != null && EnumKey != null)
        {
            try
            {
                var value = EnumProvider.GetValue(EnumKey);
                if (value != null)
                {
                    var dv = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    InnerSlider.Value = dv;
                    UpdateLabelWithValue(dv);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider enum init error: {0}", ex.Message);
            }

            // Writeback
            InnerSlider.ValueChanged += (_, ev) =>
            {
                _pendingValue = ev.NewValue;
                _debounceTimer!.Stop();
                _debounceTimer.Start();
                UpdateLabelWithValue(ev.NewValue);
            };

            // subscribe provider change notifications
            EnumProvider.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == EnumKey.ToString())
                    try
                    {
                        var v = EnumProvider.GetValue(EnumKey);
                        if (v != null)
                        {
                            var dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                            if (Math.Abs(InnerSlider.Value - dv) > 1e-6)
                            {
                                InnerSlider.Dispatcher.Invoke(() => InnerSlider.Value = dv);
                                UpdateLabelWithValue(dv);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider enum notify error: {0}", ex.Message);
                    }
            };
        }
        else if (Source != null && !string.IsNullOrEmpty(Path))
        {
            // Try to bind or fall back to manual reflection update
            var prop = Source.GetType().GetProperty(Path!);
            if (prop != null)
            {
                // Initialize slider value from source
                try
                {
                    var v = prop.GetValue(Source);
                    if (v != null)
                    {
                        var dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                        InnerSlider.Value = dv;
                        UpdateLabelWithValue(dv);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider init error: {0}", ex.Message);
                }

                // Listen to slider changes to write back to source
                InnerSlider.ValueChanged += (_, ev) =>
                {
                    _pendingValue = ev.NewValue;
                    _debounceTimer!.Stop();
                    _debounceTimer.Start();
                    UpdateLabelWithValue(ev.NewValue);
                };

                if (Source is INotifyPropertyChanged npc)
                    npc.PropertyChanged += (_, ev) =>
                    {
                        if (ev.PropertyName == Path)
                            try
                            {
                                var v = prop.GetValue(Source);
                                if (v != null)
                                {
                                    var dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                                    if (Math.Abs(InnerSlider.Value - dv) > 1e-6)
                                    {
                                        InnerSlider.Dispatcher.Invoke(() => InnerSlider.Value = dv);
                                        UpdateLabelWithValue(dv);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider notify error: {0}", ex.Message);
                            }
                    };
            }
            else
            {
                try
                {
                    var binding = new Binding(Path!)
                    {
                        Source = Source,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    InnerSlider.SetBinding(RangeBase.ValueProperty, binding);
                    // Ensure label updates with value changes
                    InnerSlider.ValueChanged += (_, ev) => UpdateLabelWithValue(ev.NewValue);
                    // Update label with initial value
                    UpdateLabelWithValue(InnerSlider.Value);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider binding fallback error: {0}", ex.Message);
                }
            }
        }
    }

    private void UpdateLabelWithValue(double value)
    {
        if (!string.IsNullOrEmpty(_labelText))
        {
            if (_labelText.Contains("{0}"))
            {
                // Use template formatting
                Label.Text = Strings.FormatLocalized(_labelText, (int)value);
            }
            else
            {
                // Use legacy formatting
                var localizedLabel = Strings.Localize(_labelText);
                Label.Text = localizedLabel + ": " + ((int)value).ToString();
            }
        }
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer!.Stop();
        // Write back the pending value
        if (EnumProvider != null && EnumKey != null)
        {
            try
            {
                EnumProvider.SetValue(EnumKey, _pendingValue);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider enum debounce writeback error: {0}", ex.Message);
            }
        }
        else if (Source != null && !string.IsNullOrEmpty(Path))
        {
            var prop = Source.GetType().GetProperty(Path!);
            if (prop != null)
                try
                {
                    var targetType = prop.PropertyType;
                    object? converted;
                    if (targetType == typeof(int) || targetType == typeof(int?))
                        converted = (int)_pendingValue;
                    else if (targetType == typeof(float) || targetType == typeof(float?))
                        converted = (float)_pendingValue;
                    else if (targetType == typeof(double) || targetType == typeof(double?))
                        converted = _pendingValue;
                    else if (targetType == typeof(bool) || targetType == typeof(bool?))
                        converted = _pendingValue > 0.5;
                    else
                        converted = Convert.ChangeType(_pendingValue, targetType,
                            CultureInfo.InvariantCulture);

                    prop.SetValue(Source, converted);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider debounce failed to write back value: {0}", ex.Message);
                }
        }
    }

    private void SettingsSlider_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        InnerSlider.IsEnabled = IsEnabled;
    }

    private void OnLanguageChanged()
    {
        // Update label when language changes
        if (_isInitialized)
        {
            UpdateLabelWithValue(InnerSlider.Value);
        }
    }
}