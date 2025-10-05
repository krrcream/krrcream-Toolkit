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
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools.Configuration;

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
    /// 双向绑定：control.Bind(source, x => x.Property) - 兼容旧版本
    /// </summary>
    public static T Bind<T>(this T control, object source, Expression<Func<object, object>> property)
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
    /// 枚举专用绑定：combo.BindEnum&lt;EnumType&gt;(source, x => x.Property)
    /// </summary>
    public static ComboBox BindEnum<TEnum>(this ComboBox combo, object source, Expression<Func<object, TEnum>> property)
        where TEnum : struct, Enum
    {
        var path = GetPath(property);
        combo.ItemsSource = Enum.GetValues(typeof(TEnum));
        var binding = new Binding(path)
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        combo.SetBinding(Selector.SelectedItemProperty, binding);
        return combo;
    }

    /// <summary>
    /// 从表达式提取属性路径
    /// </summary>
    private static string GetPath(System.Linq.Expressions.Expression expression)
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

        // 布尔类型 - 使用CheckBox
        if (propertyInfo.PropertyType == typeof(bool))
        {
            var checkBox = SharedUIComponents.CreateStandardCheckBox(label, tooltip);
            checkBox.Bind(options, propertySelector);
            return checkBox;
        }

        // 枚举类型 - 使用ComboBox
        if (propertyInfo.PropertyType.IsEnum)
        {
            var comboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            if (!string.IsNullOrEmpty(tooltip)) ToolTipService.SetToolTip(comboBox, tooltip);

            // 使用动态方法调用BindEnum
            var bindEnumMethod = typeof(QuickBind).GetMethod("BindEnum");
            var genericMethod = bindEnumMethod?.MakeGenericMethod(propertyInfo.PropertyType);
            if (genericMethod != null)
            {
                // 创建类型化的selector: x => (TEnum)propertySelector.Compile()((T)x)
                var compiledSelector = propertySelector.Compile();
                var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "x");
                var castToT = System.Linq.Expressions.Expression.Convert(param, typeof(T));
                var callSelector = System.Linq.Expressions.Expression.Call(
                    System.Linq.Expressions.Expression.Constant(compiledSelector),
                    typeof(Func<T, object>).GetMethod("Invoke")!,
                    castToT);
                var convertToEnum = System.Linq.Expressions.Expression.Convert(callSelector, propertyInfo.PropertyType);
                var typedSelector = System.Linq.Expressions.Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(typeof(object), propertyInfo.PropertyType),
                    convertToEnum, param);

                genericMethod.Invoke(null, [comboBox, options, typedSelector]);
            }

            return comboBox;
        }

        // 字符串和数值类型 - 使用TextBox
        if (propertyInfo.PropertyType == typeof(string) || IsNumericType(propertyInfo.PropertyType))
        {
            var textBox = SharedUIComponents.CreateStandardTextBox();
            textBox.Bind(options, propertySelector);
            return textBox;
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

        var attr = propertyInfo.GetCustomAttribute<OptionAttribute>();
        if (attr == null) return new TextBlock { Text = $"No OptionAttribute for {propertyInfo.Name}" };

        var label = GetLocalizedString(attr.LabelKey);
        var tooltip = string.IsNullOrEmpty(attr.TooltipKey) ? null : GetLocalizedString(attr.TooltipKey);

        if (IsNumericType(propertyInfo.PropertyType))
        {
            var slider = new SettingsSlider<T>
            {
                LabelText = label,
                TooltipText = tooltip ?? "",
                Min = Convert.ToDouble(attr.Min ?? 0),
                Max = Convert.ToDouble(attr.Max ?? 100),
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
    private static PropertyInfo? GetPropertyInfoFromExpression<T>(Expression<Func<T, object>> propertySelector)
    {
        var current = propertySelector.Body;

        // 处理可能的转换 (如 int -> object)
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
    public static FrameworkElement CreateSeedPanel(object dataContext, string seedPath = "Options.Seed")
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
}