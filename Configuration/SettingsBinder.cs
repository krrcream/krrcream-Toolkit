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
using Microsoft.Extensions.Logging;
using Expression = System.Linq.Expressions.Expression;
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools.Configuration
{
    /// <summary>
    /// 用法示例：
    /// <para></para>
    /// control.Bind(source, x => x.Property)
    /// <para></para>
    /// - 布尔值：   checkBox.Bind(options, x => x.Enabled)
    /// <para></para>
    /// - 数值：     slider.Bind(options, x => x.Volume)
    /// <para></para>
    /// - 文本：     textBox.Bind(options, x => x.Name)
    /// <para></para>
    /// - 枚举：     comboBox.BindEnum&lt;MyEnum&gt;(options, x => x.Mode)
    /// </summary>
    public static class QuickBind
    {
        /// <summary>
        /// 双向绑定：control.Bind(source, x => x.Property)
        /// </summary>
        public static T Bind<T, TSource, TProperty>(this T control, TSource source,
            Expression<Func<TSource, TProperty>> property)
            where T : FrameworkElement
        {
            var path = GetPath(property);
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // 根据控件类型自动选择目标属性
            var targetProperty = control switch
            {
                ToggleButton => ToggleButton.IsCheckedProperty,
                RangeBase => RangeBase.ValueProperty,
                TextBox => System.Windows.Controls.TextBox.TextProperty,
                ComboBox => Selector.SelectedItemProperty,
                _ => throw new NotSupportedException($"不支持的控件类型: {typeof(T)}")
            };

            control.SetBinding(targetProperty, binding);
            return control;
        }

        /// <summary>
        /// 从表达式提取属性路径
        /// </summary>
        private static string GetPath(Expression expression)
        {
            var path = new List<string>();
            var current = expression is LambdaExpression lambda ? lambda.Body : expression;

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
                else if (current is MethodCallExpression
                         {
                             Method.Name: "GetValue", Arguments:
                             [
                                 _, ConstantExpression
                                 {
                                     Value: string pathStr
                                 },
                                 ..
                             ]
                         })
                {
                    // 处理 GetValue(x, "PropertyPath") 的情况
                    return pathStr;
                }
                else
                {
                    break;
                }

            return string.Join(".", path);
        }
    }

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
            return type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(decimal);
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
                    if (effectiveType == typeof(bool))
                    {
                        var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);
                        checkBox.Bind(options, propertySelector);
                        return checkBox;
                    }
                    break;
                case UIType.Slider:
                    if (IsNumericType(effectiveType))
                    {
                        // 转换propertySelector为double类型
                        var doublePropertySelector = Expression.Lambda<Func<T, double>>(
                            Expression.Convert(propertySelector.Body, typeof(double)), propertySelector.Parameters);
                        return (FrameworkElement)CreateEnhancedTemplatedSlider(options, doublePropertySelector, propertyInfo);
                    }
                    break;
                case UIType.NumberBox:
                    // 对于数字输入框，使用TextBox
                    var numberBox = SharedUIComponents.CreateStandardTextBox();
                    numberBox.Bind(options, propertySelector);
                    return numberBox;
                case UIType.Text:
                    // 对于文本，使用TextBox
                    var textBox = SharedUIComponents.CreateStandardTextBox();
                    textBox.Bind(options, propertySelector);
                    return textBox;
                case UIType.ComboBox:
                    if (effectiveType.IsEnum)
                    {
                        var comboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
                        if (!string.IsNullOrEmpty(tooltip)) ToolTipService.SetToolTip(comboBox, tooltip);

                        // 使用动态方法调用BindEnum
                        var bindEnumMethod = typeof(QuickBind).GetMethod("BindEnum");
                        var genericMethod = bindEnumMethod?.MakeGenericMethod(effectiveType);
                        if (genericMethod != null)
                        {
                            // 创建类型化的selector: x => (TEnum)propertySelector.Compile()((T)x)
                            var compiledSelector = propertySelector.Compile();
                            var param = Expression.Parameter(typeof(object), "x");
                            var castToT = Expression.Convert(param, typeof(T));
                            var callSelector = Expression.Call(
                                Expression.Constant(compiledSelector),
                                typeof(Func<T, object>).GetMethod("Invoke")!,
                                castToT);
                            var convertToEnum = Expression.Convert(callSelector, effectiveType);
                            var typedSelector = Expression.Lambda(
                                typeof(Func<,>).MakeGenericType(typeof(object), effectiveType),
                                convertToEnum, param);

                            genericMethod.Invoke(null, [comboBox, options, typedSelector]);
                        }
                        return comboBox;
                    }
                    break;
            }

            return new TextBlock { Text = $"Unsupported control type for {propertyInfo.Name}" };
        }

        /// <summary>
        /// 创建模板化的滑块控件（使用表达式，可选勾选框，可选字典映射）
        /// </summary>
        public static UIElement CreateTemplatedSlider<T>(T options, Expression<Func<T, double>> propertySelector,
            Expression<Func<T, object>>? checkPropertySelector = null, Dictionary<double, string>? valueDisplayMap = null) where T : class
        {
            var propertyInfo = GetPropertyInfoFromExpression(propertySelector);
            if (propertyInfo == null) return new TextBlock { Text = "Invalid property selector" };

            var checkPropertyInfo =
                checkPropertySelector != null ? GetPropertyInfoFromExpression(checkPropertySelector) : null;
            var checkEnabled = checkPropertyInfo != null;

            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var label = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (IsNumericType(propertyInfo.PropertyType))
            {
                // 转换propertySelector为double类型
                var doublePropertySelector = Expression.Lambda<Func<T, double>>(
                    Expression.Convert(propertySelector.Body, typeof(double)), propertySelector.Parameters);

                var slider = new SettingsSlider<T>
                {
                    LabelText = label,
                    TooltipText = tooltip ?? "",
                    Min = Convert.ToDouble(attr.Min ?? 0),
                    Max = Convert.ToDouble(attr.Max ?? 100),
                    TickFrequency = attr.TickFrequency ?? 1,
                    KeyboardStep = attr.KeyboardStep ?? 1,
                    Source = options,
                    PropertySelector = doublePropertySelector,
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
            var binding = new Binding(seedPath)
            {
                Source = dataContext,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            SeedTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);

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
            Expression<Func<T, double>> propertySelector,
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

            var label = GetLocalizedString(attr.LabelKey);
            var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

            if (IsNumericType(propertyInfo.PropertyType))
            {
                // 转换propertySelector为double类型
                var doublePropertySelector = Expression.Lambda<Func<T, double>>(
                    Expression.Convert(propertySelector.Body, typeof(double)), propertySelector.Parameters);

                var slider = new SettingsSlider<T>
                {
                    LabelText = label,
                    TooltipText = tooltip ?? "",
                    Min = Convert.ToDouble(attr.Min ?? 0),
                    Max = Convert.ToDouble(attr.Max ?? 100), // 初始最大值，会被动态绑定覆盖
                    TickFrequency = attr.TickFrequency ?? 1,
                    KeyboardStep = attr.KeyboardStep ?? 1,
                    Source = options,
                    PropertySelector = doublePropertySelector,
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
        private static UIElement CreateEnhancedTemplatedSlider<T>(T options, Expression<Func<T, double>> propertySelector, PropertyInfo propertyInfo) where T : class
        {
            var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
            if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

            var label = GetLocalizedString(attr.LabelKey);
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

            if (IsNumericType(propertyInfo.PropertyType))
            {
                var slider = new SettingsSlider<T>
                {
                    LabelText = label,
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