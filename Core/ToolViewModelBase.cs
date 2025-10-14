using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;

namespace krrTools.Core;


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

            // Subscribe to property changes for auto-save if enabled
            if (_autoSave && _options is ObservableObject observableOptions)
                observableOptions.PropertyChanged += OnOptionsPropertyChanged;

            // Initialize save timer for debouncing - 增加防抖时间
            if (_autoSave)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
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

                    BaseOptionsManager.SettingsChanged += OnSettingsChanged;

                    if (_autoSave)
                    {
                        PropertyChanged += OnPropertyChanged;
                    }
                
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
                    var toolOptions = saved as ToolOptionsBase;
                    toolOptions!.IsLoading = true;
                    saved.Validate();
                
                    // 临时禁用初始化状态来设置选项
                    var wasInitializing = _isInitializing;
                    _isInitializing = true;
                    Options = saved;
                    _isInitializing = wasInitializing;
                    toolOptions.IsLoading = false;
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
            // 同步响应式属性
            OnOptionsPropertyChangedInternal(e);
        
            // 触发ViewModel的PropertyChanged事件，以便UI（如预览）能监听到选项变化
            OnPropertyChanged(e.PropertyName);
            if (!_isInitializing && _autoSave) 
            {
                StartDelayedSave();
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
