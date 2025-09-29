using krrTools.tools.DPtool;
using System.Windows.Controls;

namespace krrTools.tools.Shared
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
                var saved = OptionsManager.LoadOptions<TOptions>(_toolName, OptionsManager.ConfigFileName);
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
                OptionsManager.SaveOptions(_toolName, OptionsManager.ConfigFileName, optionsToSave);
            }
            catch
            {
                // Best-effort save; ignore errors
            }
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Auto-save when ViewModel properties change (not just Options)
            if (_autoSave && e.PropertyName != nameof(Options))
            {
                DoSaveOptions();
            }
        }

        private void OnOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
                var saved = OptionsManager.LoadOptions<TOptions>(_toolName, OptionsManager.ConfigFileName);
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
                OptionsManager.SaveOptions(_toolName, OptionsManager.ConfigFileName, optionsToSave);
            }
            catch
            {
                // Best-effort save; ignore errors
            }
        }
    }
}
