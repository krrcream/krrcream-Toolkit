using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    // TODO: 过时？是否需要找一个新的内存读取方法替换？暂时不要处理
    private readonly IOsuMemoryReader _memoryReader;
#pragma warning restore CS0618
    private string _lastBeatmapId = string.Empty;
    private readonly string _configPath;

    // 支持集中保存配置变化
    private bool _suppressConfigSave;

    // 智能检查相关
    private bool _wasOsuRunning;
    private int _consecutiveFailures;

    // 记忆路径、热键等配置
    public ListenerConfig Config { get; }

    public event EventHandler? HotkeyChanged;

    public event EventHandler<BeatmapInfo>? BeatmapSelected;

    public RelayCommand ConvertCommand { get; }
    public RelayCommand BrowseCommand { get; }

    public void SetHotkey(string hotkey)
    {
        Config.Hotkey = hotkey;
    }

    public string WindowTitle = Strings.OSUListener.Localize();

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
        Config = new ListenerConfig();
        Config.PropertyChanged += Config_PropertyChanged;

        // 获取项目根目录并构建配置文件路径
        var projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(projectDirectory, "listenerConfig.fq");

        // 尝试加载已保存的配置
        LoadConfig();
        InitializeOsuMonitoring();

        ConvertCommand = new RelayCommand(() => { });
        BrowseCommand = new RelayCommand(SetSongsPath);
    }

    private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressConfigSave) return;

        // Persist all config changes centrally
        SaveConfig();

        // Raise HotkeyChanged when hotkey property changes
        if (e.PropertyName == nameof(ListenerConfig.Hotkey)) HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    // 在 LoadConfig 方法中加载热键
    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<ListenerConfig>(json);
                if (config != null)
                {
                    _suppressConfigSave = true;

                    Config.SongsPath = config.SongsPath;

                    if (!string.IsNullOrEmpty(config.Hotkey)) Config.Hotkey = config.Hotkey;

                    _suppressConfigSave = false;
                }
            }
        }
        catch (IOException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (IO): {0}", ex.Message);
        }
        catch (JsonException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (JSON): {0}", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (unauthorized): {0}",
                ex.Message);
        }
    }

    public void SaveConfig()
    {
        try
        {
            // Serialize the Config object directly
            var json = JsonSerializer.Serialize(Config);
            File.WriteAllText(_configPath, json);
        }
        catch (IOException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (IO): {0}", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (unauthorized): {0}",
                ex.Message);
        }
        catch (JsonException ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (JSON): {0}", ex.Message);
        }
    }

    // 配置类：现在是一个可观察对象，便于集中订阅变化并保存
    public class ListenerConfig : ObservableObject
    {
        public string SongsPath { get; set; } = string.Empty;
        public string? Hotkey { get; set; } = "Ctrl+Shift+Alt+X";
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
            bool isOsuRunning = osuProcesses.Length > 0;

            // 智能调整检查间隔
            if (!isOsuRunning)
            {
                if (_wasOsuRunning)
                {
                    // osu!刚退出，立即清理状态
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() => { CurrentOsuFilePath = string.Empty; }));
                }
                
                // 进程未运行时增加检查间隔
                if (_checkTimer is { Interval: < 2000 })
                {
                    _checkTimer.Interval = Math.Min(_checkTimer.Interval * 1.5, 2000);
                }
                _wasOsuRunning = false;
                return;
            }

            // 进程运行时重置为快速检查
            if (!_wasOsuRunning && _checkTimer != null)
            {
                _checkTimer.Interval = 150;
            }
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
                                if (songsPath != Config.SongsPath)
                                {
                                    Config.SongsPath = songsPath;
                                    Console.WriteLine("[ListenerViewModel] 客户端: osu!, 进程ID: {0}, 加载Songs路径: {1}",
                                        selectedProcess.Id, songsPath);
                                }
                            }
                            else
                            {
                                Logger.WriteLine(LogLevel.Warning,
                                    "[ListenerViewModel] Songs path not found: {0}", songsPath);
                                if ((string.IsNullOrEmpty(Config.SongsPath) || !Directory.Exists(Config.SongsPath)) &&
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

            if (!string.IsNullOrEmpty(beatmapFile) && !string.IsNullOrEmpty(mapFolderName) && _lastBeatmapId != beatmapFile)
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
                Logger.WriteLine(LogLevel.Warning, "[ListenerViewModel] Too many failures, slowing down checks to {0}ms", 
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
            SelectedPath = Config.SongsPath,
            Description = "Please select the osu! Songs directory",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            Config.SongsPath = dialog.SelectedPath;
            _hasPromptedForSongsPath = true; // 设置标志为 true，表示已经弹出过窗口
        }
    }

    private async Task ProcessBeatmapAsync(string beatmapFile, string mapFolderName)
    {
        await Task.Run(() =>
        {
            var filePath = Path.Combine(Config.SongsPath, mapFolderName, beatmapFile);
            var beatmap = BeatmapDecoder.Decode(filePath);
            var bgPath = string.Empty;
            var bg = beatmap.EventsSection.BackgroundImage;
            if (!string.IsNullOrWhiteSpace(bg))
            {
                bgPath = Path.Combine(Path.GetDirectoryName(filePath)!, bg);
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
            });
        });
    }

    public string? GetCurrentBeatmapFile() => _memoryReader.GetOsuFileName();
    public string? GetCurrentMapFolderName() => _memoryReader.GetMapFolderName();

    public void Cleanup()
    {
        _checkTimer?.Stop();
        _checkTimer?.Dispose();
    }
}