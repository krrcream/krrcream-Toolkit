using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using krrTools.Bindable;

namespace krrTools.Configuration
{
    // 工具选项接口，所有工具选项类必须实现此接口
    public interface IToolOptions
    {
        void Validate();
    }

    /// <summary>
    /// 预览选项提供者接口
    /// </summary>
    public interface IPreviewOptionsProvider;

    // 基类，实现了基本的选项加载和保存逻辑
    public abstract class ToolOptionsBase : ObservableObject, IToolOptions
    {
        protected bool IsValidating { get; set; }

        /// <summary>
        /// <summary>
        /// Validate and normalize option values (called by UI or callers before use)
        /// Default implementation clamps numeric properties based on OptionAttribute Min/Max.
        /// Supports both direct properties and Bindable<T> properties.
        /// </summary>
        public virtual void Validate()
        {
            if (IsValidating) return;
            IsValidating = true;
            try
            {
                var properties = GetType().GetProperties();
                foreach (var prop in properties)
                {
                    var attr = prop.GetCustomAttribute<OptionAttribute>();
                    if (attr == null || attr.Min == null || attr.Max == null) continue;

                    object value = GetPropertyValue(prop);
                    ClampNumericValue(prop, value, attr);
                }
            }
            finally
            {
                IsValidating = false;
            }
        }

        private object GetPropertyValue(PropertyInfo prop)
        {
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                var bindable = prop.GetValue(this);
                if (bindable == null) return null!;
                var valueProp = prop.PropertyType.GetProperty("Value");
                return valueProp?.GetValue(bindable) ?? null!;
            }
            return prop.GetValue(this) ?? null!;
        }

        private void SetPropertyValue(PropertyInfo prop, object value)
        {
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                var bindable = prop.GetValue(this);
                if (bindable == null) return;
                var valueProp = prop.PropertyType.GetProperty("Value");
                valueProp?.SetValue(bindable, value);
            }
            else
            {
                prop.SetValue(this, value);
            }
        }

        private void ClampNumericValue(PropertyInfo prop, object value, OptionAttribute attr)
        {
            if (value is int intValue)
            {
                var min = Convert.ToInt32(attr.Min);
                var max = Convert.ToInt32(attr.Max);
                var clamped = Math.Clamp(intValue, min, max);
                if (clamped != intValue)
                {
                    SetPropertyValue(prop, clamped);
                }
            }
            else if (value is double doubleValue)
            {
                var min = Convert.ToDouble(attr.Min);
                var max = Convert.ToDouble(attr.Max);
                var clamped = Math.Clamp(doubleValue, min, max);
                if (Math.Abs(clamped - doubleValue) > 1e-9)
                {
                    SetPropertyValue(prop, clamped);
                }
            }
        }

        /// <summary>
        /// Helper method to create Bindable<T> properties with validation
        /// </summary>
        protected Bindable<T> CreateBindable<T>(T defaultValue, Action<T>? onValueChanged = null)
        {
            var bindable = new Bindable<T>(defaultValue);
            bindable.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Bindable<T>.Value))
                {
                    OnPropertyChanged(); // Notify that this property changed
                    onValueChanged?.Invoke(bindable.Value);
                }
            };
            return bindable;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            // 设置变化时，通过UI或其他方式触发BaseOptionsManager.SaveOptions
            Console.WriteLine(!IsValidating
                ? $"[ToolOptions] Property changed: {e.PropertyName}"
                : $"[ToolOptions] Property changed during validation, not sending message");
        }

        public PresetKind SelectedPreset { get; init; } = PresetKind.Default;
    }

    /// <summary>
    /// 选项属性，用于定义选项的元数据
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OptionAttribute : Attribute
    {
        public string? LabelKey { get; set; } // Strings中的键，如 "DPModifyKeysCheckbox"
        public string? TooltipKey { get; set; } // Strings中的键，如 "DPModifyKeysTooltip"
        public object? DefaultValue { get; set; }
        public object? Min { get; set; }
        public object? Max { get; set; }
        public UIType UIType { get; set; } = UIType.Toggle;
        public Type? DataType { get; set; } // 数据类型，如 typeof(int), typeof(double) 等
        public double? TickFrequency { get; set; } = 1;
        public double? KeyboardStep { get; set; } = 1;
    
        // 统一滑条配置支持
        /// <summary>
        /// 自定义显示值映射的静态字段名称 (在同一类中)
        /// 例如："AlignValuesDict", "TransformSpeedSlotDict"
        /// </summary>
        public string? DisplayMapField { get; set; }
    
        /// <summary>
        /// 自定义实际值映射的静态字段名称 (在同一类中)
        /// 用于滑条值与实际值不同的情况
        /// </summary>
        public string? ActualMapField { get; set; }
    
        /// <summary>
        /// 是否启用勾选框 (对于滑条)
        /// </summary>
        public bool HasCheckBox { get; set; }
    
        /// <summary>
        /// 勾选框对应的属性名 (必须是bool类型，在同一类中)
        /// 例如："IsChecked", "IsEnabled"
        /// </summary>
        public string? CheckBoxProperty { get; set; }
    }

    /// <summary>
    /// UI控件类型枚举
    /// </summary>
    public enum UIType
    {
        Toggle, // CheckBox
        Slider, // Slider for numeric
        Text, // TextBox for string
        ComboBox, // 下拉框

        NumberBox // 数字输入框
        // 可根据需要添加更多类型
    }

    /// <summary>
    /// 基类，提供选项加载和保存功能
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class   ToolViewModelBase<TOptions> : ObservableObject, IDisposable where TOptions : class, IToolOptions, new()
    {
        private TOptions _options;
        private readonly ConverterEnum _toolEnum;
        private readonly bool _autoSave;
        private readonly DispatcherTimer? _saveTimer;

        private bool _isInitializing = true;
    
        protected readonly List<IDisposable> Disposables = new();

        protected ToolViewModelBase(ConverterEnum toolEnum, bool autoSave = true, TOptions? injectedOptions = null)
        {
            _toolEnum = toolEnum;
            _autoSave = autoSave;
            _options = injectedOptions ?? new TOptions();

            // Initialize save timer for debouncing - 增加防抖时间
            if (_autoSave)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _saveTimer.Tick += (_, _) =>
                {
                    _saveTimer.Stop();
                    if (!_isInitializing) 
                    {
                        try
                        {
                            var optionsToSave = Options;
                            optionsToSave.Validate();
                            BaseOptionsManager.SaveOptions(_toolEnum, optionsToSave);
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine(LogLevel.Error, $"[ToolOptions] Failed to save options for {_toolEnum}: {ex.Message}");
                            Console.WriteLine("[DEBUG] Failed to save options; changes may be lost.");
                        }
                    }
                };
            }

            // 延迟初始化，避免构造时的事件风暴
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                try
                {
                    // Load options on initialization if not injected
                    if (injectedOptions == null) DoLoadOptions();

                    // Subscribe to settings changes
                    BaseOptionsManager.SettingsChanged += OnSettingsChanged;

                    // Subscribe to property changes for auto-save if enabled
                    if (_autoSave)
                    {
                        PropertyChanged += OnPropertyChanged;
                        if (_options is ObservableObject observableOptions)
                            observableOptions.PropertyChanged += OnOptionsPropertyChanged;
                    }
                
                    // Setup reactive constraints
                    SetupReactiveConstraints();
                }
                finally
                {
                    _isInitializing = false;
                }
            }, DispatcherPriority.Background);
        }

        private void OnSettingsChanged(ConverterEnum changedConverter)
        {
            if (changedConverter == _toolEnum && !_isInitializing) 
            {
                DoLoadOptions();
            }
        }

        /// <summary>
        /// The options for this tool
        /// </summary>
        public TOptions Options
        {
            get => _options;
            set
            {
                if (SetProperty(ref _options, value))
                {
                    // Validate the new options
                    _options.Validate();
                    // Unsubscribe from old options and subscribe to new ones
                    if (_autoSave)
                    {
                        if (_options is ObservableObject oldObservable && oldObservable != value)
                            oldObservable.PropertyChanged -= OnOptionsPropertyChanged;
                        if (value is ObservableObject newObservable)
                            newObservable.PropertyChanged += OnOptionsPropertyChanged;
                    }
                }
            }
        }

        private void DoLoadOptions()
        {
            try
            {
                var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
                if (saved != null)
                {
                    saved.Validate();
                
                    // 临时禁用初始化状态来设置选项
                    var wasInitializing = _isInitializing;
                    _isInitializing = true;
                    Options = saved;
                    _isInitializing = wasInitializing;
                }
            }
            catch
            {
                Console.WriteLine("[DEBUG] Failed to load options; using defaults.");
            }
        }



        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_autoSave && e.PropertyName != nameof(Options) && !_isInitializing) 
            {
                StartDelayedSave();
                TriggerPreviewRefresh();
            }
        }

        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing) return;
        
            // 同步响应式属性
            OnOptionsPropertyChangedInternal(e);
        
            // 触发ViewModel的PropertyChanged事件，以便UI（如预览）能监听到选项变化
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Options)));
            if (_autoSave) 
            {
                StartDelayedSave();
                TriggerPreviewRefresh();
            }
        }
    
        /// <summary>
        /// 处理选项属性变化 - 子类可以重写此方法来同步响应式属性
        /// </summary>
        protected virtual void OnOptionsPropertyChangedInternal(PropertyChangedEventArgs e) { }
    
        /// <summary>
        /// 设置响应式约束 - 子类重写此方法来设置响应式属性和约束
        /// </summary>
        protected virtual void SetupReactiveConstraints() { }
    

    
        protected virtual void TriggerPreviewRefresh()
        {
            // 子类可以重写此方法来触发预览刷新
            // 严格禁止在这里传递或缓存beatmap对象
            // 只发送刷新信号，让预览组件自己重新加载数据
        }

        private void StartDelayedSave()
        {
            if (_saveTimer == null) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            foreach (var d in Disposables) d.Dispose();
            Disposables.Clear();
        }
    }

    /// <summary>
    /// Unified base class for tool controls that need options management
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolViewBase<TOptions> : UserControl where TOptions : class, IToolOptions, new()
    {
        private readonly ConverterEnum _toolEnum;

        protected ToolViewBase(ConverterEnum toolEnum, TOptions? injectedOptions = null)
        {
            _toolEnum = toolEnum;
            Options = injectedOptions ?? new TOptions();

            // Load options on initialization if not injected
            if (injectedOptions == null) DoLoadOptions();

            // 订阅设置变化事件
            BaseOptionsManager.SettingsChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(ConverterEnum changedConverter)
        {
            if (changedConverter == _toolEnum) DoLoadOptions();
        }

        /// <summary>
        /// The options for this tool
        /// </summary>
        protected TOptions Options { get; private set; }

        private void DoLoadOptions()
        {
            var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
            if (saved != null) Options = saved;
        }
    }

    /// <summary>
    /// 预设类型枚举
    /// </summary>
    public enum PresetKind
    {
        [Description("Default|默认")] Default = 0,
        [Description("10K Preset|10K预设")] TenK = 1,
        [Description("8K Preset|8K预设")] EightK = 2,
        [Description("7K Preset|7K预设")] SevenK = 3
    }
}