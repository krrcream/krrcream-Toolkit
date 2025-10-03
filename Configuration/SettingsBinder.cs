using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.Localization;
using krrTools.UI;
using Wpf.Ui.Controls;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

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
                var localizedLabel = GetLocalizedString(attr.LabelKey).GetLocalizedString();
                var localizedTooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey).GetLocalizedString();

                switch (attr.UIType)
                {
                    case UIType.Toggle:
                        if (prop.PropertyType == typeof(bool))
                        {
                            var checkBox = SharedUIComponents.CreateStandardCheckBox("", localizedTooltip?.Value);
                            checkBox.SetBinding(ContentControl.ContentProperty, new Binding("Value") { Source = localizedLabel });
                            if (localizedTooltip != null)
                            {
                                checkBox.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Value") { Source = localizedTooltip });
                            }
                            BindToggle(checkBox, options, prop.Name);
                            control = checkBox;
                        }

                        break;
                    case UIType.Slider:
                        if (IsNumericType(prop.PropertyType))
                        {
                            var sliderSettings = new SettingsSlider
                            {
                                LabelText = "", // 空
                                TooltipText = localizedTooltip?.Value ?? "",
                                Min = attr.Min as double? ?? 0,
                                Max = attr.Max as double? ?? 100,
                                TickFrequency = attr.TickFrequency ?? 1,
                                KeyboardStep = attr.KeyboardStep ?? 1
                            };
                            sliderSettings.Label.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = localizedLabel });
                            if (localizedTooltip != null)
                            {
                                sliderSettings.Label.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Value") { Source = localizedTooltip });
                            }
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
                            control = SharedUIComponents.CreateLabeledRow(localizedLabel.Value, textBox, new Thickness(0, 0, 0, 10));
                        }

                        break;
                    case UIType.ComboBox:
                        // 可根据需要实现
                        break;
                    case UIType.NumberBox:
                        if (IsNumericType(prop.PropertyType))
                        {
                            var textBox = new NumberBox()
                            {
                                Value = (prop.GetValue(options) as double? ??
                                    (double)(prop.GetValue(options) ?? 114514)),
                                Minimum = attr.Min as double? ?? 0,
                            };
                            var binding = new Binding(prop.Name)
                            {
                                Source = options,
                                Mode = BindingMode.TwoWay,
                                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                            };
                            textBox.SetBinding(TextBox.TextProperty, binding);
                            control = SharedUIComponents.CreateLabeledRow(localizedLabel.Value, textBox, new Thickness(0, 0, 0, 10));
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
        /// 创建模板化的单个控件（使用表达式）
        /// </summary>
        public static FrameworkElement CreateTemplatedControl<T>(T options, Expression<Func<T, object>> propertySelector) where T : class
        {
            string propertyPath = GetPropertyPathFromExpression(propertySelector);
            return CreateTemplatedControl(options, propertyPath);
        }

        /// <summary>
        /// 创建模板化的单个控件（使用字符串路径）
        /// </summary>
        public static FrameworkElement CreateTemplatedControl<T>(T options, string propertyPath) where T : class
        {
            var prop = GetPropertyFromPath(options, propertyPath);
            if (prop == null) return new TextBlock { Text = $"Property {propertyPath} not found" };

            var attr = prop.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyPath}" };

            string label = GetLocalizedString(attr.LabelKey);
            string? tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (prop.PropertyType == typeof(bool))
            {
                var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);
                BindToggle(checkBox, options, propertyPath);
                return checkBox;
            }

            return new TextBlock { Text = $"Unsupported control type for {propertyPath}" };
        }

        /// <summary>
        /// 创建模板化的滑块控件（使用表达式）
        /// </summary>
        public static UIElement CreateTemplatedSlider<T>(T options, Expression<Func<T, object>> propertySelector) where T : class
        {
            string propertyPath = GetPropertyPathFromExpression(propertySelector);
            return CreateTemplatedSlider(options, propertyPath);
        }

        /// <summary>
        /// 创建模板化的滑块控件（使用字符串路径）
        /// </summary>
        public static UIElement CreateTemplatedSlider<T>(T options, string propertyPath) where T : class
        {
            var prop = GetPropertyFromPath(options, propertyPath);
            if (prop == null) return new TextBlock { Text = $"Property {propertyPath} not found" };

            var attr = prop.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyPath}" };

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
                    Path = propertyPath
                };
                return sliderSettings;
            }

            return new TextBlock { Text = $"Unsupported slider type for {propertyPath}" };
        }

        private static PropertyInfo? GetPropertyFromPath(object obj, string path)
        {
            var parts = path.Split('.');
            PropertyInfo? prop = null;
            object? current = obj;
            foreach (var part in parts)
            {
                if (current == null) return null;
                prop = current.GetType().GetProperty(part);
                if (prop == null) return null;
                current = prop.GetValue(current);
            }
            return prop;
        }



        private static string GetPropertyPathFromExpression<T>(Expression<Func<T, object>> propertySelector)
        {
            var path = new List<string>();
            System.Linq.Expressions.Expression? current = propertySelector.Body;

            while (current != null)
            {
                if (current is MemberExpression member)
                {
                    path.Insert(0, member.Member.Name);
                    current = member.Expression;
                }
                else if (current is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
                {
                    path.Insert(0, unaryMember.Member.Name);
                    current = unaryMember.Expression;
                }
                else
                {
                    break;
                }
            }

            return string.Join(".", path);
        }
    }
}
