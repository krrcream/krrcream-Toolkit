using System;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Data;
using krrTools.Tools.Shared;

namespace krrTools.tools.DPtool
{
    public static class SettingsBinder
    {
        public static void BindToggle(ToggleButton toggle, object source, string path)
        {
            // parameters are expected non-null; callers should ensure validity
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            toggle.SetBinding(ToggleButton.IsCheckedProperty, binding);
        }

        public static void BindSlider(RangeBase slider, object source, string path)
        {
            // parameters are expected non-null; callers should ensure validity
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            slider.SetBinding(RangeBase.ValueProperty, binding);
        }

        public static void BindTextBlock(TextBlock textBlock, object source, string path, string? stringFormat = null)
        {
            // parameters are expected non-null; callers should ensure validity
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            if (!string.IsNullOrEmpty(stringFormat)) binding.StringFormat = stringFormat;
            textBlock.SetBinding(TextBlock.TextProperty, binding);
        }

        // Bind a ToggleButton to an enum-based settings provider + key.
        public static void BindToggle(ToggleButton toggle, IEnumSettingsProvider provider, Enum key)
        {
            // parameters are expected non-null; callers should ensure validity

            // Initialize from provider
            try
            {
                var v = provider.GetValue(key);
                if (v is bool b) toggle.IsChecked = b;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BindToggle(enum) init error: {ex.Message}");
            }

            // When user toggles, write back to provider
            toggle.Checked += (_, _) =>
            {
                try { provider.SetValue(key, true); }
                catch (Exception ex) { Debug.WriteLine($"BindToggle(enum) write error: {ex.Message}"); }
            };
            toggle.Unchecked += (_, _) =>
            {
                try { provider.SetValue(key, false); }
                catch (Exception ex) { Debug.WriteLine($"BindToggle(enum) write error: {ex.Message}"); }
            };

            // Listen for provider changes
            provider.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == key.ToString())
                {
                    try
                    {
                        var nv = provider.GetValue(key);
                        if (nv is bool nb)
                        {
                            toggle.Dispatcher.Invoke(() => toggle.IsChecked = nb);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"BindToggle(enum) notify error: {ex.Message}");
                    }
                }
            };
        }
    }
}
