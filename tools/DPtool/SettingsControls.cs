using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.Tools.Shared;

namespace krrTools.tools.DPtool
{
    // A simple flow-style container mirroring the concise initializer pattern
    public class SettingsFlowPanel : StackPanel
    {
        public SettingsFlowPanel()
        {
            Orientation = Orientation.Vertical;
        }
    }

    // A compact settings slider that binds to a property path on a provided source object.
    // It exposes the inner Slider and Label so existing code can keep using references if needed.
    public class SettingsSlider : StackPanel
    {
        public TextBlock LabelTextBlock { get; private set; }
        public Slider InnerSlider { get; private set; }

        public string LabelText { get; set; } = string.Empty;
        public string TooltipText { get; set; } = string.Empty;
        public object? Source { get; set; }
        public string? Path { get; set; }
        // Enum-based provider support
        public IEnumSettingsProvider? EnumProvider { get; set; }
        public Enum? EnumKey { get; set; }
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 100;
        public double TickFrequency { get; set; } = double.NaN;
        public double KeyboardStep { get; set; } = 1.0;

        private bool _isInitialized;

        public SettingsSlider()
        {
            Orientation = Orientation.Vertical;
            Margin = new Thickness(0, 0, 0, 10);
            LabelTextBlock = new TextBlock();
            InnerSlider = new Slider();
            Children.Add(LabelTextBlock);
            Children.Add(InnerSlider);
            Loaded += SettingsSlider_Loaded;
        }

        private void SettingsSlider_Loaded(object? sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (!string.IsNullOrEmpty(LabelText))
                LabelTextBlock.Text = LabelText;
            if (!string.IsNullOrEmpty(TooltipText))
                ToolTipService.SetToolTip(this, TooltipText);

            InnerSlider.Minimum = Min;
            InnerSlider.Maximum = Max;
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
                        double dv = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        InnerSlider.Value = dv;
                        UpdateLabelWithValue(dv);
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"SettingsSlider enum init error: {ex.Message}"); }

                // Writeback
                InnerSlider.ValueChanged += (_, ev) =>
                {
                    try
                    {
                        EnumProvider.SetValue(EnumKey!, ev.NewValue);
                    }
                    catch (Exception ex) { Debug.WriteLine($"SettingsSlider enum writeback error: {ex.Message}"); }
                    UpdateLabelWithValue(ev.NewValue);
                };

                // subscribe provider change notifications
                EnumProvider.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == EnumKey.ToString())
                    {
                        try
                        {
                            var v = EnumProvider.GetValue(EnumKey);
                            if (v != null)
                            {
                                double dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                                if (Math.Abs(InnerSlider.Value - dv) > 1e-6)
                                {
                                    InnerSlider.Dispatcher.Invoke(() => InnerSlider.Value = dv);
                                    UpdateLabelWithValue(dv);
                                }
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine($"SettingsSlider enum notify error: {ex.Message}"); }
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
                            double dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                            InnerSlider.Value = dv;
                            UpdateLabelWithValue(dv);
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"SettingsSlider init error: {ex.Message}"); }

                    // Listen to slider changes to write back to source
                    InnerSlider.ValueChanged += (_, ev) =>
                    {
                        try
                        {
                            var targetType = prop.PropertyType;
                            object? converted;
                            if (targetType == typeof(int) || targetType == typeof(int?))
                                converted = (int)ev.NewValue;
                            else if (targetType == typeof(float) || targetType == typeof(float?))
                                converted = (float)ev.NewValue;
                            else if (targetType == typeof(double) || targetType == typeof(double?))
                                converted = ev.NewValue;
                            else if (targetType == typeof(bool) || targetType == typeof(bool?))
                                converted = (ev.NewValue > 0.5);
                            else
                                converted = Convert.ChangeType(ev.NewValue, targetType, CultureInfo.InvariantCulture);

                            prop.SetValue(Source, converted);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"SettingsSlider failed to write back value: {ex.Message}");
                        }
                        UpdateLabelWithValue(ev.NewValue);
                    };

                    // If the source supports INotifyPropertyChanged, subscribe so the slider reflects external changes
                    if (Source is INotifyPropertyChanged npc)
                    {
                        npc.PropertyChanged += (_, ev) =>
                        {
                            if (ev.PropertyName == Path)
                            {
                                try
                                {
                                    var v = prop.GetValue(Source);
                                    if (v != null)
                                    {
                                        double dv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                                        if (Math.Abs(InnerSlider.Value - dv) > 1e-6)
                                        {
                                            InnerSlider.Dispatcher.Invoke(() => InnerSlider.Value = dv);
                                            UpdateLabelWithValue(dv);
                                        }
                                    }
                                }
                                catch (Exception ex) { Debug.WriteLine($"SettingsSlider notify error: {ex.Message}"); }
                            }
                        };
                    }
                }
                else
                {
                    // If property not found, attempt to bind via WPF binding system if Source is a dependency source
                    try
                    {
                        var binding = new Binding(Path!) { Source = Source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
                        InnerSlider.SetBinding(RangeBase.ValueProperty, binding);
                    }
                    catch (Exception ex) { Debug.WriteLine($"SettingsSlider binding fallback error: {ex.Message}"); }
                }
            }
        }

        private void UpdateLabelWithValue(double value)
        {
            try
            {
                if (!string.IsNullOrEmpty(LabelText))
                {
                    // Append numeric value to label
                    LabelTextBlock.Text = LabelText + " " + ((int)value).ToString();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"UpdateLabelWithValue error: {ex.Message}"); }
        }
    }

    public class SettingsSlider<T> : SettingsSlider
    {
        // Generic wrapper to allow usage like `new SettingsSlider<double> { ... }`.
        // T is not directly used by the implementation; this wrapper preserves the concise API.
    }
}
