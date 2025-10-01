using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public interface IPreviewOptionsProvider
    {
        IToolOptions GetPreviewOptions();
    }

    // 基类，实现了基本的选项加载和保存逻辑
    public abstract class ToolOptionsBase : ObservableObject, IToolOptions
    {
        /// <summary>
        /// Validate and normalize option values (called by UI or callers before use)
        /// Default implementation does nothing.
        /// </summary>
        public virtual void Validate() { }
    }

    /// <summary>
    /// 统一的工具选项基类，包含通用设置
    /// </summary>
    public abstract class UnifiedToolOptions : ToolOptionsBase
    {
        public PresetKind SelectedPreset { get; set; } = PresetKind.Default;

        public override void Validate()
        {
            var properties = GetType().GetProperties();
            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr != null)
                {
                    var value = prop.GetValue(this);
                    
                    if (value is IComparable comparable)
                    {
                        if (attr.Min != null && comparable.CompareTo(attr.Min) < 0)
                        {
                            prop.SetValue(this, attr.Min);
                        }
                        if (attr.Max != null && comparable.CompareTo(attr.Max) > 0)
                        {
                            prop.SetValue(this, attr.Max);
                        }
                    }
                }
            }
        }
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
    }

    /// <summary>
    /// UI控件类型枚举
    /// </summary>
    public enum UIType
    {
        Toggle, // CheckBox
        Slider, // Slider for numeric
        Text,   // TextBox for string
        ComboBox, // 下拉框
        NumberBox, // 数字输入框
        // 可根据需要添加更多类型
    }

    /// <summary>
    /// 基类，提供选项加载和保存功能
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolViewModelBase<TOptions> : ObservableObject where TOptions : class, IToolOptions, new()
    {
        private TOptions _options;
        private readonly ConverterEnum _toolEnum;
        private readonly bool _autoSave;

        protected ToolViewModelBase(ConverterEnum toolEnum, bool autoSave = true, TOptions? injectedOptions = null)
        {
            _toolEnum = toolEnum;
            _autoSave = autoSave;
            _options = injectedOptions ?? new TOptions();

            // Load options on initialization if not injected
            if (injectedOptions == null)
            {
                DoLoadOptions();
            }

            // Subscribe to property changes for auto-save if enabled
            if (_autoSave)
            {
                PropertyChanged += OnPropertyChanged;
                if (_options is ObservableObject observableOptions)
                {
                    observableOptions.PropertyChanged += OnOptionsPropertyChanged;
                }
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
                    // Unsubscribe from old options and subscribe to new ones
                    if (_autoSave)
                    {
                        if (_options is ObservableObject oldObservable && oldObservable != value)
                        {
                            oldObservable.PropertyChanged -= OnOptionsPropertyChanged;
                        }
                        if (value is ObservableObject newObservable)
                        {
                            newObservable.PropertyChanged += OnOptionsPropertyChanged;
                        }
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
                    Options = saved;
                }
            }
            catch
            {

            }
        }

        private void DoSaveOptions()
        {
            try
            {
                var optionsToSave = Options;
                optionsToSave.Validate();
                BaseOptionsManager.SaveOptions(_toolEnum, optionsToSave);
            }
            catch
            {
                // Best-effort save; ignore errors
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Auto-save when ViewModel properties change (not just Options)
            if (_autoSave && e.PropertyName != nameof(Options))
            {
                DoSaveOptions();
            }
        }

        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Auto-save when Options properties change
            if (_autoSave)
            {
                DoSaveOptions();
            }
        }
    }

    /// <summary>
    /// Unified base class for tool controls that need options management
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolControlBase<TOptions> : UserControl where TOptions : class, IToolOptions, new()
    {
        private readonly ConverterEnum _toolEnum;

        protected ToolControlBase(ConverterEnum toolEnum, TOptions? injectedOptions = null)
        {
            _toolEnum = toolEnum;
            Options = injectedOptions ?? new TOptions();

            // Load options on initialization if not injected
            if (injectedOptions == null)
            {
                DoLoadOptions();
            }
        }

        /// <summary>
        /// The options for this tool
        /// </summary>
        protected TOptions Options { get; private set; }

        private void DoLoadOptions()
        {
            try
            {
                var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
                if (saved != null)
                {
                    Options = saved;
                }
            }
            catch
            {
                // Best-effort load; ignore errors and keep defaults
            }
        }

        protected void DoSaveOptions()
        {
            try
            {
                var optionsToSave = Options;
                optionsToSave.Validate();
                BaseOptionsManager.SaveOptions(_toolEnum, optionsToSave);
            }
            catch
            {
                // Best-effort save; ignore errors
            }
        }

        /// <summary>
        /// 使用模板自动生成设置UI面板
        /// </summary>
        /// <returns>设置面板</returns>
        protected virtual StackPanel CreateTemplatedSettingsPanel()
        {
            return SettingsBinder.CreateSettingsPanel(Options);
        }
    }

    /// <summary>
    /// 预设类型枚举
    /// </summary>
    public enum PresetKind
    {
        [Description("Default|默认")]
        Default = 0,
        [Description("10K Preset|10K预设")]
        TenK = 1,
        [Description("8K Preset|8K预设")]
        EightK = 2,
        [Description("7K Preset|7K预设")]
        SevenK = 3
    }
}
