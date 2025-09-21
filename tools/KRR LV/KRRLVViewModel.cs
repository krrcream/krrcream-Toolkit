using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using krrTools.Tools.KRRLV;
using krrTools.Tools.OsuParser;
using Microsoft.Win32;

namespace krrTools.tools.KRR_LV
{
    public class OsuFileItem : ObservableObject
    {
        private string? _fileName;
        private string? _filePath;
        private string? _status;
        private string? _diff;
        private string? _title;
        private string? _artist;
        private string? _creator;
        private string? _bpm;
        private double _keys;
        private double _od;
        private double _hp;
        private double _lnPercent;
        private double _beatmapID;
        private double _beatmapSetID;
        private double _xxySR;
        private double _krrLV;
        
        private readonly ProcessingWindow _processingWindow = new ProcessingWindow();
        private int _processedCount;
        private int _totalCount;
        
        public string? FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string? FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string? Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string? Diff
        {
            get => _diff;
            set => SetProperty(ref _diff, value);
        }
        
        public string? Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string? Creator
        {
            get => _creator;
            set => SetProperty(ref _creator, value);
        }

        public double Keys
        {
            get => _keys;
            set => SetProperty(ref _keys, value);
        }

        public string? BPM
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

        public double LNPercent
        {
            get => _lnPercent;
            set => SetProperty(ref _lnPercent, value);
        }

        public double BeatmapID
        {
            get => _beatmapID;
            set => SetProperty(ref _beatmapID, value);
        }

        public double BeatmapSetID
        {
            get => _beatmapSetID;
            set => SetProperty(ref _beatmapSetID, value);
        }
        
        public double XXYSR
        {
            get => _xxySR;
            set => SetProperty(ref _xxySR, value);
        }
    
        public double KRRLV
        {
            get => _krrLV;
            set => SetProperty(ref _krrLV, value);
        }
        
        public int ProcessedCount
        {
            get => _processedCount;
            set
            {
                _processedCount = value;
                OnPropertyChanged();
                // 更新进度条
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _processingWindow.UpdateProgress(value, _totalCount);
                });
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
            }
        }
    }
    
    public partial class KRRLVViewModel : ObservableObject, INotifyPropertyChanged
{
    [ObservableProperty]
    private string _pathInput = null!;

    [ObservableProperty]
    private ObservableCollection<OsuFileItem> _osuFiles = new ObservableCollection<OsuFileItem>();

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4, 4); // 最多4个并发线程
    private readonly DispatcherTimer _updateTimer;
    private ProcessingWindow? _processingWindow;
    private readonly List<OsuFileItem> _pendingItems = new List<OsuFileItem>();
    private readonly object _pendingItemsLock = new object();

    private int _totalCount;
    private int _processedCount;
    
    private int _currentProcessedCount;

    private int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public int ProcessedCount
    {
        get => _processedCount;
        set => SetProperty(ref _processedCount, value);
    }
    
    // protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    // {
    //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    // }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public KRRLVViewModel()
    {
        // 初始化定时器，每100毫秒批量更新一次UI
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        List<OsuFileItem> itemsToAdd;
        lock (_pendingItemsLock)
        {
            if (_pendingItems.Count == 0) return;
            itemsToAdd = new List<OsuFileItem>(_pendingItems);
            _pendingItems.Clear();
        }
        
        foreach (var item in itemsToAdd)
        {
            OsuFiles.Add(item);
        }
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择文件或文件夹",
            CheckFileExists = false, // 允许选择文件夹
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            PathInput = dialog.FileName;
            ProcessDroppedFiles([dialog.FileName]);
        }
    }

    [RelayCommand]
    private void Save()
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存为CSV文件",
            Filter = "CSV文件|*.csv",
            DefaultExt = "csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var csv = new StringBuilder();
        
                // 添加CSV头部
                csv.AppendLine("KRR_LV,XXY_SR,Title,Diff,Artist,Creator,Keys,BPM,OD,HP,LN%,beatmapID,beatmapsetId,filePath");
        
                // 添加数据行
                foreach (var file in OsuFiles)
                {
                    var line = $"\"{file.KRRLV:F2}\",\"{file.XXYSR:F2}\",\"{file.Title}\",\"{file.Diff}\",\"{file.Artist}\",\"{file.Creator}\",{file.Keys},\"{file.BPM}\",{file.OD},{file.HP},\"{file.LNPercent:F2}\",{file.BeatmapID},{file.BeatmapSetID},\"{file.FilePath}\"";
                    csv.AppendLine(line);
                }
        
                File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
                var processStartInfo = new ProcessStartInfo(dialog.FileName)
                {
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);

            }
            catch (Exception ex)
            {
                // 可以添加错误处理，例如显示消息框
                Console.WriteLine($"保存文件时出错: {ex.Message}");
            }
        }
    }

    public async void ProcessDroppedFiles(string[] files)
    {
        try
        {
            // 计算总文件数（包括.osz中的.osu文件）
            TotalCount = CountOsuFiles(files);
            _currentProcessedCount = 0;
    
            // 计算.osz文件中的.osu文件数量
            foreach (var file in files)
            {
                if (File.Exists(file) && Path.GetExtension(file).Equals(".osz", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var archive = ZipFile.OpenRead(file);
                        TotalCount += archive.Entries.Count(e => e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取.osz文件时出错: {ex.Message}");
                    }
                }
            }
    
            // 显示进度窗口
            Application.Current.Dispatcher.Invoke(() =>
            {
                _processingWindow = new ProcessingWindow();
                _processingWindow.Show();
            });
    
            _updateTimer.Start();
    
            await Task.Run(async () =>
            {
                var tasks = new List<Task>();
        
                foreach (var file in files)
                {
                    if (Directory.Exists(file))
                    {
                        // 处理文件夹
                        var osuFiles = Directory.GetFiles(file, "*.osu", SearchOption.AllDirectories)
                            .Where(f => Path.GetExtension(f).Equals(".osu", StringComparison.OrdinalIgnoreCase));
                
                        foreach (var osuFile in osuFiles)
                        {
                            await _semaphore.WaitAsync();
                            var task = Task.Run(() => ProcessOsuFile(osuFile))
                                .ContinueWith(t => 
                                {
                                    _semaphore.Release();
                                    // 使用原子操作更新计数器
                                    Interlocked.Increment(ref _currentProcessedCount);
                            
                                    // 更新UI进度
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        UpdateProgress(_currentProcessedCount, TotalCount);
                                    });
                                });
                            tasks.Add(task);
                        }
                    }
                    else if (File.Exists(file) && Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        await _semaphore.WaitAsync();
                        var task = Task.Run(() => ProcessOsuFile(file))
                            .ContinueWith(t => 
                            {
                                _semaphore.Release();
                                // 使用原子操作更新计数器
                                Interlocked.Increment(ref _currentProcessedCount);
                        
                                // 更新UI进度
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    UpdateProgress(_currentProcessedCount, TotalCount);
                                });
                            });
                        tasks.Add(task);
                    }
                    // 添加对.osz文件的支持
                    else if (File.Exists(file) && Path.GetExtension(file).Equals(".osz", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var archive = ZipFile.OpenRead(file);
                            var osuEntries = archive.Entries.Where(e => e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
                        
                            foreach (var entry in osuEntries)
                            {
                                await _semaphore.WaitAsync();
                                var task = Task.Run(() => ProcessOszEntry(entry, file))
                                    .ContinueWith(t => 
                                    {
                                        _semaphore.Release();
                                        // 使用原子操作更新计数器
                                        Interlocked.Increment(ref _currentProcessedCount);
                                    
                                        // 更新UI进度
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            UpdateProgress(_currentProcessedCount, TotalCount);
                                        });
                                    });
                                tasks.Add(task);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"处理.osz文件时出错: {ex.Message}");
                        }
                    }
                }
        
                await Task.WhenAll(tasks);
            });
    
            // 等待所有任务完成后，确保剩余的项目也被添加
            await Task.Delay(200); // 给最后一次更新留出时间
            _updateTimer.Stop();
    
            // 关闭进度窗口
            Application.Current.Dispatcher.Invoke(() =>
            {
                _processingWindow?.Close();
                _processingWindow = null;
            });
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void ProcessOszEntry(ZipArchiveEntry entry, string oszFilePath)
    {
        try
        {
            // 创建一个唯一的标识符，包含.osz文件路径和条目名称
            string uniqueId = $"{oszFilePath}|{entry.FullName}";
        
            // 检查是否已存在于列表中
            if (OsuFiles.Any(f => f.FilePath != null && f.FilePath.Equals(uniqueId, StringComparison.OrdinalIgnoreCase)))
                return;

            var item = new OsuFileItem
            {
                FileName = entry.Name,
                FilePath = uniqueId, // 使用唯一标识符
                Status = "待处理"
            };

            // 添加到待处理列表
            lock (_pendingItemsLock)
            {
                _pendingItems.Add(item);
            }

            // 执行分析方法
            AnalyzeOszEntry(item, entry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理.osz条目时出错: {ex.Message}");
        }
    }
    
    private void AnalyzeOszEntry(OsuFileItem item, ZipArchiveEntry entry)
{
    try
    {
        // 从.osz条目中读取内容
        using var stream = entry.Open();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        // 创建临时文件路径
        var tempFilePath = Path.GetTempFileName();
        try
        {
            // 将内存流写入临时文件
            using (var fileStream = File.Create(tempFilePath))
            {
                memoryStream.WriteTo(fileStream);
            }

            // 使用 Analyzer 分析临时文件
            var analyzer = new OsuAnalyzer();
            var result = analyzer.Analyze(tempFilePath); // 调用已存在的方法

            // 更新 UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Diff = result.Diff;
                item.Title = result.Title;
                item.Artist = result.Artist;
                item.Creator = result.Creator;
                item.Keys = result.Keys;
                item.BPM = result.BPM;
                item.OD = result.OD;
                item.HP = result.HP;
                item.LNPercent = result.LNPercent;
                item.BeatmapID = result.BeatmapID;
                item.BeatmapSetID = result.BeatmapSetID;
                item.XXYSR = result.XXY_SR;
                item.KRRLV = result.KRR_LV;
                item.Status = "已分析";
            });
        }
        finally
        {
            // 确保删除临时文件
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
    catch (ArgumentException ex) when (ex.Message == "不是mania模式")
    {
        Application.Current.Dispatcher.Invoke((Action)(() =>
        {
            var itemToRemove = OsuFiles.FirstOrDefault(f => f.FilePath == item.FilePath);
            if (itemToRemove != null)
                OsuFiles.Remove(itemToRemove);
        }));
    }
    catch (Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            item.Status = $"错误: {ex.Message}";
        });
    }
}


    
    private void UpdateProgress(int current, int total)
    {
        if (_processingWindow != null)
        {
            _processingWindow.UpdateProgress(current, total);
        }
    }

    
    
    
    private int CountOsuFiles(string[] files)
    {
        int count = 0;
        foreach (var file in files)
        {
            if (Directory.Exists(file))
            {
                count += Directory.GetFiles(file, "*.osu", SearchOption.AllDirectories)
                    .Count(f => Path.GetExtension(f).Equals(".osu", StringComparison.OrdinalIgnoreCase));
            }
            else if (File.Exists(file) && Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
            // 添加对.osz文件的支持
            else if (File.Exists(file) && Path.GetExtension(file).Equals(".osz", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(file);
                    count += archive.Entries.Count(e => e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"读取.osz文件时出错: {ex.Message}");
                }
            }
        }
        return count;
    }


    private bool IsOsuFile(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase);
    }

    private void ProcessDirectory(string directoryPath)
    {
        try
        {
            var osuFiles = Directory.GetFiles(directoryPath, "*.osu", SearchOption.AllDirectories)
                .Where(IsOsuFile);
    
            foreach (var file in osuFiles)
            {
                ProcessOsuFile(file);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理目录时出错: {ex.Message}");
        }
    }
    
    private void ProcessOsuFile(string filePath)
    {
        // 检查文件是否已存在于列表中
        if (OsuFiles.Any(f => f.FilePath != null && f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var item = new OsuFileItem
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            Status = "待处理"
        };

        // 添加到待处理列表
        lock (_pendingItemsLock)
        {
            _pendingItems.Add(item);
        }

        // 执行分析方法
        Analyze(item);
    }


    private void Analyze(OsuFileItem item)
    {
        try
        {
            var analyzer = new OsuAnalyzer();
            var result = analyzer.Analyze(item.FilePath);

            // 使用Dispatcher将更新操作调度到UI线程
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新 OsuFileItem 的属性
                item.Diff = result.Diff;
                item.Title = result.Title;
                item.Artist = result.Artist;
                item.Creator = result.Creator;
                item.Keys = result.Keys;
                item.BPM = result.BPM;
                item.OD = result.OD;
                item.HP = result.HP;
                item.LNPercent = result.LNPercent;
                item.BeatmapID = result.BeatmapID;
                item.BeatmapSetID = result.BeatmapSetID;
                item.XXYSR = result.XXY_SR;
                item.KRRLV = result.KRR_LV;
                item.Status = "已分析";
            });
        }
        catch (ArgumentException ex) when (ex.Message == "不是mania模式")
        {
            // 使用Dispatcher将删除操作调度到UI线程
            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                // 从UI列表中查找并移除该项
                var itemToRemove = OsuFiles.FirstOrDefault(f => f.FilePath == item.FilePath);
                if (itemToRemove != null)
                    OsuFiles.Remove(itemToRemove);
            }));
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Status = $"错误: {ex.Message}";
            });
        }
    }
    
    // 添加 Dispose 方法
    public void Dispose()
    {
        _semaphore.Dispose();
        _updateTimer.Stop();
    }

    /// <summary>
    /// osu 文件分析方法，留空等你实现
    /// <param name="filePath">osu 文件路径</param>
    /// </summary>

    private void OsuAnalyze(string? filePath)
    {
        var analyzer = new OsuAnalyzer();
        analyzer.Analyze(filePath);
    }
}

 
}
