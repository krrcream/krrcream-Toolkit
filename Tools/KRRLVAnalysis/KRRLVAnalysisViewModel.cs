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
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;

namespace krrTools.Tools.KRRLVAnalysis
{
    //TODO:   1.控件优化，优化UI更新逻辑，减少频繁更新;
    //        2.数据加载异常，显示数据为0;
    //        3.xxySR计算太慢，严重需要优化速度;

    public partial class KRRLVAnalysisViewModel : ReactiveViewModelBase
    {
        public Bindable<string> PathInput { get; set; } = new(string.Empty);
        public Bindable<ObservableCollection<KRRLVAnalysisItem>> OsuFiles { get; set; } = new(new ObservableCollection<KRRLVAnalysisItem>());

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4, 4); // 最多4个并发线程
        private readonly DispatcherTimer _updateTimer;
        // private ProcessingWindow? _processingWindow;
        private readonly List<KRRLVAnalysisItem> _pendingItems = new List<KRRLVAnalysisItem>();
        private readonly Lock _pendingItemsLock = new Lock();

        private int _currentProcessedCount;

        public Bindable<int> TotalCount { get; set; } = new();
        public Bindable<double> ProgressValue { get; set; } = new();
        public Bindable<bool> IsProgressVisible { get; set; } = new();
        
        public int ProcessedCount { get; set; }

        public KRRLVAnalysisViewModel()
        {
            // 初始化定时器，每100毫秒批量更新一次UI
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // 设置自动绑定通知
            SetupAutoBindableNotifications();
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
                OsuFiles.Value.Add(item);
            }
        }

        /// <summary>
        /// 创建异步处理任务，包含进度更新逻辑
        /// </summary>
        private Task CreateProcessingTask(Func<Task> processingAction)
        {
            return Task.Run(processingAction)
                .ContinueWith(_ =>
                {
                    _semaphore.Release();
                    // 使用原子操作更新计数器
                    Interlocked.Increment(ref _currentProcessedCount);

                    // 更新UI进度
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressValue.Value = (double)_currentProcessedCount / TotalCount.Value * 100;
                    });
                });
        }

        /// <summary>
        /// 更新分析结果到UI项目
        /// </summary>
        private void UpdateAnalysisResult(KRRLVAnalysisItem item, OsuAnalysisResult result)
        {
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
                item.YlsLV = OsuAnalyzer.CalculateYlsLevel(result.XXY_SR);
                item.NotesCount = result.NotesCount;
                item.MaxKPS = result.MaxKPS;
                item.AvgKPS = result.AvgKPS;
                item.Status = "已分析";
            });
        }

        [RelayCommand]
        private void Browse()
        {
            var selected = FilesHelper.ShowFolderBrowserDialog("选择文件夹");
            if (!string.IsNullOrEmpty(selected))
            {
                PathInput.Value = selected;
                ProcessDroppedFiles([selected]);
            }
        }

        [RelayCommand]
        private void OpenPath()
        {
            if (!string.IsNullOrEmpty(PathInput.Value))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = PathInput.Value,
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
                    foreach (var file in OsuFiles.Value)
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
                TotalCount.Value = BeatmapFileHelper.GetOsuFilesCount(files);
                _currentProcessedCount = 0;

                // 显示进度窗口,处理前
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // _processingWindow = new ProcessingWindow();
                    // _processingWindow.Show();
                    IsProgressVisible.Value = true;
                    ProgressValue.Value = 0;
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
                                var task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(osuFile)));
                                tasks.Add(task);
                            }
                        }
                        else if (File.Exists(file) && Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                        {
                            await _semaphore.WaitAsync();
                            var task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(file)));
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
                                    var task = CreateProcessingTask(() => Task.Run(() => ProcessOszEntry(entry, file)));
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
                    IsProgressVisible.Value = false;
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
                if (OsuFiles.Value.Any(f => f.FilePath != null && f.FilePath.Equals(uniqueId, StringComparison.OrdinalIgnoreCase)))
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

                    // 使用 BeatmapAnalyzer 分析临时文件
                    var result = BeatmapAnalyzer.Analyze(tempFilePath);
                    if (result == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.Status = "分析失败";
                        });
                        return;
                    }

                    // 使用通用方法更新UI
                    UpdateAnalysisResult(item, result);
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
                    var itemToRemove = OsuFiles.Value.FirstOrDefault(f => f.FilePath == item.FilePath);
                    if (itemToRemove != null)
                        OsuFiles.Value.Remove(itemToRemove);
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
            if (OsuFiles.Value.Any(f => f.FilePath != null && f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
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
                var result = BeatmapAnalyzer.Analyze(item.FilePath);
                if (result == null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.Status = "分析失败";
                    });
                    return;
                }

                // 使用通用方法更新UI
                UpdateAnalysisResult(item, result);
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



        public new void Dispose()
        {
            _semaphore.Dispose();
            _updateTimer.Stop();
        }
    }
}
