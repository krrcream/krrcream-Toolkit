using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview
{
    /// <summary>
    /// 预览响应式ViewModel - 谱面预览的响应式管理
    /// 核心功能：设置变更监听 + 智能刷新 + 事件驱动更新
    /// </summary>
    public class PreviewViewModel : ReactiveViewModelBase
    {
        // Mock event bus for testing
        private class MockEventBus : IEventBus
        {
            public void Publish<T>(T eventData) { }
            public IDisposable Subscribe<T>(Action<T> handler) => new MockDisposable();
            
            private class MockDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }
        // 响应式属性
        private Bindable<FrameworkElement?> _originalVisual = new Bindable<FrameworkElement?>();
        private Bindable<FrameworkElement?> _convertedVisual = new Bindable<FrameworkElement?>();
        private Bindable<string> _title = new Bindable<string>(string.Empty);
    
        private string? _beatmapPath;
        private bool _isRefreshing;
        private ConverterEnum? _currentTool;
        private Dictionary<string, object?> _changedSettings = new();
        private object? _currentViewModel;
        private PropertyChangedEventHandler? _optionsPropertyChangedHandler;

        public PreviewViewModel(IEventBus? eventBus = null)
        {
            IEventBus actualEventBus;
            if (eventBus != null)
            {
                actualEventBus = eventBus;
            }
            else
            {
                try
                {
                    actualEventBus = App.Services.GetRequiredService<IEventBus>();
                }
                catch (Exception)
                {
                    // For testing purposes, create a mock event bus
                    actualEventBus = new MockEventBus();
                }
            }
            
            // 连接Bindable属性的PropertyChanged事件到ViewModel的PropertyChanged事件
            _originalVisual.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OriginalVisual));
            _convertedVisual.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ConvertedVisual));
            _title.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
            
            var settingsSubscription = actualEventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
            Disposables.Add(settingsSubscription);
            
            var fileSubscription = actualEventBus.Subscribe<FileChangedEvent>(OnFileChanged);
            Disposables.Add(fileSubscription);
            
            var refreshSubscription = actualEventBus.Subscribe<PreviewRefreshEvent>(OnPreviewRefreshRequested);
            Disposables.Add(refreshSubscription);
        }

        // Parameterless constructor for testing
        public PreviewViewModel() : this(null) { }

        public FrameworkElement? OriginalVisual
        {
            get => _originalVisual.Value;
            private set => _originalVisual.Value = value;
        }

        /// <summary>
        /// 转换后谱面预览 - 响应式属性，线程安全
        /// </summary>
        public FrameworkElement? ConvertedVisual
        {
            get => _convertedVisual.Value;
            private set => _convertedVisual.Value = value;
        }

        /// <summary>
        /// 预览标题 - 响应式属性，线程安全
        /// </summary>
        public string Title
        {
            get => _title.Value;
            private set => _title.Value = value;
        }

        public IPreviewProcessor? Processor { get; private set; }

        /// <summary>
        /// 设置当前工具类型，用于统一日志输出
        /// </summary>
        public void SetCurrentTool(ConverterEnum? tool)
        {
            _currentTool = tool;
        }

        /// <summary>
        /// 设置当前ViewModel，用于监听设置变化
        /// </summary>
        public void SetCurrentViewModel(object? viewModel)
        {
            // 取消之前ViewModel的监听
            if (_currentViewModel is INotifyPropertyChanged oldNotify && _optionsPropertyChangedHandler != null)
            {
                oldNotify.PropertyChanged -= _optionsPropertyChangedHandler;
            }

            _currentViewModel = viewModel;

            // 如果新ViewModel有Options属性，监听其变化
            if (viewModel != null)
            {
                var optionsProperty = viewModel.GetType().GetProperty("Options");
                if (optionsProperty != null)
                {
                    var options = optionsProperty.GetValue(viewModel);
                    if (options is INotifyPropertyChanged notifyOptions)
                    {
                        _optionsPropertyChangedHandler = (sender, e) =>
                        {
                            if (e.PropertyName != null && sender != null)
                            {
                                // 获取属性的值
                                var property = sender.GetType().GetProperty(e.PropertyName);
                                if (property != null)
                                {
                                    var value = property.GetValue(sender);
                                    _changedSettings[e.PropertyName] = value;
                                }
                            }
                        };
                        notifyOptions.PropertyChanged += _optionsPropertyChangedHandler;
                    }
                }
            }
        }

        /// <summary>
        /// 输出当前工具的设置变化日志 - 统一格式：模块-设置-值
        /// </summary>
        private void LogCurrentSettings()
        {
            if (_currentTool == null || _changedSettings.Count == 0) return;

            var moduleName = _currentTool.Value switch
            {
                ConverterEnum.N2NC => "N2N",
                ConverterEnum.DP => "DP",
                ConverterEnum.KRRLN => "KRRLN",
                _ => "未知"
            };

            // 只输出发生变化的设置
            foreach (var (propertyName, value) in _changedSettings)
            {
                if (value != null)
                {
                    Console.WriteLine($"[{moduleName}模块]-{propertyName}-变更为{value}");
                }
            }

            // 打印后清除变化记录
            _changedSettings.Clear();
        }
    
        /// <summary>
        /// 响应设置变更事件 - 智能刷新逻辑
        /// </summary>
        private void OnSettingsChanged(SettingsChangedEvent settingsEvent)
        {
            if (_isRefreshing) return; // 防止递归刷新
        
            Console.WriteLine($"[PreviewViewModel] 收到设置变更: {settingsEvent.PropertyName} in {settingsEvent.SettingsType?.Name}");
        
            // 记录设置变化
            if (!string.IsNullOrEmpty(settingsEvent.PropertyName) && settingsEvent.NewValue != null)
            {
                _changedSettings[settingsEvent.PropertyName] = settingsEvent.NewValue;
            }
        
            // 只有影响预览的设置变更才刷新
            if (IsRelevantSettingChange(settingsEvent))
            {
                Console.WriteLine("[PreviewViewModel] 设置变更影响预览，触发刷新");
            
                // 确保在UI线程中执行刷新操作
                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    TriggerRefresh();
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(TriggerRefresh);
                }
            }
        }
    
        /// <summary>
        /// 响应文件变更事件 - 谱面分析完成后刷新
        /// </summary>
        private void OnFileChanged(FileChangedEvent fileEvent)
        {
            if (_isRefreshing) return;
        
            if (fileEvent.ChangeType == "BeatmapAnalyzed")
            {
                Console.WriteLine($"[PreviewViewModel] 收到谱面分析完成事件: {fileEvent.FileName}");
            
                // 如果是当前预览的文件，立即刷新 - 确保在UI线程执行
                if (!string.IsNullOrEmpty(_beatmapPath) && fileEvent.FilePath == _beatmapPath)
                {
                    Console.WriteLine("[PreviewViewModel] 当前预览文件已更新，立即刷新");
                
                    // 确保在UI线程中执行刷新操作
                    if (Application.Current?.Dispatcher.CheckAccess() == true)
                    {
                        ExecuteRefresh();
                    }
                    else
                    {
                        Application.Current?.Dispatcher.Invoke(ExecuteRefresh);
                    }
                }
            }
        }
    
        /// <summary>
        /// 响应预览刷新请求事件
        /// </summary>
        private void OnPreviewRefreshRequested(PreviewRefreshEvent refreshEvent)
        {
            if (_isRefreshing) return;
        
            Console.WriteLine($"[PreviewViewModel] 收到预览刷新请求: {refreshEvent.Reason}");
        
            // 确保在UI线程中执行刷新操作
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                if (refreshEvent.ForceRedraw)
                {
                    ExecuteRefresh();
                }
                else
                {
                    TriggerRefresh();
                }
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (refreshEvent.ForceRedraw)
                    {
                        ExecuteRefresh();
                    }
                    else
                    {
                        TriggerRefresh();
                    }
                });
            }
        }
    
        /// <summary>
        /// 判断设置变更是否影响预览显示
        /// </summary>
        private bool IsRelevantSettingChange(SettingsChangedEvent settingsEvent)
        {
            // 主要的预览相关设置
            var relevantProperties = new[]
            {
                "TargetKeys", "MaxKeys", "MinKeys", 
                "TransformSpeed", "Seed",
                "Pattern", "Density", "Style"
            };
        
            return Array.Exists(relevantProperties, prop => 
                settingsEvent.PropertyName?.Contains(prop) == true);
        }

        public void LoadFromPath(string path)
        {
            _beatmapPath = path;
            ExecuteRefresh();
        }

        public void LoadBuiltInSample()
        {
            _beatmapPath = null;
            ExecuteRefresh();
        }

        public void SetProcessor(IPreviewProcessor? processor)
        {
            var oldProcessor = Processor;
            Processor = processor;

            if (oldProcessor != processor) 
            {
                ExecuteRefresh();
            }
        }

        public void TriggerRefresh()
        {
            ExecuteRefresh();
        }

        /// <summary>
        /// 执行预览刷新 - 核心刷新逻辑，带防递归保护，异步执行避免阻塞UI
        /// </summary>
        internal async void ExecuteRefresh()
        {
            if (_isRefreshing) 
            {
                Console.WriteLine("[PreviewViewModel] 已在刷新中，跳过重复刷新");
                return;
            }
        
            _isRefreshing = true;

            // 在刷新开始时输出当前设置信息
            LogCurrentSettings();
        
            try
            {
                var startTime = DateTime.Now;
            
                // 在后台线程执行谱面解码
                Beatmap? beatmap = null;
            
                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(_beatmapPath))
                    {
                        try
                        {
                            beatmap = BeatmapDecoder.Decode(_beatmapPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[PreviewViewModel] 谱面解码失败: {ex.Message}");
                        }
                    }

                    beatmap ??= PreviewManiaNote.BuiltInSampleStream();
                });
            
                // 在UI线程创建UI组件
                FrameworkElement? originalVisual = null;
                FrameworkElement? convertedVisual = null;
            
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (beatmap != null)
                    {
                        originalVisual = Processor?.BuildOriginalVisual(beatmap);

                        if (Processor != null)
                        {
                            convertedVisual = Processor.BuildConvertedVisual(beatmap);
                        }
                    }
                });
            
                // 在UI线程更新属性
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OriginalVisual = originalVisual;
                    ConvertedVisual = convertedVisual;
                    if (beatmap != null)
                    {
                        UpdateTitle(beatmap);
                    }
                });
            
                var duration = DateTime.Now - startTime;
                Console.WriteLine($"[PreviewViewModel] 预览刷新完成，耗时: {duration.TotalMilliseconds:F1}ms");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void Reset()
        {
            _beatmapPath = null;
            OriginalVisual = null;
            ConvertedVisual = null;
            Title = string.Empty;
        }

        private void UpdateTitle(Beatmap beatmap)
        {
            if (beatmap.MetadataSection.Title == "Built-in Sample")
            {
                Title = "Built-in Sample";
            }
            else
            {
                var name = beatmap.GetOutputOsuFileName(true);
                Title = $"DIFF: {name}";
            }
        }
    }
}