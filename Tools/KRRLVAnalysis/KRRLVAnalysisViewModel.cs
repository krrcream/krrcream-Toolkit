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

        // 简单的对象池用于复用KRRLVAnalysisItem对象
        private class ItemObjectPool
        {
            private readonly Stack<KRRLVAnalysisItem> _pool = new();
            private readonly object _lock = new();

            public KRRLVAnalysisItem Rent()
            {
                lock (_lock)
                {
                    return _pool.Count > 0 ? _pool.Pop() : new KRRLVAnalysisItem();
                }
            }

            public void Return(KRRLVAnalysisItem item)
            {
                if (item == null) return;
                
                // 重置对象状态
                // item.FileName = null;
                item.FilePath = null;
                item.Status = null;
                item.Result = null;

                lock (_lock)
                {
                    if (_pool.Count < 1000) // 限制池大小
                        _pool.Push(item);
                }
            }
        }

        private readonly ItemObjectPool _itemPool = new();

        public Bindable<string> PathInput { get; set; } = new(string.Empty);

        private Bindable<ObservableCollection<KRRLVAnalysisItem>> OsuFiles { get; set; } =
            new(new ObservableCollection<KRRLVAnalysisItem>());

        public Bindable<ICollectionView> FilteredOsuFiles { get; set; }

        private readonly SemaphoreSlim _semaphore = new(4, 4); // 最多4个并发线程

        // private ProcessingWindow? _processingWindow;
        private readonly List<KRRLVAnalysisItem> _pendingItems = new();
        private readonly Lock _pendingItemsLock = new();

        private int _currentProcessedCount;
        private int _uiUpdateCounter; // UI更新计数器，每BatchSize个文件更新一次UI

        private Bindable<int> TotalCount { get; set; } = new();
        private Bindable<double> ProgressValue { get; set; } = new();
        private Bindable<bool> IsProgressVisible { get; set; } = new();

        public KRRLVAnalysisViewModel()
        {
            _uiUpdateCounter = 0;
            FilteredOsuFiles = new Bindable<ICollectionView>(CollectionViewSource.GetDefaultView(OsuFiles.Value));
            // 设置自动绑定通知
            SetupAutoBindableNotifications();
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

                    // 每OptimalBatchSize个文件更新一次UI（使用智能批处理大小）
                    if (_uiUpdateCounter >= OptimalBatchSize)
                    {
                        Interlocked.Exchange(ref _uiUpdateCounter, 0);

                        // 异步UI更新，不阻塞后台线程
                        UpdateUIAsync();
                    }
                });
        }

        /// <summary>
        /// 异步UI更新，使用BeginInvoke避免阻塞
        /// </summary>
        private void UpdateUIAsync()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressValue.Value = (double)_currentProcessedCount / TotalCount.Value * 100;

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
                    {
                        itemsToAdd = new List<KRRLVAnalysisItem>();
                    }
                }

                foreach (var item in itemsToAdd)
                    OsuFiles.Value.Add(item);
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 更新分析结果到UI项目
        /// </summary>
        private void UpdateAnalysisResult(KRRLVAnalysisItem item, OsuAnalysisResult result)
        {
            // 异步更新UI，避免阻塞
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                item.Result = result;
                item.Status = result.Status ?? "√";
            }), DispatcherPriority.Background);
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
                var filePath = saveDialog.FileName;
                var extension = Path.GetExtension(filePath).ToLower();

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
            var exportProperties = KRRLVAnalysisColumnConfig.ExportProperties;

            // 添加CSV头部
            csv.AppendLine(string.Join(",", exportProperties.Select(p => p.Header)));

            // 添加数据行
            foreach (var file in OsuFiles.Value)
            {
                var values = exportProperties.Select(prop =>
                {
                    var value = file.Result != null ? 
                        typeof(OsuAnalysisResult).GetProperty(prop.Property)?.GetValue(file.Result) : 
                        null;
                    
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
            var worksheet = workbook.Worksheets.Add("KRR LV Analysis");

            // 使用共享的导出属性配置
            var exportProperties = KRRLVAnalysisColumnConfig.ExportProperties;

            // 添加头部
            for (var i = 0; i < exportProperties.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = exportProperties[i].Header;
            }

            // 添加数据行
            var row = 2;
            foreach (var file in OsuFiles.Value)
            {
                for (var col = 0; col < exportProperties.Length; col++)
                {
                    var propName = exportProperties[col].Property;
                    var value = file.Result != null ? 
                        typeof(OsuAnalysisResult).GetProperty(propName)?.GetValue(file.Result) : 
                        null;
                    
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
                    foreach (var item in OsuFiles.Value)
                    {
                        _itemPool.Return(item);
                    }
                    
                    OsuFiles.Value.Clear();
                    FilteredOsuFiles.Value = CollectionViewSource.GetDefaultView(OsuFiles.Value);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014
                
                // 计算总文件数（包括.osz中的.osu文件）
                var allOsuFiles = BeatmapFileHelper.EnumerateOsuFiles(files).ToArray();
                Logger.WriteLine(LogLevel.Debug, $"[DEBUG] allOsuFiles.Length = {allOsuFiles.Length}");
                TotalCount.Value = allOsuFiles.Length;
                _currentProcessedCount = 0;
                _uiUpdateCounter = 0;

                // 显示进度窗口,处理前
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsProgressVisible.Value = true;
                    ProgressValue.Value = 0;
                }, DispatcherPriority.Background);
#pragma warning restore CS4014

                const int batchSize = BatchSize; // 保持向后兼容性，但内部使用智能大小
                var batches = new List<string[]>();
                for (var i = 0; i < allOsuFiles.Length; i += batchSize)
                {
                    var batch = allOsuFiles.Skip(i).Take(batchSize).ToArray();
                    batches.Add(batch);
                }

                foreach (var batch in batches)
                {
                    await Task.Run(async () =>
                    {
                        var tasks = new List<Task>();

                        foreach (var file in batch)
                            if (Directory.Exists(file))
                            {
                                // 处理文件夹
                                var osuFiles = Directory.GetFiles(file, "*.osu", SearchOption.AllDirectories)
                                    .Where(f => Path.GetExtension(f)
                                        .Equals(".osu", StringComparison.OrdinalIgnoreCase));

                                foreach (var osuFile in osuFiles)
                                {
                                    await _semaphore.WaitAsync();
                                    var task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(osuFile)));
                                    tasks.Add(task);
                                }
                            }
                            else if (File.Exists(file) &&
                                     Path.GetExtension(file).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                            {
                                await _semaphore.WaitAsync();
                                var task = CreateProcessingTask(() => Task.Run(() => ProcessOsuFile(file)));
                                tasks.Add(task);
                            }
                            // 添加对.osz文件的支持
                            else if (File.Exists(file) &&
                                     Path.GetExtension(file).Equals(".osz", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    using var archive = ZipFile.OpenRead(file);
                                    var osuEntries = archive.Entries.Where(e =>
                                        e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));

                                    foreach (var entry in osuEntries)
                                    {
                                        await _semaphore.WaitAsync();
                                        var task = CreateProcessingTask(() =>
                                            Task.Run(() => ProcessOszEntry(entry, file)));
                                        tasks.Add(task);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERROR] 处理.osz文件失败: {ex.Message}");
                                }
                            }

                        await Task.WhenAll(tasks);
                    });

                    // 智能内存管理：只有在内存压力大时才进行GC
                    if (_currentProcessedCount % 1000 == 0) // 每处理1000个文件检查一次
                    {
                        var memoryInfo = GC.GetGCMemoryInfo();
                        if (memoryInfo.MemoryLoadBytes > 500 * 1024 * 1024) // 超过500MB
                        {
                            GC.Collect(1, GCCollectionMode.Optimized, false);
                        }
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
                        {
                            itemsToAdd = new List<KRRLVAnalysisItem>();
                        }
                    }

                    foreach (var item in itemsToAdd) OsuFiles.Value.Add(item);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014

                // 短暂延迟，让用户看到100%的进度
                await Task.Delay(300);

                // 关闭进度窗口
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    FilteredOsuFiles.Value = CollectionViewSource.GetDefaultView(OsuFiles.Value);
                    Logger.WriteLine(LogLevel.Information,
                        "[KRRLVAnalysisViewModel] FilteredOsuFiles refreshed, count: {0}",
                        FilteredOsuFiles.Value.Cast<object>().Count());
                    // _processingWindow?.Close();
                    // _processingWindow = null;
                    IsProgressVisible.Value = false;
                }, DispatcherPriority.Background);
#pragma warning restore CS4014
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
                var uniqueId = $"{oszFilePath}|{entry.FullName}";

                // 检查是否已存在于列表中
                if (OsuFiles.Value.Any(f =>
                        f.FilePath != null && f.FilePath.Equals(uniqueId, StringComparison.OrdinalIgnoreCase)))
                    return;

                var item = _itemPool.Rent();
                // item.FileName = entry.Name;
                item.FilePath = uniqueId; // 使用唯一标识符
                item.Status = "waiting";

                // 添加到待处理列表
                lock (_pendingItemsLock)
                {
                    _pendingItems.Add(item);
                }

                // 执行分析方法
                Task.Run(() => AnalyzeOszEntry(item, entry));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理.osz条目时发生异常: {ex.Message}");
            }
        }

        private async Task AnalyzeOszEntry(KRRLVAnalysisItem item, ZipArchiveEntry entry)
        {
            try
            {
                // 从.osz条目中读取内容
                await using var stream = entry.Open();
                await using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // 创建临时文件路径
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    // 将内存流写入临时文件
                    await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write,
                                     FileShare.None, 4096, true))
                    {
                        await memoryStream.CopyToAsync(fileStream);
                    }

                    // 使用 BeatmapAnalyzer 分析临时文件
                    var result = await OsuAnalyzer.AnalyzeAsync(tempFilePath);

                    // 更新基础信息
#pragma warning disable CS4014
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        UpdateAnalysisResult(item, result);
                    }, DispatcherPriority.Background);
#pragma warning restore CS4014
                }
                finally
                {
                    // 确保删除临时文件
                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                }
            }
            catch (ArgumentException ex) when (ex.Message == "no-mania")
            {
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var itemToRemove = OsuFiles.Value.FirstOrDefault(f => f.FilePath == item.FilePath);
                    if (itemToRemove != null)
                        OsuFiles.Value.Remove(itemToRemove);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 分析.osz条目时发生异常: {ex.Message}");
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() => { item.Status = $"错误: {ex.Message}"; }, DispatcherPriority.Background);
#pragma warning restore CS4014
            }
        }

        private void ProcessOsuFile(string filePath)
        {
            // 检查文件是否已存在于列表中
            if (OsuFiles.Value.Any(f =>
                    f.FilePath != null && f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                return;

            var item = _itemPool.Rent();
            // item.FileName = Path.GetFileName(filePath);
            item.FilePath = filePath;
            item.Status = "waiting";

            // 添加到待处理列表
            lock (_pendingItemsLock)
            {
                _pendingItems.Add(item);
            }

            // 执行分析方法
            Task.Run(() => Analyze(item));
        }


        private async Task Analyze(KRRLVAnalysisItem item)
        {
            try
            {
                var result = await OsuAnalyzer.AnalyzeAsync(item.FilePath!);
                
                // 更新基础信息
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    UpdateAnalysisResult(item, result);
                }, DispatcherPriority.Background);
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
#pragma warning disable CS4014
                Application.Current.Dispatcher.BeginInvoke(() => { item.Status = $"错误: {ex.Message}"; }, DispatcherPriority.Background);
#pragma warning restore CS4014
            }
        }
    }
}