using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Bindable;
using Microsoft.Extensions.Logging;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.Tools.Preview;
using OsuParsers.Decoders;

namespace krrTools.Utilities
{
    public class FileDropZoneViewModel : INotifyPropertyChanged
    {
        private FileSource _currentSource = FileSource.None;
        private string[]? _stagedPaths;
        private string? _backgroundPath;
        private string? _lastLoadedFile;
        private bool _isSingleFile;

        private readonly FileDispatcher _fileDispatcher;

        // 公共属性注入事件总线，便于设置和获取
        [Inject]
        public IEventBus EventBus
        {
            get => _eventBus;
            set
            {
                _eventBus = value;
                // 在EventBus设置后订阅事件
                _eventBus.Subscribe<BeatmapChangedEvent>(OnBeatmapChanged);
                _eventBus.Subscribe<MonitoringEnabledChangedEvent>(OnMonitoringEnabledChanged);
            }
        }
        private IEventBus _eventBus = null!;

        // 依赖注入 - 构造函数
        public FileDropZoneViewModel(FileDispatcher fileDispatcher)
        {
            _fileDispatcher = fileDispatcher ?? throw new ArgumentNullException(nameof(fileDispatcher));

            // 设置进度更新回调
            _fileDispatcher.UpdateProgress = (current, total, text) =>
            {
                if (current == 0 && total == 100 && string.IsNullOrEmpty(text))
                {
                    // 重置状态
                    IsProcessing = false;
                    ProgressValue = 0;
                    ProgressMaximum = 100;
                    // 转换完成后清除拖拽状态，除单文件外
                    if (!_isSingleFile)
                    {
                        SetFiles(null);
                    }
                }
                else
                {
                    ProgressValue = current;
                    ProgressMaximum = total;
                }
            };

            // 初始化显示文字
            UpdateDisplayText();

            // 订阅语言切换事件
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        // UI 相关属性
        public PreviewViewDual? PreviewDual { get; set; }
        public Func<ConverterEnum>? GetActiveTabTag { get; set; }

        // 本地化
        private readonly DynamicLocalizedString _dropHintLocalized = new(Strings.DropHint);
        private readonly DynamicLocalizedString _dropFilesHintLocalized = new(Strings.DropFilesHint);
        private readonly DynamicLocalizedString _droppedPrefixLocalized = new(Strings.DroppedPrefix);
        private readonly DynamicLocalizedString _listenedPrefixLocalized = new(Strings.ListenedPrefix);

        // 属性
        private string _displayText = string.Empty;
        public string DisplayText
        {
            get => _displayText;
            private set => SetProperty(ref _displayText, value);
        }

        private bool _isConversionEnabled;
        public bool IsConversionEnabled
        {
            get => _isConversionEnabled;
            private set => SetProperty(ref _isConversionEnabled, value);
        }

        // 进度相关属性
        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                if (_progressValue != value)
                {
                    _progressValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressBrush));
                }
            }
        }

        private int _progressMaximum = 100;
        public int ProgressMaximum
        {
            get => _progressMaximum;
            set
            {
                if (_progressMaximum != value)
                {
                    _progressMaximum = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressBrush));
                }
            }
        }

        // 进度边框刷子
        public LinearGradientBrush ProgressBrush
        {
            get
            {
                double ratio = ProgressMaximum > 0 ? (double)ProgressValue / ProgressMaximum : 0;
                Color borderColor = Color.FromArgb(255, 175, 200, 255); // 正常边框色
                Color progressColor = Color.FromArgb(255, 0, 123, 255); // 进度高亮色

                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0) // 从左到右渐变
                };

                brush.GradientStops.Add(new GradientStop(progressColor, 0));
                brush.GradientStops.Add(new GradientStop(progressColor, ratio));
                brush.GradientStops.Add(new GradientStop(borderColor, ratio));
                brush.GradientStops.Add(new GradientStop(borderColor, 1));

                return brush;
            }
        }

        public event EventHandler<string[]>? FilesDropped;

        public void SetFiles(string[]? files, FileSource source = FileSource.Dropped)
        {
            var oldSource = _currentSource;
            _stagedPaths = files;
            _isSingleFile = files?.Length == 1;
            _backgroundPath = null; // Will be determined later
            _currentSource = files is { Length: > 0 } ? source : FileSource.None;
            
            // 发布状态变化事件
            EventBus.Publish(new FileSourceChangedEvent(oldSource, _currentSource, _stagedPaths));
            
            UpdateDisplayText();
            IsConversionEnabled = _stagedPaths is { Length: > 0 };
            LoadPreviewIfAvailable();
            FilesDropped?.Invoke(this, _stagedPaths ?? []);
        }

        private void LoadPreviewIfAvailable()
        {
            if (_stagedPaths is { Length: > 0 } && PreviewDual != null && _stagedPaths[0] != _lastLoadedFile)
            {
                _lastLoadedFile = _stagedPaths[0];
                try
                {
                    // 只在用户拖拽文件时主动加载预览，监听模式由PreviewViewModel处理
                    if (_currentSource == FileSource.Dropped)
                    {
                        PreviewDual.LoadPreview(_stagedPaths[0]);
                    }
                    
                    if (string.IsNullOrEmpty(_backgroundPath))
                    {
                        var beatmap = BeatmapDecoder.Decode(_stagedPaths[0]);
                        if (beatmap != null && !string.IsNullOrWhiteSpace(beatmap.EventsSection.BackgroundImage))
                        {
                            _backgroundPath = Path.Combine(Path.GetDirectoryName(_stagedPaths[0])!, beatmap.EventsSection.BackgroundImage);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(_backgroundPath))
                    {
                        PreviewDual.LoadBackgroundBrush(_backgroundPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "Failed to load preview for {0}: {1}", _stagedPaths[0], ex.Message);
                }
            }
        }

        public void ConvertFiles()
        {
            Logger.WriteLine(LogLevel.Debug, "[FileDropZone] ConvertFiles called. _stagedPaths: {0}, GetActiveTabTag: {1}", _stagedPaths?.Length ?? 0, GetActiveTabTag?.Invoke().ToString() ?? "null");
            if (_stagedPaths is { Length: > 0 } && GetActiveTabTag != null)
            {
                // 在UI层过滤非Mania谱面
                var filteredPaths = _stagedPaths.Where(BeatmapFileHelper.IsManiaBeatmap).ToArray();
                var skippedCount = _stagedPaths.Length - filteredPaths.Length;
                if (skippedCount > 0)
                {
                    Logger.WriteLine(LogLevel.Information, "[FileDropZone] UI层过滤跳过 {0} 个非Mania文件", skippedCount);
                }

                var activeTab = GetActiveTabTag();
                Logger.WriteLine(LogLevel.Debug, "[FileDropZone] ActiveTabTag: {0}", activeTab);

                // 开始处理
                IsProcessing = true;
                ProgressValue = 0;
                ProgressMaximum = filteredPaths.Length;

                _fileDispatcher.ActiveTabTag = activeTab;
                _fileDispatcher.ConvertFiles(filteredPaths);
            }
            else
            {
                Logger.WriteLine(LogLevel.Debug, "[FileDropZone] ConvertFiles skipped. Has files: {0}, Has GetActiveTabTag: {1}", _stagedPaths is { Length: > 0 }, GetActiveTabTag != null);
            }
        }

        public List<string> CollectOsuFiles(string[] items)
        {
            var osuFiles = new List<string>();
            foreach (var item in items)
            {
                if (File.Exists(item) && Path.GetExtension(item).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    osuFiles.Add(item);
                }
                else if (Directory.Exists(item))
                {
                    try
                    {
                        var found = Directory.GetFiles(item, "*.osu", SearchOption.AllDirectories);
                        osuFiles.AddRange(found);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[FileDropZone] Error accessing directory {0}: {1}", item, ex.Message);
                    }
                }
            }
            return osuFiles;
        }

        private void UpdateDisplayText()
        {
            if (_stagedPaths == null || _stagedPaths.Length == 0)
            {
                DisplayText = _dropHintLocalized.Value;
                _currentSource = FileSource.None;
            }
            else
            {
                string prefix = _currentSource switch
                {
                    FileSource.Dropped => _droppedPrefixLocalized.Value,
                    FileSource.Listened => _listenedPrefixLocalized.Value,
                    _ => ""
                };
                DisplayText = prefix + string.Format(_dropFilesHintLocalized.Value, _stagedPaths.Length);
            }
        }

        public void SetSource(FileSource source)
        {
            if (_stagedPaths is { Length: > 0 })
            {
                _currentSource = source;
                UpdateDisplayText();
            }
        }

        private void OnLanguageChanged()
        {
            UpdateDisplayText();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(propertyName);
        }

        // 事件处理方法
        private void OnBeatmapChanged(BeatmapChangedEvent e)
        {
            // 使用Dispatcher确保在UI线程上执行，避免跨线程访问错误
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 处理路径变化事件
                if (e.ChangeType == BeatmapChangeType.FromMonitoring)
                {
                    if (!string.IsNullOrEmpty(e.FilePath) && File.Exists(e.FilePath))
                    {
                        SetFiles([e.FilePath], FileSource.Listened);
                    }
                    return;
                }

                if (e.ChangeType == BeatmapChangeType.FromDropZone)
                {
                    if (!string.IsNullOrEmpty(e.FilePath) && File.Exists(e.FilePath))
                    {
                        SetFiles([e.FilePath], FileSource.Dropped);
                    }
                    return;
                }
                
                EventBus.Publish(new ConvPrevRefreshOnlyEvent
                {
                    NewValue = false
                });
            });
        }

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent e)
        {
            // 使用Dispatcher确保在UI线程上执行，避免跨线程访问错误
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.NewValue && _stagedPaths is { Length: > 0 })
                {
                    SetSource(FileSource.Listened);
                    EventBus.Publish(new ConvPrevRefreshOnlyEvent
                    {
                        NewValue = e.NewValue
                    });
                }
                else if (!e.NewValue)
                {
                    SetSource(FileSource.None);
                }
            });
        }
    }
}