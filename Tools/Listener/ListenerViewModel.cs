using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.Tools.Listener
{
    /// <summary>
    /// 响应式监听器ViewModel - 文件监听和谱面分析的核心控制器
    /// 核心功能：文件变更监听 + 重复处理防护 + 响应式状态管理
    /// </summary>
    public class ListenerViewModel : ReactiveViewModelBase
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
        private readonly IEventBus _eventBus;
        private Bindable<string> _currentOsuFilePath = new Bindable<string>(string.Empty);
        private System.Timers.Timer? _checkTimer; // 明确指定命名空间
#pragma warning disable CS0618 // IOsuMemoryReader is obsolete but kept for compatibility
        // 暂时不要处理
        private readonly IOsuMemoryReader _memoryReader;
#pragma warning restore CS0618
        private string _lastBeatmapId = string.Empty;

        // 智能检查相关
        private bool _wasOsuRunning;
        private int _consecutiveFailures;
        private int _invalidDataCount; // 用于计数无效数据，避免过度日志

        // 文件处理防重复机制 - 核心优化点
        private readonly ConcurrentDictionary<string, (DateTime lastProcessTime, string contentHash)> _processedFiles = new();
        private readonly TimeSpan _duplicateProcessWindow = TimeSpan.FromMilliseconds(500); // 500ms内相同文件不重复处理

        // 记忆路径、热键等配置
        public ListenerConfig Config { get; }

        public event EventHandler<BeatmapInfo>? BeatmapSelected;

        public RelayCommand ConvertCommand { get; }
        public RelayCommand BrowseCommand { get; }

        public void SetN2NCHotkey(string hotkey)
        {
            BaseOptionsManager.UpdateGlobalSettings(settings => settings.N2NCHotkey = hotkey);
        }

        public void SetDPHotkey(string hotkey)
        {
            BaseOptionsManager.UpdateGlobalSettings(settings => settings.DPHotkey = hotkey);
        }

        public void SetKRRLNHotkey(string hotkey)
        {
            BaseOptionsManager.UpdateGlobalSettings(settings => settings.KRRLNHotkey = hotkey);
        }

        public string WindowTitle = Strings.OSUListener.Localize();

        public ListenerFileInfo CurrentFileInfo { get; } = new();

        public ObservableCollection<ListenerFileInfo> CurrentFileInfoCollection { get; } = new();

        public string CurrentOsuFilePath
        {
            get => _currentOsuFilePath.Value;
            private set => _currentOsuFilePath.Value = value;
        }

        public ListenerViewModel(IEventBus? eventBus = null)
        {
            if (eventBus != null)
            {
                _eventBus = eventBus;
            }
            else
            {
                try
                {
                    _eventBus = App.Services.GetRequiredService<IEventBus>();
                }
                catch (Exception)
                {
                    // For testing purposes, create a mock event bus
                    _eventBus = new MockEventBus();
                }
            }

            // 初始化内存读取器
            // OsuMemoryReader is marked obsolete by the package; suppress the warning for the assignment
#pragma warning disable CS0618
            _memoryReader = OsuMemoryReader.Instance;
#pragma warning restore CS0618

            // 初始化配置对象并订阅变化
            Config = BaseOptionsManager.LoadModuleOptions<ListenerConfig>(ModuleEnum.Listener) ?? new ListenerConfig();

            // 初始化文件信息集合
            CurrentFileInfoCollection.Add(CurrentFileInfo);

            InitializeOsuMonitoring();

            ConvertCommand = new RelayCommand(() => { });
            BrowseCommand = new RelayCommand(SetSongsPath);

            // 连接Bindable属性的PropertyChanged事件到ViewModel的PropertyChanged事件
            _currentOsuFilePath.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CurrentOsuFilePath));
        }

        // 配置类：现在是一个可观察对象，便于集中订阅变化并保存
        public class ListenerConfig : ObservableObject
        {
            public string SongsPath { get; set; } = string.Empty;
            public string? N2NCHotkey { get; set; } = "Ctrl+Shift+N";
            public string? DPHotkey { get; set; } = "Ctrl+Shift+D";
            public string? KRRLNHotkey { get; set; } = "Ctrl+Shift+K";
        }

        // 监听文件信息类
        public class ListenerFileInfo : ObservableObject
        {
            private string _title = string.Empty;
            private string _artist = string.Empty;
            private string _creator = string.Empty;
            private string _version = string.Empty;
            private double _bpm;
            private double _od;
            private double _hp;
            private int _keys;
            private int _notesCount;
            private double _lnPercent;
            private string _status = "Monitoring...";
        
            // LV 分析相关属性
            private double _xxySR;
            private double _krrLV = -1;
            private double _ylsLV = -1;
            private double _maxKPS;
            private double _avgKPS;

            public string Title
            {
                get => _title;
                set => SetProperty(ref _title, value);
            }

            public string Artist
            {
                get => _artist;
                set => SetProperty(ref _artist, value);
            }

            public string Creator
            {
                get => _creator;
                set => SetProperty(ref _creator, value);
            }

            public string Version
            {
                get => _version;
                set => SetProperty(ref _version, value);
            }

            public double BPM
            {
                get => _bpm;
                set => SetProperty(ref _bpm, value);
            }

            public double OD
            {
                get => _od;
                set => SetProperty(ref _od, value);
            }

            public double HP
            {
                get => _hp;
                set => SetProperty(ref _hp, value);
            }

            public int Keys
            {
                get => _keys;
                set => SetProperty(ref _keys, value);
            }

            public int NotesCount
            {
                get => _notesCount;
                set => SetProperty(ref _notesCount, value);
            }

            public double LNPercent
            {
                get => _lnPercent;
                set => SetProperty(ref _lnPercent, value);
            }

            public string Status
            {
                get => _status;
                set => SetProperty(ref _status, value);
            }

            // LV 分析指标属性
            public double XxySR
            {
                get => _xxySR;
                set => SetProperty(ref _xxySR, value);
            }

            public double KrrLV
            {
                get => _krrLV;
                set => SetProperty(ref _krrLV, value);
            }

            public double YlsLV
            {
                get => _ylsLV;
                set => SetProperty(ref _ylsLV, value);
            }

            public double MaxKPS
            {
                get => _maxKPS;
                set => SetProperty(ref _maxKPS, value);
            }

            public double AvgKPS
            {
                get => _avgKPS;
                set => SetProperty(ref _avgKPS, value);
            }
        }

        // 简单数据类，用于传递谱面信息
        public class BeatmapInfo
        {
            public string FilePath { get; set; } = string.Empty;
            public string BackgroundImagePath { get; set; } = string.Empty;
        }

        private void InitializeOsuMonitoring()
        {
            try
            {
                // 设置定时检查
                SetupTimer();
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] InitializeOsuMonitoring failed: {0}", ex.Message);
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => { }));
            }
        }

        private void SetupTimer()
        {
            _checkTimer = new System.Timers.Timer(1000); // 设置为1秒间隔，减少系统负载
            _checkTimer.Elapsed += async (_, _) => await CheckOsuBeatmapAsync();
            _checkTimer.Start();
        }

        private async Task CheckOsuBeatmapAsync()
        {
            try
            {
                var osuProcesses = Process.GetProcessesByName("osu!");
                var isOsuRunning = osuProcesses.Length > 0;

                // 智能调整检查间隔
                if (!isOsuRunning)
                {
                    if (_wasOsuRunning)
                        // osu!刚退出，立即清理状态
                        Application.Current?.Dispatcher?.BeginInvoke(
                            new Action(() => { CurrentOsuFilePath = string.Empty; }));

                    // 进程未运行时增加检查间隔
                    if (_checkTimer is { Interval: < 2000 })
                        _checkTimer.Interval = Math.Min(_checkTimer.Interval * 1.5, 2000);
                    _wasOsuRunning = false;
                    return;
                }

                // 进程运行时重置为合理的检查间隔 (避免过于频繁)
                if (!_wasOsuRunning && _checkTimer != null) _checkTimer.Interval = 500;
                _wasOsuRunning = true;
                _consecutiveFailures = 0; // 重置失败计数

                // 指定客户端
                Process? selectedProcess = null;
                if (osuProcesses.Length == 1)
                    selectedProcess = osuProcesses[0];
                else
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var selectionWindow = new ProcessSelectionWindow(osuProcesses);
                        if (selectionWindow.ShowDialog() == true) selectedProcess = selectionWindow.SelectedProcess;
                    });

                // 如果还没有设置 SongsPath，尝试自动获取
                if (selectedProcess != null)
                    try
                    {
                        if (selectedProcess.MainModule?.FileName is { } exePath)
                        {
                            var osuDir = Path.GetDirectoryName(exePath);
                            if (osuDir != null && !osuDir.ToUpper().Contains("SYSTEM32"))
                            {
                                var songsPath = Path.Combine(osuDir, "Songs");
                                if (Directory.Exists(songsPath))
                                {
                                    if (songsPath != BaseOptionsManager.GetGlobalSettings().SongsPath)
                                    {
                                        BaseOptionsManager.UpdateGlobalSettings(settings => settings.SongsPath = songsPath);
                                        Console.WriteLine("[ListenerViewModel] 客户端: osu!, 进程ID: {0}, 加载Songs路径: {1}",
                                            selectedProcess.Id, songsPath);
                                    }
                                }
                                else
                                {
                                    Logger.WriteLine(LogLevel.Warning,
                                        "[ListenerViewModel] Songs path not found: {0}", songsPath);
                                    if ((string.IsNullOrEmpty(BaseOptionsManager.GetGlobalSettings().SongsPath) ||
                                         !Directory.Exists(BaseOptionsManager.GetGlobalSettings().SongsPath)) &&
                                        !_hasPromptedForSongsPath)
                                    {
                                        _hasPromptedForSongsPath = true;
                                        Application.Current.Dispatcher.Invoke(SetSongsPath);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to get songs path: {0}",
                            ex.Message);
                    }

                // 尝试读取当前谱面信息
                var beatmapFile = _memoryReader.GetOsuFileName();
                var mapFolderName = _memoryReader.GetMapFolderName();

                // 验证文件名有效性 - 防止无效或不完整的文件名
                bool isValidBeatmapFile = !string.IsNullOrEmpty(beatmapFile) && 
                                         beatmapFile.Length > 3 && 
                                         beatmapFile.EndsWith(".osu", StringComparison.OrdinalIgnoreCase);
            
                bool isValidMapFolder = !string.IsNullOrEmpty(mapFolderName) && 
                                       mapFolderName.Length > 1;

                if (!isValidBeatmapFile || !isValidMapFolder)
                {
                    _invalidDataCount++;
                
                    // 只在前几次或间隔记录日志，避免日志泛滥
                    if (_invalidDataCount <= 3 || _invalidDataCount % 50 == 0)
                    {
                        Logger.WriteLine(LogLevel.Debug, 
                            "[ListenerViewModel] Invalid data detected - BeatmapFile: '{0}' (length: {1}), MapFolder: '{2}' (length: {3})", 
                            beatmapFile ?? "null", beatmapFile?.Length ?? 0,
                            mapFolderName ?? "null", mapFolderName?.Length ?? 0);
                    }
                
                    // 如果数据无效，清空当前状态
                    if (!string.IsNullOrEmpty(_lastBeatmapId))
                    {
                        _lastBeatmapId = string.Empty;
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CurrentFileInfo.Status = "Monitoring...";
                        });
                    }
                    return;
                }

                // 重置无效数据计数
                _invalidDataCount = 0;

                // 检查是否是新的谱面文件
                if (_lastBeatmapId != beatmapFile)
                {
                    Logger.WriteLine(LogLevel.Information, 
                        "[ListenerViewModel] Processing new beatmap: {0} in folder {1}", beatmapFile, mapFolderName);
                    await ProcessBeatmapAsync(beatmapFile, mapFolderName);
                    _lastBeatmapId = beatmapFile;
                }
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] CheckOsuBeatmap failed ({0} consecutive): {1}",
                    _consecutiveFailures, ex.Message);

                // 连续失败过多时增加检查间隔
                if (_consecutiveFailures >= 5 && _checkTimer != null)
                {
                    _checkTimer.Interval = Math.Min(_checkTimer.Interval * 2, 5000);
                    Logger.WriteLine(LogLevel.Warning,
                        "[ListenerViewModel] Too many failures, slowing down checks to {0}ms",
                        _checkTimer.Interval);
                }

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => { }));
            }
        }

        private bool _hasPromptedForSongsPath; // 添加标志变量，确保只弹出一次 Songs 路径选择窗口

        public void SetSongsPath()
        {
            if (_hasPromptedForSongsPath) return; // 如果已经弹出过窗口，直接返回

            var dialog = new FolderBrowserDialog
            {
                SelectedPath = BaseOptionsManager.GetGlobalSettings().SongsPath,
                Description = "Please select the osu! Songs directory",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                BaseOptionsManager.UpdateGlobalSettings(settings => settings.SongsPath = dialog.SelectedPath);
                _hasPromptedForSongsPath = true; // 设置标志为 true，表示已经弹出过窗口
            }
        }
    
        /// <summary>
        /// 检查是否应该跳过重复处理 - 核心优化逻辑
        /// </summary>
        private bool ShouldSkipDuplicateProcessing(string filePath)
        {
            if (!File.Exists(filePath)) return true;
        
            var fileHash = GetFileHash(filePath);
            var currentTime = DateTime.Now;
        
            // 检查是否在时间窗口内已处理过相同文件
            if (_processedFiles.TryGetValue(fileHash, out var processInfo))
            {
                if (currentTime - processInfo.lastProcessTime < _duplicateProcessWindow)
                {
                    Logger.WriteLine(LogLevel.Debug, 
                        "[ListenerViewModel] 重复处理防护生效: {0} (上次处理: {1:HH:mm:ss.fff})", 
                        Path.GetFileName(filePath), processInfo.lastProcessTime);
                    return true;
                }
            }
        
            return false;
        }
    
        /// <summary>
        /// 记录文件已处理状态 - 防重复核心机制
        /// </summary>
        private void RecordFileProcessed(string filePath)
        {
            if (!File.Exists(filePath)) return;
        
            var fileHash = GetFileHash(filePath);
            var processInfo = (DateTime.Now, fileHash);
            _processedFiles.AddOrUpdate(fileHash, processInfo, (_, _) => processInfo);
        
            // 清理过期记录 - 保持内存使用合理
            CleanupExpiredRecords();
        }
    
        /// <summary>
        /// 获取文件哈希用于去重识别
        /// </summary>
        private string GetFileHash(string filePath)
        {
            try
            {
                // 使用文件路径+大小+修改时间作为简单哈希，避免完整文件读取
                var fileInfo = new FileInfo(filePath);
                return $"{filePath}|{fileInfo.Length}|{fileInfo.LastWriteTime:yyyyMMddHHmmss}";
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Warning, "[ListenerViewModel] 文件哈希计算失败: {0}", ex.Message);
                return filePath; // 降级为使用文件路径
            }
        }
    
        /// <summary>
        /// 清理过期的处理记录
        /// </summary>
        private void CleanupExpiredRecords()
        {
            var cutoffTime = DateTime.Now - _duplicateProcessWindow - TimeSpan.FromMinutes(1);
            var expiredKeys = new List<string>();
        
            foreach (var kvp in _processedFiles)
            {
                if (kvp.Value.lastProcessTime < cutoffTime)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredKeys)
            {
                _processedFiles.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 处理谱面文件 - 核心业务逻辑，包含重复处理防护
        /// </summary>
        private async Task ProcessBeatmapAsync(string beatmapFile, string mapFolderName)
        {
            await Task.Run(() =>
            {
                try
                {
                    var filePath = Path.Combine(BaseOptionsManager.GetGlobalSettings().SongsPath, mapFolderName, beatmapFile);
                
                    // 验证文件是否存在
                    if (!File.Exists(filePath))
                    {
                        Logger.WriteLine(LogLevel.Warning, "[ListenerViewModel] File not found: {0}", filePath);
                        return;
                    }
                
                    // 响应式防重复处理检查 - 核心优化点
                    if (ShouldSkipDuplicateProcessing(filePath))
                    {
                        Logger.WriteLine(LogLevel.Debug, "[ListenerViewModel] Skipping duplicate processing: {0}", filePath);
                        return;
                    }
                
                    Logger.WriteLine(LogLevel.Debug, "[ListenerViewModel] Analyzing file: {0}", filePath);
                
                    // 使用 OsuAnalyzer 进行完整分析
                    var analyzer = new OsuAnalyzer();
                    OsuAnalysisResult analysisResult;
                
                    try 
                    {
                        analysisResult = analyzer.Analyze(filePath);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, 
                            "[ListenerViewModel] OsuAnalyzer.Analyze() failed for {0}: {1}", filePath, ex.Message);
                        throw new InvalidOperationException($"Analysis failed: {ex.Message}", ex);
                    }
                
                    // 调试：输出分析结果的原始数据
                    Logger.WriteLine(LogLevel.Debug, 
                        "[ListenerViewModel] Analysis result - XXY_SR: {0}, KRR_LV: {1}, Keys: {2}",
                        analysisResult.XXY_SR, analysisResult.KRR_LV, analysisResult.Keys);
                
                // 检查分析结果是否有效
                if (analysisResult.XXY_SR <= 0 || analysisResult.KRR_LV <= 0)
                {
                    string statusMessage = "Analysis failed";
                
                    // 特别检查是否是不支持的键数
                    if (analysisResult.Keys > 10)
                    {
                        statusMessage = $"Unsupported key count: {analysisResult.Keys}K (max 10K)";
                        Logger.WriteLine(LogLevel.Warning, 
                            "[ListenerViewModel] Unsupported key count for {0} - Keys: {1} (SRCalculator only supports 1-10K)",
                            filePath, analysisResult.Keys);
                    }
                    else
                    {
                        Logger.WriteLine(LogLevel.Warning, 
                            "[ListenerViewModel] Invalid analysis result for {0} - XXY_SR: {1}, KRR_LV: {2}",
                            filePath, analysisResult.XXY_SR, analysisResult.KRR_LV);
                    }
                
                    // 显示基本信息和错误状态
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CurrentFileInfo.Status = statusMessage;
                        CurrentFileInfo.Title = analysisResult.Title ?? "Unknown";
                        CurrentFileInfo.Artist = analysisResult.Artist ?? "Unknown";
                        CurrentFileInfo.Creator = analysisResult.Creator ?? "Unknown";
                        CurrentFileInfo.Version = analysisResult.Diff ?? "Unknown";
                        CurrentFileInfo.Keys = (int)analysisResult.Keys;
                        CurrentFileInfo.BPM = double.TryParse(analysisResult.BPMDisplay?.Replace(" BPM", ""), out var bpm) ? bpm : 0.0;
                        CurrentFileInfo.OD = analysisResult.OD;
                        CurrentFileInfo.HP = analysisResult.HP;
                        CurrentFileInfo.NotesCount = analysisResult.NotesCount;
                        CurrentFileInfo.LNPercent = analysisResult.LNPercent;
                        // LV 分析指标设为无效值
                        CurrentFileInfo.XxySR = -1;
                        CurrentFileInfo.KrrLV = -1;
                        CurrentFileInfo.YlsLV = double.NaN;
                        CurrentFileInfo.MaxKPS = analysisResult.MaxKPS;
                        CurrentFileInfo.AvgKPS = analysisResult.AvgKPS;
                    });
                    return;
                }                // 获取背景图片路径
                    var beatmap = BeatmapDecoder.Decode(filePath);
                
                    // 检查游戏模式
                    Logger.WriteLine(LogLevel.Debug, 
                        "[ListenerViewModel] Beatmap mode: {0}, CircleSize: {1}", 
                        beatmap.GeneralSection.Mode, beatmap.DifficultySection.CircleSize);
                
                    // 检查是否为 mania 模式 (ModeId = 3)
                    // 首先尝试通过 Ruleset 获取模式ID
                    bool isMania;
                    try
                    {
                        // 假设 Mode 是 Ruleset 类型，尝试获取其值
                        var modeValue = (int)beatmap.GeneralSection.Mode;
                        isMania = (modeValue == 3);
                    }
                    catch
                    {
                        // 如果转换失败，尝试字符串匹配
                        string modeString = beatmap.GeneralSection.Mode.ToString();
                        isMania = modeString.Contains("Mania", StringComparison.OrdinalIgnoreCase) || modeString.Contains("3");
                    }
                
                    if (!isMania)
                    {
                        Logger.WriteLine(LogLevel.Warning, 
                            "[ListenerViewModel] Beatmap {0} is not mania mode (Mode: {1})", 
                            filePath, beatmap.GeneralSection.Mode);
                    
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CurrentFileInfo.Status = $"Not Mania Mode ({beatmap.GeneralSection.Mode})";
                            CurrentFileInfo.Title = beatmap.MetadataSection.Title ?? "Unknown";
                            CurrentFileInfo.Artist = beatmap.MetadataSection.Artist ?? "Unknown";
                        });
                        return;
                    }
                
                    var bgPath = string.Empty;
                    var bg = beatmap.EventsSection.BackgroundImage;
                    if (!string.IsNullOrWhiteSpace(bg)) bgPath = Path.Combine(Path.GetDirectoryName(filePath)!, bg);

                    // 计算 YLS LV (基于 XXY SR)
                    var ylsLV = CalculateYlsLevel(analysisResult.XXY_SR);
                
                    // 记录处理状态 - 防重复机制核心
                    RecordFileProcessed(filePath);

                    var info = new BeatmapInfo
                    {
                        FilePath = filePath,
                        BackgroundImagePath = bgPath
                    };
                
                    // 响应式事件发布 - 统一事件管理
                    _eventBus.Publish(new FileChangedEvent 
                    { 
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        ChangeType = "BeatmapAnalyzed"
                    });

                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CurrentOsuFilePath = filePath;
                        BeatmapSelected?.Invoke(this, info);

                        // 更新当前文件信息 - 现在包含完整的 LV 分析数据
                        CurrentFileInfo.Title = analysisResult.Title ?? string.Empty;
                        CurrentFileInfo.Artist = analysisResult.Artist ?? string.Empty;
                        CurrentFileInfo.Creator = analysisResult.Creator ?? string.Empty;
                        CurrentFileInfo.Version = analysisResult.Diff ?? string.Empty;
                        CurrentFileInfo.BPM = double.TryParse(analysisResult.BPMDisplay?.Replace(" BPM", ""), out var bpmValue) ? bpmValue : 0.0;
                        CurrentFileInfo.OD = analysisResult.OD;
                        CurrentFileInfo.HP = analysisResult.HP;
                        CurrentFileInfo.Keys = (int)analysisResult.Keys;
                        CurrentFileInfo.NotesCount = analysisResult.NotesCount;
                        CurrentFileInfo.LNPercent = analysisResult.LNPercent;
                    
                        // LV 分析指标
                        CurrentFileInfo.XxySR = analysisResult.XXY_SR;
                        CurrentFileInfo.KrrLV = analysisResult.KRR_LV;
                        CurrentFileInfo.YlsLV = ylsLV;
                        CurrentFileInfo.MaxKPS = analysisResult.MaxKPS;
                        CurrentFileInfo.AvgKPS = analysisResult.AvgKPS;
                    
                        CurrentFileInfo.Status = "Analyzed";
                    
                        // 调试日志
                        Logger.WriteLine(LogLevel.Information, 
                            "[ListenerViewModel] Updated CurrentFileInfo: {0} - {1} | XXY_SR: {2}, KRR_LV: {3}, YLS_LV: {4}",
                            CurrentFileInfo.Artist, CurrentFileInfo.Title, 
                            CurrentFileInfo.XxySR, CurrentFileInfo.KrrLV, CurrentFileInfo.YlsLV);
                    });
                }
                catch (ArgumentException ex) when (ex.Message == "不是mania模式")
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CurrentFileInfo.Status = "Not Mania Mode";
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] ProcessBeatmapAsync failed: {0}", ex.Message);
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CurrentFileInfo.Status = $"Error: {ex.Message}";
                    });
                }
            });
        }

        public string? GetCurrentBeatmapFile()
        {
            return _memoryReader.GetOsuFileName();
        }

        public string? GetCurrentMapFolderName()
        {
            return _memoryReader.GetMapFolderName();
        }

        public void Cleanup()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }

        public async Task TriggerImmediateCheck()
        {
            await CheckOsuBeatmapAsync();
        }

        // 用于调试的方法 - 设置测试数据
        public void SetTestData()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CurrentFileInfo.Title = "Test Song Title";
                CurrentFileInfo.Artist = "Test Artist";
                CurrentFileInfo.Creator = "Test Creator";
                CurrentFileInfo.Version = "Test Difficulty";
                CurrentFileInfo.BPM = 180.0;
                CurrentFileInfo.OD = 8.5;
                CurrentFileInfo.HP = 7.0;
                CurrentFileInfo.Keys = 4;
                CurrentFileInfo.NotesCount = 1250;
                CurrentFileInfo.LNPercent = 0.15;
                CurrentFileInfo.XxySR = 5.67;
                CurrentFileInfo.KrrLV = 12.34;
                CurrentFileInfo.YlsLV = 8.91;
                CurrentFileInfo.MaxKPS = 15.5;
                CurrentFileInfo.AvgKPS = 8.2;
                CurrentFileInfo.Status = "Test Data";
            
                Logger.WriteLine(LogLevel.Information, "[ListenerViewModel] Test data set successfully!");
            });
        }



        private static double CalculateYlsLevel(double xxyStarRating)
        {
            const double LOWER_BOUND = 2.76257856739498;
            const double UPPER_BOUND = 10.5541834716376;

            if (xxyStarRating is >= LOWER_BOUND and <= UPPER_BOUND)
            {
                return FittingFormula(xxyStarRating);
            }

            if (xxyStarRating is < LOWER_BOUND and > 0)
            {
                return 3.6198 * xxyStarRating;
            }

            if (xxyStarRating is > UPPER_BOUND and < 12.3456789)
            {
                return (2.791 * xxyStarRating) + 0.5436;
            }

            return double.NaN;
        }

        private static double FittingFormula(double x)
        {
            // TODO: 实现正确的拟合公式
            // For now, returning a placeholder value
            return x * 1.5; // Replace with actual formula
        }
    }
}