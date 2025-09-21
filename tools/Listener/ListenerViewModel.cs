using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Diagnostics;
using System.Windows;
using OsuMemoryDataProvider;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Windows.Threading;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.tools.Listener
{
    public class ListenerViewModel : INotifyPropertyChanged
    {
        private string _currentOsuFilePath;
        private string _statusMessage;
        private System.Timers.Timer _checkTimer; // 明确指定命名空间
        private readonly IOsuMemoryReader _memoryReader;
        private string _lastBeatmapId = string.Empty;
        private string _songsPath;
        private string _bgPath;
        private string _windowTitle = "osu!Listener";
        private string _configPath;
        private string _hotkeyText = "Ctrl+Shift+Alt+X";
        private GlobalHotkey _globalHotkey;
        
        // 添加事件，用于通知主窗口热键已更改
        public event EventHandler HotkeyChanged;
        
        // 添加事件，用于通知DPTool有新的文件被选中
        public event EventHandler<string> BeatmapSelected;

        public void SetHotkey(string hotkey)
        {
            HotkeyText = hotkey;
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
    
            // 保存到配置
            SaveConfig();
        }
        
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }
        public string BGPath
        {
            get => _bgPath;
            set
            {
                _bgPath = value;
                OnPropertyChanged();
            }
        }
        public string HotkeyText
        {
            get => _hotkeyText;
            set
            {
                _hotkeyText = value;
                OnPropertyChanged();
            }
        }
        
        
        public string CurrentOsuFilePath
        {
            get => _currentOsuFilePath;
            set
            {
                _currentOsuFilePath = value;
                OnPropertyChanged();
                
                // 当文件路径改变时触发事件
                if (!string.IsNullOrEmpty(value))
                {
                    BeatmapSelected
                        ?.Invoke(this, value);
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string SongsPath
        {
            get => _songsPath;
            set
            {
                _songsPath = value;
                OnPropertyChanged();
            }
        }

        public ListenerViewModel()
        {
            // 初始化内存读取器
            _memoryReader = OsuMemoryReader.Instance;
            // 获取项目根目录并构建配置文件路径
            string projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(projectDirectory, "listenerConfig.fq");
            // 尝试加载已保存的配置
            LoadConfig();
            InitializeOsuMonitoring();
        }
        // 在 LoadConfig 方法中加载热键
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<ListenerConfig>(json);
                    if (!string.IsNullOrEmpty(config.SongsPath) && Directory.Exists(config.SongsPath))
                    {
                        SongsPath = config.SongsPath;
                    }
            
                    // 加载热键
                    if (!string.IsNullOrEmpty(config.Hotkey))
                    {
                        HotkeyText = config.Hotkey;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load config: {ex.Message}");
            }
        }
    
        public void SaveConfig()
        {
            try
            {
                var config = new ListenerConfig 
                { 
                    SongsPath = _songsPath,
                    Hotkey = _hotkeyText  // 添加这一行
                };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }
    
        // 配置类
        private class ListenerConfig
        {
            public string SongsPath { get; set; }
            public string Hotkey { get; set; }  // 添加这一行
        }
        
        private void InitializeOsuMonitoring()
        {
            try
            {
                StatusMessage = "Monitoring osu! song selection...";
                
                // 设置定时检查
                SetupTimer();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Initialization failed: {ex.Message}";
            }
        }

        private void SetupTimer()
        {
            _checkTimer = new System.Timers.Timer(1000); // 每秒检查一次
            _checkTimer.Elapsed += (sender, e) => CheckOsuBeatmap();
            _checkTimer.Start();
        }

        private void CheckOsuBeatmap()
        {
            try
            {
                // 检查osu!进程是否存在
                var osuProcesses = Process.GetProcessesByName("osu!");
                if (osuProcesses.Length == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "osu! is not running";
                        CurrentOsuFilePath = string.Empty;
                    });
                    return;
                }

                // 尝试读取当前谱面信息
                var beatmapFile = _memoryReader.GetOsuFileName();
                var mapFolderName = _memoryReader.GetMapFolderName();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(beatmapFile) && !string.IsNullOrEmpty(mapFolderName))
                    {
                        // 只有当谱面改变时才更新
                        if (_lastBeatmapId != beatmapFile)
                        {
                            // 构建完整的文件路径
                            if (!string.IsNullOrEmpty(SongsPath))
                            {
                                CurrentOsuFilePath = Path.Combine(SongsPath, mapFolderName, beatmapFile);

                                string Mes = $"Detected selected beatmap:\n{beatmapFile}\n" +
                                             "\n" + $"OD:{_memoryReader.GetMapOd()}" + 
                                             "\n" + $"HP:{_memoryReader.GetMapHp()}" + 
                                              "\n" + $"CS:{_memoryReader.GetMapCs()}";
                                
                                StatusMessage = Mes;
                                
                                Beatmap beatmap = BeatmapDecoder.Decode(CurrentOsuFilePath);
                                String BG = beatmap.EventsSection.BackgroundImage;
                                if (!string.IsNullOrWhiteSpace(BG))
                                {
                                    BGPath = Path.Combine(SongsPath, mapFolderName, BG);
                                }
                            }
                            else
                            {
                                CurrentOsuFilePath = Path.Combine(mapFolderName, beatmapFile);
                                StatusMessage = "Please set Songs directory for full path";
                            }
                            _lastBeatmapId = beatmapFile;
                        }
                    }
                    else
                    {
                        StatusMessage = "osu! is running, waiting for beatmap selection...";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Failed to read osu! memory: {ex.Message}";
                });
            }
        }

        public void SetSongsPath()
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Please select the osu! Songs directory",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                SongsPath = dialog.SelectedPath;
                StatusMessage = $"Songs directory set: {SongsPath}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }
    }
}
