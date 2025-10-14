using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using krrTools.Bindable;
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
                }
                else
                {
                    ProgressValue = current;
                    ProgressMaximum = total;
                }
            };
        }

        // UI 相关属性
        public PreviewViewDual? PreviewDual { get; set; }
        public Func<ConverterEnum>? GetActiveTabTag { get; set; }

        // 本地化
        private readonly DynamicLocalizedString _dropHintLocalized = new(Strings.DropHint);
        private readonly DynamicLocalizedString _dropFilesHintLocalized = new(Strings.DropFilesHint);

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
            // TODO: 需要梳理
            var oldSource = _currentSource;
            _stagedPaths = files;
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
                    PreviewDual.LoadPreview(_stagedPaths[0]);
                    
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
                    Console.WriteLine($"Failed to load preview for {_stagedPaths[0]}: {ex.Message}");
                }
            }
        }

        public void ConvertFiles()
        {
            Console.WriteLine($"[DEBUG] ConvertFiles called. _stagedPaths: {_stagedPaths?.Length ?? 0}, GetActiveTabTag: {GetActiveTabTag}");
            if (_stagedPaths is { Length: > 0 } && GetActiveTabTag != null)
            {
                var activeTab = GetActiveTabTag();
                Console.WriteLine($"[DEBUG] ActiveTabTag: {activeTab}");

                // 开始处理
                IsProcessing = true;
                ProgressValue = 0;
                ProgressMaximum = _stagedPaths.Length;

                _fileDispatcher.ActiveTabTag = activeTab;
                _fileDispatcher.ConvertFiles(_stagedPaths);
            }
            else
            {
                Console.WriteLine($"[DEBUG] ConvertFiles skipped. Has files: {_stagedPaths is { Length: > 0 }}, Has GetActiveTabTag: {GetActiveTabTag != null}");
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
                        Console.WriteLine($"[ERROR] Error accessing directory {item}: {ex.Message}");
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
                    FileSource.Dropped => "[拖入] ",
                    FileSource.Listened => "[监听] ",
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
            
            // 如果当前是监听状态且路径匹配，发布预览刷新事件
            if (e.FilePath == _stagedPaths?.FirstOrDefault())
            {
                EventBus.Publish(new PreviewRefreshEvent
                {

                });
            }
        }

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent e)
        {
            if (e.NewValue && _stagedPaths is { Length: > 0 })
            {
                SetSource(FileSource.Listened);
            }
        }
    }
}