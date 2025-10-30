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
using Expression = System.Linq.Expressions.Expression;

namespace krrTools.Configuration
{
    // 一个带标签和滑块的设置控件，支持双语标签和数据绑定
    public class SettingsSlider<TDataContext> : Grid where TDataContext : class
    {
        private TextBlock label { get; set; }
        private Slider innerSlider { get; }

        public object? DynamicMaxSource { get; init; }
        public string? DynamicMaxPath { get; init; }
        public object? DynamicMinSource { get; init; }
        public string? DynamicMinPath { get; init; }
        private CheckBox? checkBox { get; set; }

        private readonly DynamicLocalizedString? _labelTemplate;
        private double? _rememberedValue; // 记忆上一个有效值

        public Bindable<string> LabelText { get; set; } = new Bindable<string>(string.Empty);

        public string LabelKey
        {
            get => _labelTemplate?.Key ?? "";
            init
            {
                _labelTemplate = new DynamicLocalizedString(value);
                _labelTemplate.PropertyChanged += (_, _) => UpdateLabelWithValue(innerSlider.Value);
                UpdateLabelWithValue(innerSlider.Value);
                if (_isInitialized) UpdateLabelWithValue(innerSlider.Value);
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
            label = new TextBlock
            {
                FontSize = SharedUIComponents.HEADER_FONT_SIZE,
                FontWeight = FontWeights.Bold
            };
            innerSlider = new Slider();

            // 设置Grid布局
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 添加InnerSlider到第二行，跨越两列
            SetRow(innerSlider, 1);
            SetColumn(innerSlider, 0);
            SetColumnSpan(innerSlider, 2);
            Children.Add(innerSlider);

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

            Logger.WriteLine(LogLevel.Debug,
                             $"[SettingsControls] SettingsSlider_Loaded - DynamicMaxSource: {DynamicMaxSource}, DynamicMaxPath: {DynamicMaxPath}");

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
                // 检查属性是否是可空类型
                bool isNullableType = false;

                if (PropertySelector != null)
                {
                    PropertyInfo? propInfo = GetPropertyInfoFromExpression(PropertySelector);
                    isNullableType = propInfo != null &&
                                     propInfo.PropertyType.IsGenericType &&
                                     propInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>) &&
                                     Nullable.GetUnderlyingType(propInfo.PropertyType.GetGenericArguments()[0]) != null;
                }

                bool initialChecked = false;

                if (isNullableType && Source != null && PropertySelector != null)
                {
                    // 对于可空类型，根据当前值设置初始勾选状态
                    PropertyInfo? propInfo = GetPropertyInfoFromExpression(PropertySelector);

                    if (propInfo != null)
                    {
                        object? bindableValue = propInfo.GetValue(Source);

                        if (bindableValue != null)
                        {
                            PropertyInfo? valueProperty = bindableValue.GetType().GetProperty("Value");
                            object? currentValue = valueProperty?.GetValue(bindableValue);
                            initialChecked = currentValue != null;
                        }
                    }
                }

                checkBox = new CheckBox { Margin = new Thickness(0, 0, 5, 0), IsChecked = initialChecked };
                innerSlider.IsEnabled = initialChecked;

                checkBox.Checked += (_, _) =>
                {
                    innerSlider.IsEnabled = true;

                    if (isNullableType && Source != null && PropertySelector != null)
                    {
                        // 对于可空类型，勾选时设置为默认值
                        SetNullableValue(true);
                    }

                    UpdateLabelWithValue(innerSlider.Value);
                };
                checkBox.Unchecked += (_, _) =>
                {
                    innerSlider.IsEnabled = false;

                    if (isNullableType && Source != null && PropertySelector != null)
                    {
                        // 对于可空类型，未勾选时设置为null
                        SetNullableValue(false);
                    }

                    UpdateLabelWithValue(innerSlider.Value);
                };
            }
        }

        private void SetNullableValue(bool isChecked)
        {
            if (Source == null || PropertySelector == null) return;

            try
            {
                PropertyInfo? propertyInfo = GetPropertyInfoFromExpression(PropertySelector);
                if (propertyInfo == null) return;

                object? bindableProperty = propertyInfo.GetValue(Source);
                if (bindableProperty == null) return;

                PropertyInfo? valueProperty = bindableProperty.GetType().GetProperty("Value");
                if (valueProperty == null) return;

                if (isChecked)
                {
                    // 勾选时恢复记忆的值或设置为默认值
                    double valueToSet = _rememberedValue ?? innerSlider.Value;
                    valueProperty.SetValue(bindableProperty, valueToSet);
                    innerSlider.Value = valueToSet; // 同步滑条
                }
                else
                {
                    // 未勾选时记住当前值，然后设置为null
                    object? currentValue = valueProperty.GetValue(bindableProperty);
                    if (currentValue is double currentDouble) _rememberedValue = currentDouble;
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
            if (_labelTemplate != null && !string.IsNullOrEmpty(_labelTemplate.Value))
            {
                label = new TextBlock
                {
                    FontSize = SharedUIComponents.HEADER_FONT_SIZE, FontWeight = FontWeights.Bold,
                    // 直接绑定到LabelText而不是设置绑定
                    DataContext = LabelText
                };
                label.SetBinding(TextBlock.TextProperty, new Binding("Value"));
                SetRow(label, 0);
                SetColumn(label, 0);
                Children.Add(label);

                if (checkBox != null)
                {
                    SetRow(checkBox, 0);
                    SetColumn(checkBox, 1);
                    Children.Add(checkBox);
                }

                UpdateLabelWithValue(innerSlider.Value);
            }

            if (!string.IsNullOrEmpty(TooltipText))
                ToolTipService.SetToolTip(this, TooltipText);
        }

        private void InitializeSliderProperties()
        {
            innerSlider.Minimum = Min;
            innerSlider.Maximum = Max;
            innerSlider.LargeChange = 1.0;
            if (!double.IsNaN(TickFrequency)) innerSlider.TickFrequency = TickFrequency;
            innerSlider.SmallChange = KeyboardStep;
            innerSlider.IsSnapToTickEnabled = !double.IsNaN(TickFrequency);
        }

        private void SetupDynamicBindings()
        {
            // 设置动态最大值绑定
            if (DynamicMaxSource != null && !string.IsNullOrEmpty(DynamicMaxPath))
            {
                var maxBinding = new Binding(DynamicMaxPath)
                {
                    Source = DynamicMaxSource,
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                innerSlider.SetBinding(RangeBase.MaximumProperty, maxBinding);

                // 监听动态最大值的变化
                if (DynamicMaxSource is INotifyPropertyChanged notifier)
                {
                    notifier.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == DynamicMaxPath)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                double newMax = GetDynamicMaxValue();
                                innerSlider.Maximum = newMax; // 直接设置最大值
                                // 确保当前值不超过新的最大值
                                if (innerSlider.Value > newMax) innerSlider.Value = newMax;
                            });
                        }
                    };
                }
            }
            else
            {
                // 仅在调试时输出日志，避免生产环境日志污染，检查是否正确设置了动态绑定，没有定义动态绑定时不输出
                if (!string.IsNullOrEmpty(DynamicMaxPath))
                {
                    Logger.WriteLine(LogLevel.Debug,
                                     $"[SettingsControls] No dynamic max binding - Source: {DynamicMaxSource}, Path: {DynamicMaxPath}");
                }
            }

            // 设置动态最小值绑定
            if (DynamicMinSource != null && !string.IsNullOrEmpty(DynamicMinPath))
            {
                var minBinding = new Binding(DynamicMinPath)
                {
                    Source = DynamicMinSource,
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                innerSlider.SetBinding(RangeBase.MinimumProperty, minBinding);

                // 监听动态最小值的变化
                if (DynamicMinSource is INotifyPropertyChanged notifier)
                {
                    notifier.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == DynamicMinPath)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                double newMin = GetDynamicMinValue();
                                innerSlider.Minimum = newMin; // 直接设置最小值
                                // 确保当前值不低于新的最小值
                                if (innerSlider.Value < newMin) innerSlider.Value = newMin;
                            });
                        }
                    };
                }
            }
        }

        private double GetDynamicMaxValue()
        {
            if (DynamicMaxSource != null && !string.IsNullOrEmpty(DynamicMaxPath))
            {
                PropertyInfo? property = DynamicMaxSource.GetType().GetProperty(DynamicMaxPath);
                return property?.GetValue(DynamicMaxSource) is double value ? value : Max;
            }

            return Max;
        }

        private PropertyInfo? GetPropertyInfoFromExpression<T, TResult>(Expression<Func<T, TResult>> propertySelector)
        {
            Expression current = propertySelector.Body;

            // 处理可能的转换 (如 int -> double)
            if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary) current = unary.Operand;

            if (current is MemberExpression { Member: PropertyInfo propertyInfo }) return propertyInfo;

            return null;
        }

        private double GetDynamicMinValue()
        {
            if (DynamicMinSource != null && !string.IsNullOrEmpty(DynamicMinPath))
            {
                PropertyInfo? property = DynamicMinSource.GetType().GetProperty(DynamicMinPath);
                return property?.GetValue(DynamicMinSource) is double value ? value : Min;
            }

            return Min;
        }

        private void SetupSourceBinding()
        {
            // 使用单向绑定显示值，debounce设置值
            try
            {
                string path = GetPropertyPathFromExpression(PropertySelector!);

                // 检查属性是否是 Bindable<T> 类型，如果是则添加 .Value
                if (Source != null && PropertySelector != null)
                {
                    PropertyInfo? propertyInfo = GetPropertyInfoFromExpression(PropertySelector);
                    if (propertyInfo != null && propertyInfo.PropertyType.IsGenericType &&
                        propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                        path += ".Value";
                }

                // Logger.WriteLine(LogLevel.Debug,$"[SettingsControls] Binding to path: {path}, Source: {Source}");

                var binding = new Binding
                {
                    Path = new PropertyPath(path),
                    Source = Source,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                innerSlider.SetBinding(RangeBase.ValueProperty, binding);
                innerSlider.ValueChanged += (_, ev) =>
                {
                    if (CheckEnabled && checkBox?.IsChecked == false) return; // 如果有勾选框且未勾选，不更新源

                    _pendingValue = ev.NewValue;
                    _debounceTimer?.Stop();
                    _debounceTimer?.Start();
                    UpdateLabelWithValue(ev.NewValue);
                };
                UpdateLabelWithValue(innerSlider.Value);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[SettingsControls] SettingsSlider QuickBind error: {0}", ex.Message);
            }
        }

        private void UpdateLabelWithValue(double value)
        {
            if (_labelTemplate != null && !string.IsNullOrEmpty(_labelTemplate.Value))
            {
                bool isEnabled = !CheckEnabled || (checkBox?.IsChecked ?? false);
                string displayValue;
                if (!isEnabled)
                    displayValue = "off";
                else if (ValueDisplayMap != null && ValueDisplayMap.TryGetValue(value, out string? mappedValue))
                    displayValue = mappedValue;
                else
                    displayValue = ((int)value).ToString();

                if (_labelTemplate.Value.Contains("{0}"))
                    LabelText.Value = Strings.FormatLocalized(_labelTemplate.Value, displayValue);
                else
                {
                    string localizedLabel = _labelTemplate.Value;
                    LabelText.Value = localizedLabel + ": " + displayValue;
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
                    string path = GetPropertyPathFromExpression(PropertySelector);
                    string[] propertyNames = path.Split('.');

                    object? currentObject = Source;

                    for (int i = 0; i < propertyNames.Length - 1; i++)
                    {
                        PropertyInfo? property = currentObject.GetType().GetProperty(propertyNames[i]);
                        currentObject = property?.GetValue(currentObject);
                        if (currentObject == null) break;
                    }

                    if (currentObject != null)
                    {
                        PropertyInfo? finalProperty = currentObject.GetType().GetProperty(propertyNames[^1]);

                        if (finalProperty != null)
                        {
                            Type targetType;
                            object? targetObject;

                            // Check if the property is Bindable<T>
                            if (finalProperty.PropertyType.IsGenericType && finalProperty.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                            {
                                // For Bindable<T>, target is the Value property
                                targetType = finalProperty.PropertyType.GetGenericArguments()[0];
                                object? bindableInstance = finalProperty.GetValue(currentObject);
                                if (bindableInstance == null) return;

                                targetObject = bindableInstance;
                                finalProperty = bindableInstance.GetType().GetProperty("Value");
                                if (finalProperty == null) return;
                            }
                            else
                            {
                                targetType = finalProperty.PropertyType;
                                targetObject = currentObject;
                            }

                            // 根据属性类型转换值
                            object? convertedValue = targetType switch
                                                     {
                                                         { } t when t == typeof(int)      => (int)_pendingValue,
                                                         { } t when t == typeof(float)    => (float)_pendingValue,
                                                         { } t when t == typeof(decimal)  => (decimal)_pendingValue,
                                                         { } t when t == typeof(double)   => _pendingValue,
                                                         { } t when t == typeof(int?)     => (int?)_pendingValue,
                                                         { } t when t == typeof(float?)   => (float?)_pendingValue,
                                                         { } t when t == typeof(decimal?) => (decimal?)_pendingValue,
                                                         { } t when t == typeof(double?)  => (double?)_pendingValue,
                                                         _                                => _pendingValue
                                                     };

                            finalProperty.SetValue(targetObject, convertedValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[SettingsControls] OnDebounceTimerTick error: {0}", ex.Message);
            }
        }

        private void SettingsSlider_Unloaded(object? sender, RoutedEventArgs e)
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
        }

        private void SettingsSlider_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isEnabled)
            {
                Opacity = isEnabled ? 1.0 : 0.5;
                IsHitTestVisible = isEnabled;
            }
        }

        private void OnLanguageChanged()
        {
            // DynamicLocalizedString will automatically update its Value when language changes
            // This will trigger the PropertyChanged event which calls UpdateLabelText
            if (_isInitialized)
                UpdateLabelWithValue(innerSlider.Value);
        }

        /// <summary>
        /// 从lambda表达式获取属性路径
        /// </summary>
        private static string GetPropertyPathFromExpression<T, TResult>(Expression<Func<T, TResult>> propertySelector)
        {
            var path = new List<string>();
            Expression? current = propertySelector.Body;

            while (current != null)
            {
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
                    break;
            }

            return string.Join(".", path);
        }
    }
}
