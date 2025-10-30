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
using System.Windows.Data;
using System.Windows.Threading;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.KRRLVAnalysis
{
    /// <summary>
    /// LV分析视图模型
    /// 显示所有内容，不进行过滤，支持批量处理和进度更新
    /// 非mania谱和分析失败的谱面会显示对应状态，而不是过滤掉
    /// </summary>
    public partial class KRRLVAnalysisViewModel : ReactiveViewModelBase
    {
        // 智能批处理大小：根据CPU核心数动态调整
        private static readonly int OptimalBatchSize = Math.Max(20, Environment.ProcessorCount * 8);
        private const int BatchSize = 50; // UI更新和并行输出块大小（保持兼容性）

        [Inject]
        private StateBarManager StateBarManager { get; set; } = null!;

        // 简单的对象池用于复用KRRLVAnalysisItem对象
        private class ItemObjectPool
        {
            private readonly Stack<KRRLVAnalysisItem> _pool = new Stack<KRRLVAnalysisItem>();
            private readonly Lock _lock = new Lock();

            public KRRLVAnalysisItem Rent()
            {
                lock (_lock) return _pool.Count > 0 ? _pool.Pop() : new KRRLVAnalysisItem();
            }

            public void Return(KRRLVAnalysisItem item)
            {
                // 调用Dispose清理对象
                item.Dispose();

                lock (_lock)
                {
                    if (_pool.Count < 100) // 50*2=100，考虑UI显示延迟和缓冲
                        _pool.Push(item);
                    // 池已满时不做任何操作，让对象自然被GC回收
                }
            }

            /// <summary>
            /// 清空对象池，释放所有缓存的对象
            /// </summary>
            public void Clear()
            {
                lock (_lock) _pool.Clear();
            }
        }

        private readonly ItemObjectPool _itemPool = new ItemObjectPool();

        // 进度更新定时器 - 每100毫秒更新一次UI
        private DispatcherTimer? _progressUpdateTimer;

        public Bindable<string> PathInput { get; set; } = new Bindable<string>(string.Empty);

        public Bindable<ObservableCollection<KRRLVAnalysisItem>> OsuFiles { get; set; } = new Bindable<ObservableCollection<KRRLVAnalysisItem>>(new ObservableCollection<KRRLVAnalysisItem>());

        public Bindable<ICollectionView> FilteredOsuFiles { get; set; }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4, 4); // 最多4个并发线程

        // private ProcessingWindow? _processingWindow;
        private readonly List<KRRLVAnalysisItem> _pendingItems = new List<KRRLVAnalysisItem>();
        private readonly Lock _pendingItemsLock = new Lock();

        private int _currentProcessedCount;
        private int _uiUpdateCounter; // UI更新计数器，每BatchSize个文件更新一次UI

        private Bindable<int> TotalCount { get; set; } = new Bindable<int>();
        private Bindable<double> ProgressValue
        {
            get => StateBarManager.ProgressValue;
        }

        public KRRLVAnalysisViewModel()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            _uiUpdateCounter = 0;
            FilteredOsuFiles = new Bindable<ICollectionView>(CollectionViewSource.GetDefaultView(OsuFiles.Value));
            // 设置自动绑定通知
            SetupAutoBindableNotifications();

            // 初始化进度条为0%
            ProgressValue.Value = 0;

            // 预热GC以获得更好的性能
            Task.Run(() => GC.Collect(0, GCCollectionMode.Optimized, false));
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
                            Interlocked.Increment(ref _uiUpdateCounter);

                            // 数据表更新：每5个文件更新一次（更频繁）
                            if (_uiUpdateCounter % 5 == 0) UpdateDataGridAsync();

                            // 进度条更新：每OptimalBatchSize个文件更新一次（保持现有逻辑）
                            if (_uiUpdateCounter >= OptimalBatchSize)
                            {
                                Interlocked.Exchange(ref _uiUpdateCounter, 0);
                                UpdateProgressAsync();
                            }
                        });
        }

        /// <summary>
        /// 异步进度条更新，使用BeginInvoke避免阻塞
        /// </summary>
        private void UpdateProgressAsync()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TotalCount.Value > 0)
                {
                    ProgressValue.Value = (double)_currentProcessedCount / TotalCount.Value * 100;
                    Logger.WriteLine(LogLevel.Information, $"[DEBUG] UpdateProgressAsync: ProgressValue={ProgressValue.Value:F1}%, Current={_currentProcessedCount}, Total={TotalCount.Value}");
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 异步数据表更新，使用BeginInvoke避免阻塞
        /// </summary>
        private void UpdateDataGridAsync()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 批量更新UI项目
                List<KRRLVAnalysisItem> itemsToAdd;

                lock (_pendingItemsLock)
                {
                    if (_pendingItems.Count > 0)
                    {
                        itemsToAdd = new List<KRRLVAnalysisItem>(_pendingItems);
                        _pendingItems.Clear();
                    }
                    else
                        itemsToAdd = new List<KRRLVAnalysisItem>();
                }

                foreach (KRRLVAnalysisItem item in itemsToAdd)
                    OsuFiles.Value.Add(item);

                Logger.WriteLine(LogLevel.Information, $"[DEBUG] UpdateDataGridAsync: Added {itemsToAdd.Count} items, Total items: {OsuFiles.Value.Count}");
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 更新分析结果到UI项目
        /// </summary>
        private void UpdateAnalysisResult(KRRLVAnalysisItem item, OsuAnalysisResult result)
        {
            // 异步更新UI，避免阻塞
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { item.Result = result; }), DispatcherPriority.Background);
        }

        [RelayCommand]
        private void Browse()
        {
            string selected = FilesHelper.ShowFolderBrowserDialog("选择文件夹");

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
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出数据",
                Filter = "CSV 文件 (*.csv)|*.csv|Excel 文件 (*.xlsx)|*.xlsx",
                DefaultExt = "csv",
                AddExtension = true
            };

            if (saveDialog.ShowDialog() == true)
            {
                string filePath = saveDialog.FileName;
                string extension = Path.GetExtension(filePath).ToLower();

                try
                {
                    if (extension == ".csv")
                        ExportToCsv(filePath);
                    else if (extension == ".xlsx") ExportToExcel(filePath);

                    // 打开导出的文件
                    var processStartInfo = new ProcessStartInfo(filePath)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 导出文件失败: {ex.Message}");
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            var csv = new StringBuilder();

            // 使用共享的导出属性配置
            (string Property, string Header)[] exportProperties = KRRLVAnalysisColumnConfig.ExportProperties;

            // 添加CSV头部
            csv.AppendLine(string.Join(",", exportProperties.Select(p => p.Header)));

            // 添加数据行
            foreach (KRRLVAnalysisItem file in OsuFiles.Value)
            {
                IEnumerable<string> values = exportProperties.Select(prop =>
                {
                    object? value = file.Result != null ? typeof(OsuAnalysisResult).GetProperty(prop.Property)?.GetValue(file.Result) : null;

                    // 格式化数值类型
                    if (value is double d)
                        return $"\"{d:F2}\"";
                    else if (value is int i)
                        return i.ToString();
                    else
                        return $"\"{value ?? ""}\"";
                });

                csv.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        private void ExportToExcel(string filePath)
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add("KRR LV Analysis");

            // 使用共享的导出属性配置
            (string Property, string Header)[] exportProperties = KRRLVAnalysisColumnConfig.ExportProperties;

            // 添加头部
            for (int i = 0; i < exportProperties.Length; i++) worksheet.Cell(1, i + 1).Value = exportProperties[i].Header;

            // 添加数据行
            int row = 2;

            foreach (KRRLVAnalysisItem file in OsuFiles.Value)
            {
                for (int col = 0; col < exportProperties.Length; col++)
                {
                    string propName = exportProperties[col].Property;
                    object? value = file.Result != null ? typeof(OsuAnalysisResult).GetProperty(propName)?.GetValue(file.Result) : null;

                    worksheet.Cell(row, col + 1).Value = Convert.ToString(value ?? "");
                }

                row++;
            }

            // 自动调整列宽
            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        public async void ProcessDroppedFiles(string[] files)
        {
            try
            {
                Logger.WriteLine(LogLevel.Debug,
                                 $"[DEBUG] ProcessDroppedFiles called with {files.Length} files: {string.Join(", ", files)}");

                // 清空现有数据，开始新的分析
#pragma warning disable CS4014 // 由于此调用不会等待，因此在此调用完成之前将会继续执行当前方法
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // 将现有对象返回对象池
                    foreach (KRRLVAnalysisItem item in OsuFiles.Value) _itemPool.Return(item);

                    OsuFiles.Value.Clear();
                    FilteredOsuFiles.Value = CollectionViewSource.GetDefaultView(OsuFiles.Value);

                    // 强制清理集合的内部引用
                    GC.Collect(0, GCCollectionMode.Forced, false);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014

                // 计算总文件数（包括.osz中的.osu文件）
                string[] allOsuFiles = BeatmapFileHelper.EnumerateOsuFiles(files).ToArray();
                Logger.WriteLine(LogLevel.Debug, $"[DEBUG] allOsuFiles.Length = {allOsuFiles.Length}");
                TotalCount.Value = allOsuFiles.Length;
                _currentProcessedCount = 0;
                _uiUpdateCounter = 0;

                // 显示进度窗口,处理前
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ProgressValue.Value = 0;
                    Logger.WriteLine(LogLevel.Information, $"[DEBUG] Progress bar initialized: Value={ProgressValue.Value}");

                    // 启动进度更新定时器 - 每100毫秒更新一次
                    _progressUpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100),
                        IsEnabled = true
                    };
                    _progressUpdateTimer.Tick += ProgressUpdateTimer_Tick;
                    _progressUpdateTimer.Start();
                    Logger.WriteLine(LogLevel.Information, "[DEBUG] Progress update timer started");
                }), DispatcherPriority.Background);
#pragma warning restore CS4014

                const int batchSize = BatchSize; // 保持向后兼容性，但内部使用智能大小
                var batches = new List<string[]>();

                for (int i = 0; i < allOsuFiles.Length; i += batchSize)
                {
                    string[] batch = allOsuFiles.Skip(i).Take(batchSize).ToArray();
                    batches.Add(batch);
                }

                foreach (string[] batch in batches)
                {
                    await Task.Run(async () =>
                    {
                        var tasks = new List<Task>();

                        foreach (string file in batch)
                        {
                            if (Directory.Exists(file))
                            {
                                // 处理文件夹
                                IEnumerable<string> osuFiles = Directory.GetFiles(file, "*.osu", SearchOption.AllDirectories)
                                                                        .Where(f => Path.GetExtension(f)
                                                                                        .Equals(".osu", StringComparison.OrdinalIgnoreCase));

                                foreach (string osuFile in osuFiles)
                                {
                                    await _semaphore.WaitAsync();
                                    Task task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(osuFile)));
                                    tasks.Add(task);
                                }
                            }
                            else if (File.Exists(file) &&
                                     Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                            {
                                await _semaphore.WaitAsync();
                                Task task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(file)));
                                tasks.Add(task);
                            }
                        }

                        await Task.WhenAll(tasks);
                    });

                    // 智能内存管理：只有在内存压力大时才进行GC
                    if (_currentProcessedCount % 500 == 0) // 100*5=500，平衡检查频率
                    {
                        GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
                        if (memoryInfo.MemoryLoadBytes > 500 * 1024 * 1024) // 超过500MB
                            GC.Collect(1, GCCollectionMode.Optimized, false);
                    }
                }

                // 等待所有任务完成后，确保剩余的项目也被添加
                await Task.Delay(200); // 给最后一次更新留出时间

                // 确保进度条显示100%并添加剩余项目
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ProgressValue.Value = 100;

                    // 添加剩余的待处理项目
                    List<KRRLVAnalysisItem> itemsToAdd;

                    lock (_pendingItemsLock)
                    {
                        if (_pendingItems.Count > 0)
                        {
                            itemsToAdd = new List<KRRLVAnalysisItem>(_pendingItems);
                            _pendingItems.Clear();
                        }
                        else
                            itemsToAdd = new List<KRRLVAnalysisItem>();
                    }

                    foreach (KRRLVAnalysisItem item in itemsToAdd) OsuFiles.Value.Add(item);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014

                // 短暂延迟，让用户看到100%的进度
                await Task.Delay(300);

                // 关闭进度窗口
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                                                       {
                                                                           // 停止进度更新定时器
                                                                           if (_progressUpdateTimer != null)
                                                                           {
                                                                               _progressUpdateTimer.Stop();
                                                                               _progressUpdateTimer.Tick -= ProgressUpdateTimer_Tick;
                                                                               _progressUpdateTimer = null;
                                                                               Logger.WriteLine(LogLevel.Debug, "[DEBUG] Progress update timer stopped");
                                                                           }

                                                                           FilteredOsuFiles.Value = CollectionViewSource.GetDefaultView(OsuFiles.Value);
                                                                           Logger.WriteLine(LogLevel.Information,
                                                                                            "[KRRLVAnalysisViewModel] FilteredOsuFiles refreshed, count: {0}",
                                                                                            FilteredOsuFiles.Value.Cast<object>().Count());
                                                                           // _processingWindow?.Close();
                                                                           // _processingWindow = null;
                                                                           Logger.WriteLine(LogLevel.Debug, $"[DEBUG] Processing completed: FinalValue={ProgressValue.Value:F1}%");

                                                                           // 分析完成后进行内存清理
                                                                           PerformMemoryCleanup();
                                                                       }), DispatcherPriority.Background);
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                // 确保在异常情况下也停止定时器
#pragma warning disable CS4014
                if (_progressUpdateTimer != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        _progressUpdateTimer.Stop();
                        _progressUpdateTimer.Tick -= ProgressUpdateTimer_Tick;
                        _progressUpdateTimer = null;
                        Logger.WriteLine(LogLevel.Debug, "[DEBUG] Progress timer stopped due to exception");
                    });
                }
#pragma warning restore CS4014

                Console.WriteLine($"[ERROR] 处理文件时发生异常: {ex.Message}");
            }
        }

        private void ProcessOsuFile(string filePath)
        {
            KRRLVAnalysisItem item = _itemPool.Rent();

            // 添加到待处理列表
            lock (_pendingItemsLock) _pendingItems.Add(item);

            // 执行分析方法
            Task.Run(() => Analyze(item, filePath));
        }

        private async Task Analyze(KRRLVAnalysisItem item, string filePath)
        {
            try
            {
                OsuAnalysisResult result = await OsuAnalyzer.AnalyzeAsync(filePath);

                // 更新基础信息
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() => { UpdateAnalysisResult(item, result); }, DispatcherPriority.Background);
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 执行内存清理，释放不再需要的资源
        /// </summary>
        private void PerformMemoryCleanup()
        {
            try
            {
                // 清理对象池中多余的对象
                _itemPool.Clear();

                // 清理待处理项目列表
                lock (_pendingItemsLock) _pendingItems.Clear();

                // 强制垃圾回收
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);

                Logger.WriteLine(LogLevel.Information, "[KRRLVAnalysisViewModel] Memory cleanup completed");
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Warning, "[KRRLVAnalysisViewModel] Memory cleanup failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 进度更新定时器的Tick事件处理函数 - 每100毫秒更新一次进度条
        /// </summary>
        private void ProgressUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (TotalCount.Value > 0)
            {
                double progress = (double)_currentProcessedCount / TotalCount.Value * 100;
                ProgressValue.Value = Math.Min(progress, 100); // 确保不超过100%
                Logger.WriteLine(LogLevel.Information, $"[DEBUG] Timer update: ProgressValue={ProgressValue.Value:F1}%, Current={_currentProcessedCount}, Total={TotalCount.Value}");
            }
        }
    }
}
