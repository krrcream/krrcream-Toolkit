using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using krrTools.Configuration;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.Tools.Listener;

public class ListenerViewModel : ObservableObject
{
    private string _currentOsuFilePath = string.Empty;
    private System.Timers.Timer? _checkTimer; // 明确指定命名空间
#pragma warning disable CS0618 // IOsuMemoryReader is obsolete but kept for compatibility
    // 暂时不要处理
    private readonly IOsuMemoryReader _memoryReader;
#pragma warning restore CS0618
    private string _lastBeatmapId = string.Empty;

    // 智能检查相关
    private bool _wasOsuRunning;
    private int _consecutiveFailures;

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
        get => _currentOsuFilePath;
        private set => SetProperty(ref _currentOsuFilePath, value);
    }

    public ListenerViewModel()
    {
        // 初始化内存读取器
        // OsuMemoryReader is marked obsolete by the package; suppress the warning for the assignment
#pragma warning disable CS0618
        _memoryReader = OsuMemoryReader.Instance;
#pragma warning restore CS0618

        // 初始化配置对象并订阅变化
        Config = BaseOptionsManager.LoadModuleOptions<ListenerConfig>(ModuleEnum.Listener) ?? new ListenerConfig();

        // 初始化文件信息集合
        CurrentFileInfoCollection.Add(CurrentFileInfo);

        // 尝试加载已保存的配置（已在上方完成）
        InitializeOsuMonitoring();

        ConvertCommand = new RelayCommand(() => { });
        BrowseCommand = new RelayCommand(SetSongsPath);
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
        _checkTimer = new System.Timers.Timer(150);
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

            // 进程运行时重置为快速检查
            if (!_wasOsuRunning && _checkTimer != null) _checkTimer.Interval = 150;
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

            if (!string.IsNullOrEmpty(beatmapFile) && !string.IsNullOrEmpty(mapFolderName) &&
                _lastBeatmapId != beatmapFile)
            {
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

    private async Task ProcessBeatmapAsync(string beatmapFile, string mapFolderName)
    {
        await Task.Run(() =>
        {
            var filePath = Path.Combine(BaseOptionsManager.GetGlobalSettings().SongsPath, mapFolderName, beatmapFile);
            var beatmap = BeatmapDecoder.Decode(filePath);
            var bgPath = string.Empty;
            var bg = beatmap.EventsSection.BackgroundImage;
            if (!string.IsNullOrWhiteSpace(bg)) bgPath = Path.Combine(Path.GetDirectoryName(filePath)!, bg);

            // 计算LN百分比 (暂时简化，稍后可以改进)
            var totalNotes = beatmap.HitObjects.Count;
            var lnPercent = 0.0;

            // 计算BPM
            var bpm = 0.0;
            if (beatmap.TimingPoints.Count > 0)
            {
                var firstTimingPoint = beatmap.TimingPoints[0];
                bpm = 60000.0 / firstTimingPoint.BeatLength;
            }

            var info = new BeatmapInfo
            {
                FilePath = filePath,
                BackgroundImagePath = bgPath
            };

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CurrentOsuFilePath = filePath;
                BeatmapSelected?.Invoke(this, info);

                // 更新当前文件信息
                CurrentFileInfo.Title = beatmap.MetadataSection.Title ?? string.Empty;
                CurrentFileInfo.Artist = beatmap.MetadataSection.Artist ?? string.Empty;
                CurrentFileInfo.Creator = beatmap.MetadataSection.Creator ?? string.Empty;
                CurrentFileInfo.Version = beatmap.MetadataSection.Version ?? string.Empty;
                CurrentFileInfo.BPM = bpm;
                CurrentFileInfo.OD = beatmap.DifficultySection.OverallDifficulty;
                CurrentFileInfo.HP = beatmap.DifficultySection.HPDrainRate;
                CurrentFileInfo.Keys = (int)beatmap.DifficultySection.CircleSize;
                CurrentFileInfo.NotesCount = totalNotes;
                CurrentFileInfo.LNPercent = lnPercent;
                CurrentFileInfo.Status = "Active";
            });
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
}