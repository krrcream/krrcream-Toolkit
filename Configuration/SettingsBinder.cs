using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Configuration
{
    public static class SettingsBinder
    {
        private static void BindToggle(ToggleButton toggle, object source, string path)
        {
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            toggle.SetBinding(ToggleButton.IsCheckedProperty, binding);
        }

        private static void BindSlider(RangeBase slider, object source, string path)
        {
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            slider.SetBinding(RangeBase.ValueProperty, binding);
        }

        /// <summary>
        /// 基于选项类自动生成设置UI面板
        /// </summary>
        /// <param name="options">选项实例</param>
        /// <returns>包含所有设置控件的StackPanel</returns>
        public static StackPanel CreateSettingsPanel(object options)
        {
            var panel = new StackPanel
                { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };

            var properties = options.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;

                UIElement? control = null;
                string label = GetLocalizedString(attr.LabelKey);
                string? tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

                switch (attr.UIType)
                {
                    case UIType.Toggle:
                        if (prop.PropertyType == typeof(bool))
                        {
                            var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);
                            BindToggle(checkBox, options, prop.Name);
                            control = checkBox;
                        }

                        break;
                    case UIType.Slider:
                        if (IsNumericType(prop.PropertyType))
                        {
                            var sliderSettings = new SettingsSlider<double>
                            {
                                LabelText = label,
                                TooltipText = tooltip ?? "",
                                Min = attr.Min as double? ?? 0,
                                Max = attr.Max as double? ?? 100,
                                TickFrequency = attr.TickFrequency ?? 1,
                                KeyboardStep = attr.KeyboardStep ?? 1
                            };
                            BindSlider(sliderSettings.InnerSlider, options, prop.Name);
                            control = sliderSettings;
                        }

                        break;
                    case UIType.Text:
                        if (prop.PropertyType == typeof(string))
                        {
                            var textBox = new TextBox { Text = prop.GetValue(options) as string ?? "" };
                            var binding = new Binding(prop.Name)
                            {
                                Source = options,
                                Mode = BindingMode.TwoWay,
                                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                            };
                            textBox.SetBinding(TextBox.TextProperty, binding);
                            control = SharedUIComponents.CreateLabeledRow(label, textBox, new Thickness(0, 0, 0, 10));
                        }

                        break;
                }

                if (control != null)
                {
                    panel.Children.Add(control);
                }
            }

            return panel;
        }

        private static string GetLocalizedString(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var stringsType = typeof(Strings);
            var field = stringsType.GetField(key, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(string))
            {
                return field.GetValue(null) as string ?? key;
            }

            return key;
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(decimal);
        }

        /// <summary>
        /// 创建模板化的单个控件
        /// </summary>
        public static FrameworkElement CreateTemplatedControl<T>(T options, Expression<Func<T, object>> propertySelector) where T : class
        {
            var propertyName = GetPropertyName(propertySelector);
            var prop = options.GetType().GetProperty(propertyName);
            if (prop == null) return new TextBlock { Text = $"Property {propertyName} not found" };

            var attr = prop.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyName}" };

            string label = GetLocalizedString(attr.LabelKey);
            string? tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (prop.PropertyType == typeof(bool))
            {
                var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);
                BindToggle(checkBox, options, propertyName);
                return checkBox;
            }

            return new TextBlock { Text = $"Unsupported control type for {propertyName}" };
        }

        /// <summary>
        /// 创建模板化的滑块控件
        /// </summary>
        public static UIElement CreateTemplatedSlider<T>(T options, Expression<Func<T, object>> propertySelector) where T : class
        {
            var propertyName = GetPropertyName(propertySelector);
            var prop = options.GetType().GetProperty(propertyName);
            if (prop == null) return new TextBlock { Text = $"Property {propertyName} not found" };

            var attr = prop.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyName}" };

            string label = GetLocalizedString(attr.LabelKey);
            string? tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (IsNumericType(prop.PropertyType))
            {
                var sliderSettings = new SettingsSlider
                {
                    LabelText = label,
                    TooltipText = tooltip ?? "",
                    Min = Convert.ToDouble(attr.Min ?? 0),
                    Max = Convert.ToDouble(attr.Max ?? 100),
                    TickFrequency = attr.TickFrequency ?? 1,
                    KeyboardStep = attr.KeyboardStep ?? 1,
                    Source = options,
                    Path = propertyName
                };
                return sliderSettings;
            }

            return new TextBlock { Text = $"Unsupported slider type for {propertyName}" };
        }

        private static string GetPropertyName<T>(Expression<Func<T, object>> propertySelector)
        {
            if (propertySelector.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            else if (propertySelector.Body is UnaryExpression { Operand: MemberExpression unaryMember })
            {
                return unaryMember.Member.Name;
            }
            throw new ArgumentException("Invalid property selector expression");
        }
    }
}
