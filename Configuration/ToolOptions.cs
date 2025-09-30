using System.ComponentModel;
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
    }

    /// <summary>
    /// 基类，提供选项加载和保存功能
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolViewModelBase<TOptions> : ObservableObject where TOptions : class, IToolOptions, new()
    {
        private TOptions _options;
        private readonly object _toolEnum;
        private readonly bool _autoSave;

        protected ToolViewModelBase(object toolEnum, bool autoSave = true, TOptions? injectedOptions = null)
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
                // Best-effort load; ignore errors and keep defaults
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
        private readonly object _toolEnum;

        protected ToolControlBase(object toolEnum, TOptions? injectedOptions = null)
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
