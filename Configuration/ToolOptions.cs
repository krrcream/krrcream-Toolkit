using System.ComponentModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace krrTools.Configuration
{
    // Interface for tool options to support unified handling across different tools
    public interface IToolOptions
    {
        void Validate();
    }

    // Base class for tool option objects that need validation/notifications
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
        /// <summary>
        /// 选中的预设
        /// </summary>
        public PresetKind SelectedPreset { get; set; } = PresetKind.Default;
    }

    /// <summary>
    /// Unified base class for tool ViewModels that need options management
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolViewModelBase<TOptions> : ObservableObject where TOptions : class, IToolOptions, new()
    {
        private TOptions _options = new TOptions();
        private readonly string _toolName;
        private readonly bool _autoSave;

        protected ToolViewModelBase(string toolName, bool autoSave = true)
        {
            _toolName = toolName;
            _autoSave = autoSave;

            // Load options on initialization
            DoLoadOptions();

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

        /// <summary>
        /// Load options from persistent storage
        /// </summary>
        public virtual void LoadOptions()
        {
            // Default implementation - can be overridden
        }

        /// <summary>
        /// Save options to persistent storage
        /// </summary>
        public virtual void SaveOptions()
        {
            // Default implementation - can be overridden
        }

        private void DoLoadOptions()
        {
            try
            {
                var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolName, BaseOptionsManager.ConfigFileName);
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
                BaseOptionsManager.SaveOptions(_toolName, BaseOptionsManager.ConfigFileName, optionsToSave);
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
    /// Options common to a single hand/side (used by DP tool to represent left/right)
    /// </summary>
    public class SideOptions : ToolOptionsBase
    {
        // 用户设置的默认值应该在ViewModel中定义
        public override void Validate()
        {
        }
    }

    /// <summary>
    /// Unified base class for tool controls that need options management
    /// </summary>
    /// <typeparam name="TOptions">The options type for this tool</typeparam>
    public abstract class ToolControlBase<TOptions> : UserControl where TOptions : class, IToolOptions, new()
    {
        private readonly string _toolName;

        protected ToolControlBase(string toolName)
        {
            _toolName = toolName;
            // Load options on initialization
            DoLoadOptions();
        }

        /// <summary>
        /// The options for this tool
        /// </summary>
        public TOptions Options { get; private set; } = new TOptions();

        /// <summary>
        /// Load options from persistent storage
        /// </summary>
        public virtual void LoadOptions()
        {
            // Default implementation - can be overridden
        }

        /// <summary>
        /// Save options to persistent storage
        /// </summary>
        public virtual void SaveOptions()
        {
            // Default implementation - can be overridden
        }

        private void DoLoadOptions()
        {
            try
            {
                var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolName, BaseOptionsManager.ConfigFileName);
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
                BaseOptionsManager.SaveOptions(_toolName, BaseOptionsManager.ConfigFileName, optionsToSave);
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
