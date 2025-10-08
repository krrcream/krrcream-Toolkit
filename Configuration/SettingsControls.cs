using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Configuration;

// 一个带标签和滑块的设置控件，支持双语标签和数据绑定
public class SettingsSlider<TDataContext> : Grid where TDataContext : class
{
    private TextBlock Label { get; set; }
    private Slider InnerSlider { get; }
    
    public object? DynamicMaxSource { get; init; }
    public string? DynamicMaxPath { get; init; }
    public object? DynamicMinSource { get; init; }
    public string? DynamicMinPath { get; init; }
    private CheckBox? CheckBox { get; set; }

    private readonly string _labelText = string.Empty;

    public string LabelText
    {
        get => _labelText;
        init
        {
            _labelText = value;
            if (_isInitialized) UpdateLabelWithValue(InnerSlider.Value);
        }
    }

    public string TooltipText { get; init; } = string.Empty;
    public TDataContext? Source { get; init; }
    public Expression<Func<TDataContext, double>>? PropertySelector { get; init; }
    public bool CheckEnabled { get; init; }

    public Dictionary<double, string>? ValueDisplayMap { get; init; }

    public IEnumSettingsProvider? EnumProvider { get; set; }
    public Enum? EnumKey { get; set; }
    public double Min { get; init; } = 1;
    public double Max { get; init; } = 100;
    public double TickFrequency { get; init; } = double.NaN;
    public double KeyboardStep { get; init; } = 1.0;

    private bool _isInitialized;
    private readonly DispatcherTimer? _debounceTimer;
    private double _pendingValue;

    public SettingsSlider()
    {
        Margin = new Thickness(0, 0, 0, 15);
        Label = new TextBlock
        {
            FontSize = SharedUIComponents.HeaderFontSize,
            FontWeight = FontWeights.Bold
        };
        InnerSlider = new Slider();

        // 设置Grid布局
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 添加InnerSlider到第二行，跨越两列
        SetRow(InnerSlider, 1);
        SetColumn(InnerSlider, 0);
        SetColumnSpan(InnerSlider, 2);
        Children.Add(InnerSlider);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) }; // 设置5ms的防抖间隔
        _debounceTimer.Tick += OnDebounceTimerTick;
        Loaded += SettingsSlider_Loaded;
        Unloaded += SettingsSlider_Unloaded;
        IsEnabledChanged += SettingsSlider_IsEnabledChanged;

        // Listen to language changes to update labels
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void SettingsSlider_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // Console.WriteLine(
        //     $"[SettingsControls] SettingsSlider_Loaded - DynamicMaxSource: {DynamicMaxSource}, DynamicMaxPath: {DynamicMaxPath}");

        InitializeCheckBox();
        InitializeLabel();
        InitializeSliderProperties();
        SetupDynamicBindings();

        if (EnumProvider != null && EnumKey != null)
            SetupEnumBinding();
        else if (Source != null && PropertySelector != null) SetupSourceBinding();
    }

    private void InitializeCheckBox()
    {
        if (CheckEnabled)
        {
            CheckBox = new CheckBox { Margin = new Thickness(0, 0, 5, 0), IsChecked = false };
            InnerSlider.IsEnabled = false;

            CheckBox.Checked += (_, _) =>
            {
                // Console.WriteLine("[SettingsControls] CheckBox checked, enabling slider");
                InnerSlider.IsEnabled = true;
            };
            CheckBox.Unchecked += (_, _) =>
            {
                // Console.WriteLine("[SettingsControls] CheckBox unchecked, disabling slider");
                InnerSlider.IsEnabled = false;
            };
        }
    }

    private void InitializeLabel()
    {
        if (!string.IsNullOrEmpty(LabelText))
        {
            Label = new TextBlock { FontSize = SharedUIComponents.HeaderFontSize, FontWeight = FontWeights.Bold };
            SetRow(Label, 0);
            SetColumn(Label, 0);
            Children.Add(Label);

            if (CheckBox != null)
            {
                SetRow(CheckBox, 0);
                SetColumn(CheckBox, 1);
                Children.Add(CheckBox);
            }

            UpdateLabelWithValue(InnerSlider.Value);
        }

        if (!string.IsNullOrEmpty(TooltipText))
            ToolTipService.SetToolTip(this, TooltipText);
    }

    private void InitializeSliderProperties()
    {
        InnerSlider.Minimum = Min;
        InnerSlider.Maximum = Max;
        InnerSlider.LargeChange = 1.0;
        if (!double.IsNaN(TickFrequency)) InnerSlider.TickFrequency = TickFrequency;
        InnerSlider.SmallChange = KeyboardStep;
        InnerSlider.IsSnapToTickEnabled = !double.IsNaN(TickFrequency);
    }

    /// <summary>
    /// 设置动态属性绑定（如动态最大值、最小值）
    /// </summary>
    private void SetupDynamicBindings()
    {
        // 设置动态最大值绑定
        if (DynamicMaxSource != null && !string.IsNullOrEmpty(DynamicMaxPath))
        {
            // Console.WriteLine(
            //     $"[SettingsControls] Setting up dynamic max binding: {DynamicMaxPath}, Source: {DynamicMaxSource}");
            var maxBinding = new Binding(DynamicMaxPath)
            {
                Source = DynamicMaxSource,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            InnerSlider.SetBinding(RangeBase.MaximumProperty, maxBinding);
        }
        else
        {
            // Console.WriteLine(
            //     $"[SettingsControls] No dynamic max binding - Source: {DynamicMaxSource}, Path: {DynamicMaxPath}");
        }

        // 设置动态最小值绑定
        if (DynamicMinSource != null && !string.IsNullOrEmpty(DynamicMinPath))
        {
            // Console.WriteLine(
            //     $"[SettingsControls] Setting up dynamic min binding: {DynamicMinPath}, Source: {DynamicMinSource}");
            var minBinding = new Binding(DynamicMinPath)
            {
                Source = DynamicMinSource,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            InnerSlider.SetBinding(RangeBase.MinimumProperty, minBinding);
        }
    }

    private void SetupEnumBinding()
    {
        try
        {
            var value = EnumProvider!.GetValue(EnumKey!);
            if (value != null)
            {
                var dv = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                SetInitialValue(dv);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider enum init error: {0}", ex.Message);
        }

        AddValueChangedListener();

        EnumProvider!.PropertyChanged += (_, ev) =>
        {
            if (ev.PropertyName == EnumKey!.ToString())
                UpdateFromProvider();
        };
    }

    private void SetupSourceBinding()
    {
        // 使用单向绑定显示值，debounce设置值
        try
        {
            var path = GetPropertyPathFromExpression(PropertySelector!);
            // Console.WriteLine($"[SettingsControls] Binding to path: {path}, Source: {Source}");
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                Source = Source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            InnerSlider.SetBinding(RangeBase.ValueProperty, binding);
            InnerSlider.ValueChanged += (_, ev) =>
            {
                _pendingValue = ev.NewValue;
                _debounceTimer?.Stop();
                _debounceTimer?.Start();
                UpdateLabelWithValue(ev.NewValue);
            };
            UpdateLabelWithValue(InnerSlider.Value);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider QuickBind error: {0}", ex.Message);
        }
    }

    private void SetInitialValue(double value)
    {
        InnerSlider.Value = value;
        UpdateLabelWithValue(value);
    }

    private void AddValueChangedListener()
    {
        InnerSlider.ValueChanged += (_, ev) =>
        {
            _pendingValue = ev.NewValue;
            _debounceTimer!.Stop();
            _debounceTimer.Start();
            UpdateLabelWithValue(ev.NewValue);
        };
    }

    private void UpdateFromProvider()
    {
        try
        {
            var v = EnumProvider!.GetValue(EnumKey!);
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
    }


    private void UpdateLabelWithValue(double value)
    {
        if (!string.IsNullOrEmpty(_labelText))
        {
            string displayValue;
            if (ValueDisplayMap != null && ValueDisplayMap.TryGetValue(value, out var mappedValue))
                displayValue = mappedValue;
            else
                displayValue = ((int)value).ToString();

            if (_labelText.Contains("{0}"))
            {
                Label.Text = Strings.FormatLocalized(_labelText, displayValue);
            }
            else
            {
                var localizedLabel = Strings.Localize(_labelText);
                Label.Text = localizedLabel + ": " + displayValue;
            }
        }
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer!.Stop();
        try
        {
            if (EnumProvider != null && EnumKey != null)
            {
                EnumProvider.SetValue(EnumKey, _pendingValue);
            }
            else if (Source != null && PropertySelector != null)
            {
                // 使用反射从lambda表达式中提取属性信息并设置值
                var propertyInfo = GetPropertyInfoFromExpression(PropertySelector);
                if (propertyInfo != null) propertyInfo.SetValue(Source, _pendingValue);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider debounce writeback error: {0}",
                ex.Message);
        }
    }

    private void SettingsSlider_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (CheckBox != null)
        {
            CheckBox.IsEnabled = IsEnabled;
            InnerSlider.IsEnabled = IsEnabled && (CheckBox.IsChecked ?? false);
        }
        else
        {
            InnerSlider.IsEnabled = IsEnabled;
        }
    }

    private void SettingsSlider_Unloaded(object? sender, RoutedEventArgs e)
    {
        _debounceTimer?.Stop();
    }

    private void OnLanguageChanged()
    {
        if (_isInitialized) UpdateLabelWithValue(InnerSlider.Value);
    }

    /// <summary>
    /// 从lambda表达式获取属性路径
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
    /// 从lambda表达式获取属性信息
    /// </summary>
    private static PropertyInfo? GetPropertyInfoFromExpression<T, TResult>(
        Expression<Func<T, TResult>> propertySelector)
    {
        var current = propertySelector.Body;

        if (current is MemberExpression { Member: PropertyInfo propertyInfo }) return propertyInfo;

        return null;
    }
}