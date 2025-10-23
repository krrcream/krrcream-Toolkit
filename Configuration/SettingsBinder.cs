using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using krrTools.Bindable;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;
using Expression = System.Linq.Expressions.Expression;
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.Configuration
{
    public static class SettingsBinder
    {
        private static string GetLocalizedString(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var stringsType = typeof(Strings);
            var field = stringsType.GetField(key, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(string)) return field.GetValue(null) as string ?? key;

            return key;
        }

        private static bool IsNumericType(Type type)
        {
            // 检查基本数值类型
            if (type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(decimal) ||
                type == typeof(int?) || type == typeof(double?) || type == typeof(float?) || type == typeof(decimal?))
                return true;

            // 检查 Bindable<T> 类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                var genericArg = type.GetGenericArguments()[0];
                return genericArg == typeof(int) || genericArg == typeof(double) || genericArg == typeof(float) || genericArg == typeof(decimal) ||
                       genericArg == typeof(int?) || genericArg == typeof(double?) || genericArg == typeof(float?) || genericArg == typeof(decimal?);
            }

            return false;
        }

        /// <summary>
        /// 创建模板化的单个控件（使用简化的绑定API）
        /// </summary>
        public static FrameworkElement CreateTemplatedControl<T>(T options, Expression<Func<T, object>> propertySelector)
            where T : class
        {
            // 直接从lambda表达式获取属性信息
            var propertyInfo = GetPropertyInfoFromExpression(propertySelector);
            if (propertyInfo == null) return new TextBlock { Text = "Invalid property selector" };

            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var label = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            var effectiveType = attr.DataType ?? propertyInfo.PropertyType;

            // 根据UIType决定控件类型
            switch (attr.UIType)
            {
                case UIType.Toggle:
                    if (effectiveType == typeof(bool) || (effectiveType.IsGenericType && effectiveType.GetGenericTypeDefinition() == typeof(Bindable<>)))
                    {
                        var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);

                        var path = GetPropertyPathFromExpression(propertySelector);
                        // 检查是否是 Bindable<T> 类型，如果是则添加 .Value
                        if (effectiveType.IsGenericType && effectiveType.GetGenericTypeDefinition() == typeof(Bindable<>))
                        {
                            path += ".Value";
                        }
                        var binding = new Binding(path)
                        {
                            Source = options,
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        };
                        checkBox.SetBinding(ToggleButton.IsCheckedProperty, binding);
                        return checkBox;
                    }
                    break;
                case UIType.Slider:
                    if (IsNumericType(effectiveType))
                    {
                        return (FrameworkElement)CreateEnhancedTemplatedSlider(options, propertySelector, propertyInfo);
                    }
                    break;
                case UIType.NumberBox:
                    // 对于数字输入框，使用TextBox
                    var numberBox = SharedUIComponents.CreateStandardTextBox();
                    // 使用标准WPF绑定
                    var numberPath = GetPropertyPathFromExpression(propertySelector);
                    // 检查是否是 Bindable<T> 类型，如果是则添加 .Value
                    if (effectiveType.IsGenericType && effectiveType.GetGenericTypeDefinition() == typeof(Bindable<>))
                    {
                        numberPath += ".Value";
                    }
                    var numberBinding = new Binding(numberPath)
                    {
                        Source = options,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    numberBox.SetBinding(TextBox.TextProperty, numberBinding);
                    return numberBox;
                case UIType.Text:
                    // 对于文本，使用TextBox
                    var textBox = SharedUIComponents.CreateStandardTextBox();
                    // 使用标准WPF绑定
                    var textPath = GetPropertyPathFromExpression(propertySelector);
                    var textBinding = new Binding(textPath)
                    {
                        Source = options,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    textBox.SetBinding(TextBox.TextProperty, textBinding);
                    return textBox;
                case UIType.ComboBox:
                    if (effectiveType.IsEnum)
                    {
                        var comboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
                        if (!string.IsNullOrEmpty(tooltip)) ToolTipService.SetToolTip(comboBox, tooltip);

                        // 设置枚举项源
                        comboBox.ItemsSource = Enum.GetValues(effectiveType);
                        
                        // 使用标准WPF绑定
                        var enumPath = GetPropertyPathFromExpression(propertySelector);
                        var enumBinding = new Binding(enumPath)
                        {
                            Source = options,
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        };
                        comboBox.SetBinding(Selector.SelectedItemProperty, enumBinding);
                        return comboBox;
                    }
                    break;
            }

            return new TextBlock { Text = $"Unsupported control type for {propertyInfo.Name}" };
        }

        /// <summary>
        /// 创建模板化的滑块控件（使用表达式，可选勾选框，可选字典映射）
        /// </summary>
        public static UIElement CreateTemplatedSlider<T>(T options, Expression<Func<T, object>> propertySelector,
            Expression<Func<T, object>>? checkPropertySelector = null, Dictionary<double, string>? valueDisplayMap = null) where T : class
        {
            var propertyInfo = GetPropertyInfoFromExpression(propertySelector);
            if (propertyInfo == null) return new TextBlock { Text = "Invalid property selector" };

            var checkPropertyInfo =
                checkPropertySelector != null ? GetPropertyInfoFromExpression(checkPropertySelector) : null;
            var checkEnabled = checkPropertyInfo != null;

            // 如果没有显式指定checkPropertySelector，但属性是可空的Bindable<T>，则自动启用勾选框
            if (!checkEnabled && propertyInfo.PropertyType.IsGenericType && 
                propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                var genericArg = propertyInfo.PropertyType.GetGenericArguments()[0];
                checkEnabled = Nullable.GetUnderlyingType(genericArg) != null;
            }

            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var labelKey = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            // 如果没有提供 valueDisplayMap，但属性有 DisplayMapField，则尝试获取
            if (valueDisplayMap == null && !string.IsNullOrEmpty(attr.DisplayMapField))
            {
                var optionsType = options.GetType();
                var mapField = optionsType.GetField(attr.DisplayMapField, BindingFlags.Public | BindingFlags.Static);
                if (mapField != null && mapField.FieldType.IsGenericType && 
                    mapField.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    valueDisplayMap = mapField.GetValue(null) as Dictionary<double, string>;
                }
            }

            if (IsNumericType(propertyInfo.PropertyType))
            {
                // 处理 Bindable<T> 类型
                double min, max;
                if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    // 对于 Bindable<T>，使用属性定义的 Min/Max
                    min = Convert.ToDouble(attr.Min ?? 0);
                    max = Convert.ToDouble(attr.Max ?? 100);
                }
                else
                {
                    // 对于基本数值类型，使用属性定义的 Min/Max
                    min = Convert.ToDouble(attr.Min ?? 0);
                    max = Convert.ToDouble(attr.Max ?? 100);
                }

                var slider = new SettingsSlider<T>
                {
                    LabelKey = labelKey,
                    TooltipText = tooltip ?? "",
                    Min = min,
                    Max = max,
                    TickFrequency = attr.TickFrequency ?? 1,
                    KeyboardStep = attr.KeyboardStep ?? 1,
                    Source = options,
                    PropertySelector = propertySelector,
                    CheckEnabled = checkEnabled,
                    ValueDisplayMap = valueDisplayMap
                };
                return slider;
            }

            return new TextBlock { Text = $"Unsupported slider type for {propertyInfo.Name}" };
        }

        /// <summary>
        /// 从lambda表达式获取属性信息
        /// </summary>
        private static PropertyInfo? GetPropertyInfoFromExpression<T, TResult>(Expression<Func<T, TResult>> propertySelector)
        {
            var current = propertySelector.Body;

            // 处理可能的转换 (如 int -> double)
            if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary) current = unary.Operand;

            if (current is MemberExpression { Member: PropertyInfo propertyInfo }) return propertyInfo;

            return null;
        }

        /// <summary>
        /// 安全地从表达式中提取属性路径
        /// </summary>
        private static string GetPropertyPathFromExpression<T, TResult>(Expression<Func<T, TResult>> propertySelector)
        {
            var path = new List<string>();
            var current = propertySelector.Body;

            while (current != null)
                if (current is MemberExpression member)
                {
                    path.Insert(0, member.Member.Name);
                    current = member.Expression;
                }
                else if (current is UnaryExpression { Operand: MemberExpression unaryMember })
                {
                    path.Insert(0, unaryMember.Member.Name);
                    current = unaryMember.Expression;
                }
                else
                {
                    break;
                }

            return string.Join(".", path);
        }

        /// <summary>
        /// 创建种子输入面板，包含标签、文本框和生成按钮
        /// </summary>
        public static FrameworkElement CreateSeedPanel<T, TProperty>(T dataContext, Expression<Func<T, TProperty>> seedProperty)
            where T : class
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = SharedUIComponents.CreateHeaderLabel(Strings.SeedButtonLabel);
            var generateButton =
                SharedUIComponents.CreateStandardButton(Strings.SeedGenerateLabel, Strings.SeedGenerateTooltip);

            var SeedTextBox = SharedUIComponents.CreateStandardTextBox();
            SeedTextBox.Margin = new Thickness(5, 0, 5, 0);
            SeedTextBox.IsReadOnly = false;

            // 绑定 Seed 属性
            var seedPath = GetPropertyPathFromExpression(seedProperty);
            var propertyInfo = GetPropertyInfoFromExpression(seedProperty);
            if (propertyInfo != null && propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                seedPath += ".Value";
            }
            var binding = new Binding(seedPath)
            {
                Source = dataContext,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            SeedTextBox.SetBinding(TextBox.TextProperty, binding);

            generateButton.Click += (_, _) =>
            {
                var random = new Random();
                SeedTextBox.Text = random.Next(0, int.MaxValue).ToString();
            };

            System.Windows.Controls.Grid.SetColumn(label, 0);
            System.Windows.Controls.Grid.SetColumn(SeedTextBox, 1);
            System.Windows.Controls.Grid.SetColumn(generateButton, 2);

            grid.Children.Add(label);
            grid.Children.Add(SeedTextBox);
            grid.Children.Add(generateButton);

            return grid;
        }

        /// <summary>
        /// 创建支持动态最大值的模板化滑块控件
        /// </summary>
        public static UIElement CreateTemplatedSliderWithDynamicMax<T>(
            T options, 
            Expression<Func<T, object>> propertySelector,
            object dynamicMaxSource,
            string dynamicMaxPath,
            Expression<Func<T, object>>? checkPropertySelector = null, 
            Dictionary<double, string>? valueDisplayMap = null) where T : class
        {
            var propertyInfo = GetPropertyInfoFromExpression(propertySelector);
            if (propertyInfo == null) return new TextBlock { Text = "Invalid property selector" };

            var checkPropertyInfo =
                checkPropertySelector != null ? GetPropertyInfoFromExpression(checkPropertySelector) : null;
            var checkEnabled = checkPropertyInfo != null;

            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var labelKey = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (IsNumericType(propertyInfo.PropertyType))
            {
                // 处理 Bindable<T> 类型
                double min, max;
                if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    // 对于 Bindable<T>，使用属性定义的 Min/Max
                    min = Convert.ToDouble(attr.Min ?? 0);
                    max = Convert.ToDouble(attr.Max ?? 100);
                }
                else
                {
                    // 对于基本数值类型，使用属性定义的 Min/Max
                    min = Convert.ToDouble(attr.Min ?? 0);
                    max = Convert.ToDouble(attr.Max ?? 100);
                }

                var slider = new SettingsSlider<T>
                {
                    LabelKey = labelKey,
                    TooltipText = tooltip ?? "",
                    Min = min,
                    Max = max, // 初始最大值，会被动态绑定覆盖
                    TickFrequency = attr.TickFrequency ?? 1,
                    KeyboardStep = attr.KeyboardStep ?? 1,
                    Source = options,
                    PropertySelector = propertySelector,
                    CheckEnabled = checkEnabled,
                    ValueDisplayMap = valueDisplayMap,
                    // 设置动态绑定
                    DynamicMaxSource = dynamicMaxSource,
                    DynamicMaxPath = dynamicMaxPath
                };
                return slider;
            }

            return new TextBlock { Text = $"Unsupported slider type for {propertyInfo.Name}" };
        }

        /// <summary>
        /// 增强版滑条创建方法 - 自动从OptionAttribute读取配置
        /// </summary>
        private static UIElement CreateEnhancedTemplatedSlider<T>(T options, Expression<Func<T, object>> propertySelector, PropertyInfo propertyInfo) where T : class
        {
            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var labelKey = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            // 获取滑条配置
            var sliderConfig = GetSliderConfiguration(attr, options.GetType());
        
            // 检查是否有勾选框配置
            Expression<Func<T, object>>? checkPropertySelector = null;
            if (sliderConfig.HasCheckBox && !string.IsNullOrEmpty(attr.CheckBoxProperty))
            {
                try 
                {
                    var param = Expression.Parameter(typeof(T), "x");
                    var checkProperty = Expression.Property(param, attr.CheckBoxProperty);
                    var convertToObject = Expression.Convert(checkProperty, typeof(object));
                    checkPropertySelector = Expression.Lambda<Func<T, object>>(convertToObject, param);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Warning, "[SettingsBinder] Failed to create checkbox selector for {0}: {1}", 
                        attr.CheckBoxProperty, ex.Message);
                }
            }

            var checkEnabled = checkPropertySelector != null;

            // 对于可空类型的 Bindable，自动启用勾选框
            if (!checkEnabled && propertyInfo.PropertyType.IsGenericType && 
                propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                var genericArg = propertyInfo.PropertyType.GetGenericArguments()[0];
                if (Nullable.GetUnderlyingType(genericArg) != null)
                {
                    checkEnabled = true;
                }
            }

            if (IsNumericType(propertyInfo.PropertyType))
            {
                var slider = new SettingsSlider<T>
                {
                    LabelKey = labelKey,
                    TooltipText = tooltip ?? "",
                    Min = sliderConfig.Min,
                    Max = sliderConfig.Max,
                    TickFrequency = sliderConfig.Step,
                    KeyboardStep = sliderConfig.KeyboardStep,
                    Source = options,
                    PropertySelector = propertySelector,
                    CheckEnabled = checkEnabled,
                    ValueDisplayMap = sliderConfig.DisplayValueMap
                };
                return slider;
            }

            return new TextBlock { Text = $"Unsupported slider type for {propertyInfo.Name}" };
        }

        /// <summary>
        /// 从OptionAttribute获取滑条配置
        /// </summary>
        private static SliderConfiguration GetSliderConfiguration(OptionAttribute attr, Type containerType)
        {
            var config = new SliderConfiguration
            {
                Min = attr.Min != null ? Convert.ToDouble(attr.Min) : 0.0,
                Max = attr.Max != null ? Convert.ToDouble(attr.Max) : 100.0,
                Step = attr.TickFrequency ?? 1.0,
                KeyboardStep = attr.KeyboardStep ?? 1.0,
                HasCheckBox = attr.HasCheckBox
            };

            // 1. 检查自定义显示映射字段
            if (!string.IsNullOrEmpty(attr.DisplayMapField))
            {
                try
                {
                    var field = containerType.GetField(attr.DisplayMapField, BindingFlags.Public | BindingFlags.Static);
                    if (field?.GetValue(null) is Dictionary<double, string> displayMap)
                    {
                        config.DisplayValueMap = displayMap;
                    }
                    else
                    {
                        Logger.WriteLine(LogLevel.Warning, "[SettingsBinder] DisplayMapField {0} not found or invalid type", attr.DisplayMapField);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[SettingsBinder] Error accessing DisplayMapField {0}: {1}", attr.DisplayMapField, ex.Message);
                }
            }

            // 2. 检查自定义实际值映射字段
            if (!string.IsNullOrEmpty(attr.ActualMapField))
            {
                try
                {
                    var field = containerType.GetField(attr.ActualMapField, BindingFlags.Public | BindingFlags.Static);
                    if (field?.GetValue(null) is Dictionary<double, double> actualMap)
                    {
                        config.ActualValueMap = actualMap;
                    }
                    else
                    {
                        Logger.WriteLine(LogLevel.Warning, "[SettingsBinder] ActualMapField {0} not found or invalid type", attr.ActualMapField);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[SettingsBinder] Error accessing ActualMapField {0}: {1}", attr.ActualMapField, ex.Message);
                }
            }

            return config;
        }
    }
}