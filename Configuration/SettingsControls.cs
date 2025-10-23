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

        private DynamicLocalizedString? _labelTemplate;
        private double? _rememberedValue; // 记忆上一个有效值

        public Bindable<string> LabelText { get; set; } = new(string.Empty);

        public string LabelKey
        {
            get => _labelTemplate?.Key ?? "";
            init
            {
                _labelTemplate = new DynamicLocalizedString(value);
                _labelTemplate.PropertyChanged += (_, _) => UpdateLabelWithValue(InnerSlider.Value);
                UpdateLabelWithValue(InnerSlider.Value);
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

                bool initialChecked = false;
                if (isNullableType && Source != null && PropertySelector != null)
                {
                    // 对于可空类型，根据当前值设置初始勾选状态
                    var propInfo = GetPropertyInfoFromExpression(PropertySelector);
                    if (propInfo != null)
                    {
                        var bindableValue = propInfo.GetValue(Source);
                        if (bindableValue != null)
                        {
                            var valueProperty = bindableValue.GetType().GetProperty("Value");
                            var currentValue = valueProperty?.GetValue(bindableValue);
                            initialChecked = currentValue != null;
                        }
                    }
                }

                CheckBox = new CheckBox { Margin = new Thickness(0, 0, 5, 0), IsChecked = initialChecked };
                InnerSlider.IsEnabled = initialChecked;

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
                    // 勾选时恢复记忆的值或设置为默认值
                    double valueToSet = _rememberedValue ?? InnerSlider.Value;
                    valueProperty.SetValue(bindableProperty, valueToSet);
                    InnerSlider.Value = valueToSet; // 同步滑条
                }
                else
                {
                    // 未勾选时记住当前值，然后设置为null
                    var currentValue = valueProperty.GetValue(bindableProperty);
                    if (currentValue is double currentDouble)
                    {
                        _rememberedValue = currentDouble;
                    }
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
                Label = new TextBlock { FontSize = SharedUIComponents.HeaderFontSize, FontWeight = FontWeights.Bold };
                // 直接绑定到LabelText而不是设置绑定
                Label.DataContext = LabelText;
                Label.SetBinding(TextBlock.TextProperty, new Binding("Value"));
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
                // 仅在调试时输出日志，避免生产环境日志污染，检查是否正确设置了动态绑定，没有定义动态绑定时不输出
                if (!string.IsNullOrEmpty(DynamicMaxPath))
                    Console.WriteLine(
                        $"[SettingsControls] No dynamic max binding - Source: {DynamicMaxSource}, Path: {DynamicMaxPath}");
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
                
                // Logger.WriteLine(LogLevel.Debug,$"[SettingsControls] Binding to path: {path}, Source: {Source}");
                
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
                    if (CheckEnabled && CheckBox?.IsChecked == false) return; // 如果有勾选框且未勾选，不更新源
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
            if (_labelTemplate != null && !string.IsNullOrEmpty(_labelTemplate.Value))
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

                if (_labelTemplate.Value.Contains("{0}"))
                {
                    LabelText.Value = Strings.FormatLocalized(_labelTemplate.Value, displayValue);
                }
                else
                {
                    var localizedLabel = _labelTemplate.Value;
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
                            Type targetType;
                            object? targetObject;
                            
                            // Check if the property is Bindable<T>
                            if (finalProperty.PropertyType.IsGenericType && finalProperty.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                            {
                                // For Bindable<T>, target is the Value property
                                targetType = finalProperty.PropertyType.GetGenericArguments()[0];
                                var bindableInstance = finalProperty.GetValue(currentObject);
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
                UpdateLabelWithValue(InnerSlider.Value);
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
