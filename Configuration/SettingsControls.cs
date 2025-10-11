using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using krrTools.Bindable;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Configuration
{
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
        public Expression<Func<TDataContext, object>>? PropertySelector { get; init; }
        public bool CheckEnabled { get; init; }

        public Dictionary<double, string>? ValueDisplayMap { get; init; }

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
            SetupDynamicBindings(); // 动态绑定必须在最后设置，以覆盖静态属性

            if (Source != null && PropertySelector != null) SetupSourceBinding();
        }

        private void InitializeCheckBox()
        {
            if (CheckEnabled)
            {
                CheckBox = new CheckBox { Margin = new Thickness(0, 0, 5, 0), IsChecked = false };
                InnerSlider.IsEnabled = false;

                // 检查属性是否是可空类型
                bool isNullableType = false;
                if (PropertySelector != null)
                {
                    var propInfo = GetPropertyInfoFromExpression(PropertySelector);
                    isNullableType = propInfo != null && 
                        propInfo.PropertyType.IsGenericType && 
                        propInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>) &&
                        Nullable.GetUnderlyingType(propInfo.PropertyType.GetGenericArguments()[0]) != null;
                }

                CheckBox.Checked += (_, _) =>
                {
                    InnerSlider.IsEnabled = true;
                    if (isNullableType && Source != null && PropertySelector != null)
                    {
                        // 对于可空类型，勾选时设置为默认值
                        SetNullableValue(true);
                    }
                    UpdateLabelWithValue(InnerSlider.Value);
                };
                CheckBox.Unchecked += (_, _) =>
                {
                    InnerSlider.IsEnabled = false;
                    if (isNullableType && Source != null && PropertySelector != null)
                    {
                        // 对于可空类型，未勾选时设置为null
                        SetNullableValue(false);
                    }
                    UpdateLabelWithValue(InnerSlider.Value);
                };
            }
        }

        private void SetNullableValue(bool isChecked)
        {
            if (Source == null || PropertySelector == null) return;

            try
            {
                var propertyInfo = GetPropertyInfoFromExpression(PropertySelector);
                if (propertyInfo == null) return;

                var bindableProperty = propertyInfo.GetValue(Source);
                if (bindableProperty == null) return;

                var valueProperty = bindableProperty.GetType().GetProperty("Value");
                if (valueProperty == null) return;

                if (isChecked)
                {
                    // 勾选时设置为滑条的当前值或默认值
                    var defaultValue = InnerSlider.Value;
                    valueProperty.SetValue(bindableProperty, defaultValue);
                }
                else
                {
                    // 未勾选时设置为null
                    valueProperty.SetValue(bindableProperty, null);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[SettingsControls] SetNullableValue error: {0}", ex.Message);
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

        private void SetupDynamicBindings()
        {
            // 设置动态最大值绑定
            if (DynamicMaxSource != null && !string.IsNullOrEmpty(DynamicMaxPath))
            {
                Console.WriteLine(
                    $"[SettingsControls] Setting up dynamic max binding: {DynamicMaxPath}, Source: {DynamicMaxSource}");
                var maxBinding = new Binding(DynamicMaxPath)
                {
                    Source = DynamicMaxSource,
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                InnerSlider.SetBinding(RangeBase.MaximumProperty, maxBinding);

                // 监听动态最大值的变化
                if (DynamicMaxSource is INotifyPropertyChanged notifier)
                    notifier.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == DynamicMaxPath)
                            Dispatcher.Invoke(() =>
                            {
                                var newMax = GetDynamicMaxValue();
                                InnerSlider.Maximum = newMax; // 直接设置最大值
                                // 确保当前值不超过新的最大值
                                if (InnerSlider.Value > newMax) InnerSlider.Value = newMax;
                            });
                    };
            }
            else
            {
                Console.WriteLine(
                    $"[SettingsControls] No dynamic max binding - Source: {DynamicMaxSource}, Path: {DynamicMaxPath}");
            }

            // 设置动态最小值绑定
            if (DynamicMinSource != null && !string.IsNullOrEmpty(DynamicMinPath))
            {
                Console.WriteLine(
                    $"[SettingsControls] Setting up dynamic min binding: {DynamicMinPath}, Source: {DynamicMinSource}");
                var minBinding = new Binding(DynamicMinPath)
                {
                    Source = DynamicMinSource,
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                InnerSlider.SetBinding(RangeBase.MinimumProperty, minBinding);

                // 监听动态最小值的变化
                if (DynamicMinSource is INotifyPropertyChanged notifier)
                    notifier.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == DynamicMinPath)
                            Dispatcher.Invoke(() =>
                            {
                                var newMin = GetDynamicMinValue();
                                InnerSlider.Minimum = newMin; // 直接设置最小值
                                // 确保当前值不低于新的最小值
                                if (InnerSlider.Value < newMin) InnerSlider.Value = newMin;
                            });
                    };
            }
        }

        private double GetDynamicMaxValue()
        {
            if (DynamicMaxSource != null && !string.IsNullOrEmpty(DynamicMaxPath))
            {
                var property = DynamicMaxSource.GetType().GetProperty(DynamicMaxPath);
                return property?.GetValue(DynamicMaxSource) is double value ? value : Max;
            }

            return Max;
        }

        private PropertyInfo? GetPropertyInfoFromExpression<T, TResult>(Expression<Func<T, TResult>> propertySelector)
        {
            var current = propertySelector.Body;

            // 处理可能的转换 (如 int -> double)
            if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary) current = unary.Operand;

            if (current is MemberExpression { Member: PropertyInfo propertyInfo }) return propertyInfo;

            return null;
        }

        private double GetDynamicMinValue()
        {
            if (DynamicMinSource != null && !string.IsNullOrEmpty(DynamicMinPath))
            {
                var property = DynamicMinSource.GetType().GetProperty(DynamicMinPath);
                return property?.GetValue(DynamicMinSource) is double value ? value : Min;
            }

            return Min;
        }

        private void SetupSourceBinding()
        {
            // 使用单向绑定显示值，debounce设置值
            try
            {
                var path = GetPropertyPathFromExpression(PropertySelector!);
                
                // 检查属性是否是 Bindable<T> 类型，如果是则添加 .Value
                if (Source != null && PropertySelector != null)
                {
                    var propertyInfo = GetPropertyInfoFromExpression(PropertySelector);
                    if (propertyInfo != null && propertyInfo.PropertyType.IsGenericType && 
                        propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                    {
                        path += ".Value";
                    }
                }
                
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


        private void UpdateLabelWithValue(double value)
        {
            if (!string.IsNullOrEmpty(_labelText))
            {
                bool isEnabled = !CheckEnabled || (CheckBox?.IsChecked ?? false);
                string displayValue;
                if (!isEnabled)
                {
                    displayValue = "off";
                }
                else if (ValueDisplayMap != null && ValueDisplayMap.TryGetValue(value, out var mappedValue))
                {
                    displayValue = mappedValue;
                }
                else
                {
                    displayValue = ((int)value).ToString();
                }

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
                if (Source != null && PropertySelector != null)
                {
                    // 直接设置属性值 - WPF绑定应该已经处理了双向同步
                    // 如果绑定不工作，可能是因为属性路径问题
                    var path = GetPropertyPathFromExpression(PropertySelector);
                    var propertyNames = path.Split('.');
                    
                    object? currentObject = Source;
                    for (int i = 0; i < propertyNames.Length - 1; i++)
                    {
                        var property = currentObject.GetType().GetProperty(propertyNames[i]);
                        currentObject = property?.GetValue(currentObject);
                        if (currentObject == null) break;
                    }
                    
                    if (currentObject != null)
                    {
                        var finalProperty = currentObject.GetType().GetProperty(propertyNames[^1]);
                        if (finalProperty != null)
                        {
                            // 根据属性类型转换值
                            object? convertedValue = finalProperty.PropertyType switch
                            {
                                { } t when t == typeof(int) => (int)_pendingValue,
                                { } t when t == typeof(float) => (float)_pendingValue,
                                { } t when t == typeof(decimal) => (decimal)_pendingValue,
                                { } t when t == typeof(double) => _pendingValue,
                                { } t when t == typeof(int?) => (int?)_pendingValue,
                                { } t when t == typeof(float?) => (float?)_pendingValue,
                                { } t when t == typeof(decimal?) => (decimal?)_pendingValue,
                                { } t when t == typeof(double?) => (double?)_pendingValue,
                                _ => _pendingValue
                            };
                            finalProperty.SetValue(currentObject, convertedValue);
                        }
                    }
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
    }
}