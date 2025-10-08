using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using krrTools.Beatmaps;
using krrTools.Data;

namespace krrTools.Tools.KRRLVAnalysis
{
    //TODO:   1.控件优化，优化UI更新逻辑，减少频繁更新;
    //        2.数据加载异常，显示数据为0;
    //        3.xxySR计算太慢，严重需要优化速度;

    public partial class KRRLVAnalysisViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pathInput = null!;

        [ObservableProperty]
        private ObservableCollection<KRRLVAnalysisItem> _osuFiles = new ObservableCollection<KRRLVAnalysisItem>();

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4, 4); // 最多4个并发线程
        private readonly DispatcherTimer _updateTimer;
        // private ProcessingWindow? _processingWindow;
        private readonly List<KRRLVAnalysisItem> _pendingItems = new List<KRRLVAnalysisItem>();
        private readonly Lock _pendingItemsLock = new Lock();

        private int _totalCount;

        private int _currentProcessedCount;

        private int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (Math.Abs(_progressValue - value) > 0)
                {
                    _progressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isProgressVisible;
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set
            {
                if (_isProgressVisible != value)
                {
                    _isProgressVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public int ProcessedCount { get; set; }

        public KRRLVAnalysisViewModel()
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
            List<KRRLVAnalysisItem> itemsToAdd;
            lock (_pendingItemsLock)
            {
                if (_pendingItems.Count == 0) return;
                itemsToAdd = new List<KRRLVAnalysisItem>(_pendingItems);
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
            var selected = FilesHelper.ShowFolderBrowserDialog("选择文件夹");
            if (!string.IsNullOrEmpty(selected))
            {
                PathInput = selected;
                ProcessDroppedFiles([selected]);
            }
        }

        [RelayCommand]
        private void OpenPath()
        {
            if (!string.IsNullOrEmpty(PathInput))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = PathInput,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 无法打开路径: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void Save()
        {
            var savePath = FilesHelper.ShowSaveFileDialog("保存为CSV文件", "CSV文件|*.csv", "csv");
            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    var csv = new StringBuilder();

                    // 添加CSV头部
                    csv.AppendLine("KRR_LV,YLS_LV,XXY_SR,Title,Diff,Artist,Creator,Keys,Notes,MaxKPS,AvgKPS,BPM,OD,HP,LN%,beatmapID,beatmapSetId,filePath");

                    // 添加数据行
                    foreach (var file in OsuFiles)
                    {
                        var line = $"\"{file.KrrLV:F2}\",\"{file.YlsLV:F2}\",\"{file.XxySR:F2}\",\"{file.Title}\",\"{file.Diff}\",\"{file.Artist}\",\"{file.Creator}\",{file.Keys},{file.NotesCount},\"{file.MaxKPS:F2}\",\"{file.AvgKPS:F2}\",\"{file.BPM}\",{file.OD},{file.HP},\"{file.LNPercent:F2}\",{file.BeatmapID},{file.BeatmapSetID},\"{file.FilePath}\"";
                        csv.AppendLine(line);
                    }

                    File.WriteAllText(savePath, csv.ToString(), Encoding.UTF8);
                    var processStartInfo = new ProcessStartInfo(savePath)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(processStartInfo);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 保存CSV文件失败: {ex.Message}");
                }
            }
        }

        public async void ProcessDroppedFiles(string[] files)
        {
            try
            {
                // 计算总文件数（包括.osz中的.osu文件）
                TotalCount = BeatmapFileHelper.GetOsuFilesCount(files);
                _currentProcessedCount = 0;

                // 显示进度窗口,处理前
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // _processingWindow = new ProcessingWindow();
                    // _processingWindow.Show();
                    IsProgressVisible = true;
                    ProgressValue = 0;
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
                                    .ContinueWith(_ =>
                                    {
                                        _semaphore.Release();
                                        // 使用原子操作更新计数器
                                        Interlocked.Increment(ref _currentProcessedCount);

                                        // 更新UI进度, 处理中
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            // UpdateProgress(_currentProcessedCount, TotalCount);
                                            ProgressValue = (double)_currentProcessedCount / TotalCount * 100;
                                        });
                                    });
                                tasks.Add(task);
                            }
                        }
                        else if (File.Exists(file) && Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                        {
                            await _semaphore.WaitAsync();
                            var task = Task.Run(() => ProcessOsuFile(file))
                                .ContinueWith(_ =>
                                {
                                    _semaphore.Release();
                                    // 使用原子操作更新计数器
                                    Interlocked.Increment(ref _currentProcessedCount);

                                    // 更新UI进度
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        // UpdateProgress(_currentProcessedCount, TotalCount);
                                        ProgressValue = (double)_currentProcessedCount / TotalCount * 100;
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
                                        .ContinueWith(_ =>
                                        {
                                            _semaphore.Release();
                                            // 使用原子操作更新计数器
                                            Interlocked.Increment(ref _currentProcessedCount);

                                            // 更新UI进度
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                // UpdateProgress(_currentProcessedCount, TotalCount);
                                                ProgressValue = (double)_currentProcessedCount / TotalCount * 100;
                                            });
                                        });
                                    tasks.Add(task);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] 处理.osz文件失败: {ex.Message}");
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
                    // _processingWindow?.Close();
                    // _processingWindow = null;
                    IsProgressVisible = false;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理文件时发生异常: {ex.Message}");
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

                var item = new KRRLVAnalysisItem
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
                Console.WriteLine($"[ERROR] 处理.osz条目时发生异常: {ex.Message}");
            }
        }

        private void AnalyzeOszEntry(KRRLVAnalysisItem item, ZipArchiveEntry entry)
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
                        item.BPM = result.BPMDisplay;
                        item.OD = result.OD;
                        item.HP = result.HP;
                        item.LNPercent = result.LNPercent;
                        item.BeatmapID = result.BeatmapID;
                        item.BeatmapSetID = result.BeatmapSetID;
                        item.XxySR = result.XXY_SR;
                        item.KrrLV = result.KRR_LV;
                        item.YlsLV = CalculateLevel(result.XXY_SR);
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
                Console.WriteLine($"[ERROR] 分析.osz条目时发生异常: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Status = $"错误: {ex.Message}";
                });
            }
        }
        
        // private void UpdateProgress(int current, int total)
        // {
        //     _processingWindow?.UpdateProgress(current, total);
        // }

        private void ProcessOsuFile(string filePath)
        {
            // 检查文件是否已存在于列表中
            if (OsuFiles.Any(f => f.FilePath != null && f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                return;

            var item = new KRRLVAnalysisItem
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


        private void Analyze(KRRLVAnalysisItem item)
        {
            try
            {
                var analyzer = new OsuAnalyzer();
                var result = analyzer.Analyze(item.FilePath);

                // 使用Dispatcher将更新操作调度到UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 更新 KRRLVAnalysisItem 的属性
                    item.Diff = result.Diff;
                    item.Title = result.Title;
                    item.Artist = result.Artist;
                    item.Creator = result.Creator;
                    item.Keys = result.Keys;
                    item.BPM = result.BPMDisplay;
                    item.OD = result.OD;
                    item.HP = result.HP;
                    item.LNPercent = result.LNPercent;
                    item.BeatmapID = result.BeatmapID;
                    item.BeatmapSetID = result.BeatmapSetID;
                    item.XxySR = result.XXY_SR;
                    item.KrrLV = result.KRR_LV;
                    item.YlsLV = CalculateLevel(result.XXY_SR);
                    item.NotesCount = result.NotesCount;
                    item.MaxKPS = result.MaxKPS;
                    item.AvgKPS = result.AvgKPS;
                    item.Status = "已分析";
                });
            }
            catch (ArgumentException ex) when (ex.Message == "不是mania模式")
            {
                // 设置状态为不是mania模式，不移除项目
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Status = "不是mania模式";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Status = $"错误: {ex.Message}";
                });
            }
        }

        private static double CalculateLevel(double xxyStarRating)
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

        public void Dispose()
        {
            _semaphore.Dispose();
            _updateTimer.Stop();
        }
    }
}
