using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using Microsoft.Extensions.Logging;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.Tools.FilesManager
{
    public partial class FilesManagerViewModel : ReactiveViewModelBase
    {
        // TODO: 改为WPF控件，更换输入框过滤功能的控件功能，找一个类似Excel的筛选功能
        private readonly Bindable<ObservableCollection<FilesManagerInfo>> _osuFiles = new(new ObservableCollection<FilesManagerInfo>());
        private readonly Bindable<ICollectionView> _filteredOsuFiles;
        private readonly Bindable<bool> _isProcessing = new();
        private readonly Bindable<int> _progressValue = new();
        private readonly Bindable<int> _progressMaximum = new(100);
        private readonly Bindable<string> _progressText = new(string.Empty);
        private readonly Bindable<string> _selectedFolderPath = new("");

        public FilesManagerViewModel()
        {
            // 初始化数据源
            _filteredOsuFiles = new Bindable<ICollectionView>(CollectionViewSource.GetDefaultView(OsuFiles));

            // 连接Bindable属性的PropertyChanged事件到ViewModel的PropertyChanged事件
            _osuFiles.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OsuFiles));
            _filteredOsuFiles.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FilteredOsuFiles));
            _isProcessing.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsProcessing));
            _progressValue.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ProgressValue));
            _progressMaximum.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ProgressMaximum));
            _progressText.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ProgressText));
            _selectedFolderPath.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedFolderPath));
        }

        public ObservableCollection<FilesManagerInfo> OsuFiles
        {
            get => _osuFiles.Value;
            set => _osuFiles.Value = value;
        }

        public ICollectionView FilteredOsuFiles
        {
            get => _filteredOsuFiles.Value;
            set => _filteredOsuFiles.Value = value;
        }

        public bool IsProcessing
        {
            get => _isProcessing.Value;
            set => _isProcessing.Value = value;
        }

        public int ProgressValue
        {
            get => _progressValue.Value;
            set => _progressValue.Value = value;
        }

        public int ProgressMaximum
        {
            get => _progressMaximum.Value;
            set => _progressMaximum.Value = value;
        }

        public string ProgressText
        {
            get => _progressText.Value;
            set => _progressText.Value = value;
        }

        public string SelectedFolderPath
        {
            get => _selectedFolderPath.Value;
            set => _selectedFolderPath.Value = value;
        }


        [RelayCommand]
        private async Task SetSongsFolderAsync()
        {
            var selectedPath = FilesHelper.ShowFolderBrowserDialog("Please select the osu! Songs folder");
            if (!string.IsNullOrEmpty(selectedPath)) await ProcessAsync(selectedPath);
        }

        public async void ProcessDroppedFiles(string[] files)
        {
            try
            {
                if (files.Length == 1 && Directory.Exists(files[0]))
                    await ProcessAsync(files[0]);
                else
                    // For multiple files or single file, process as individual files
                    await ProcessFilesAsync(files);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] ProcessDroppedFiles error: {0}", ex.Message);
                ProgressText = $"处理出错: {ex.Message}";
            }
        }

        private async Task ProcessFilesAsync(string[] files)
        {
            IsProcessing = true;
            ProgressValue = 0;
            ProgressText = "Loading...";

            try
            {
                var validFiles = files.Where(f =>
                    File.Exists(f) && Path.GetExtension(f).Equals(".osu", StringComparison.OrdinalIgnoreCase)).ToArray();
                ProgressMaximum = validFiles.Length;
                ProgressText = $"Found {validFiles.Length} files, processing...";

                // Set selected folder path to the directory of the first file
                if (validFiles.Length > 0) SelectedFolderPath = Path.GetDirectoryName(validFiles[0]) ?? "";

                // Clear existing data
                OsuFiles.Clear();

                // 分批处理文件，避免UI冻结
                const int batchSize = 50;
                var batch = new ObservableCollection<FilesManagerInfo>();

                for (var i = 0; i < validFiles.Length; i++)
                {
                    try
                    {
                        // 在后台线程解析文件
                        var fileInfo = await Task.Run(() => ParseOsuFile(validFiles[i]));

                        if (fileInfo != null) batch.Add(fileInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] 解析文件 {0} 时出错: {1}", validFiles[i],
                            ex.Message);
                    }

                    // 更新进度
                    ProgressValue = i + 1;
                    ProgressText = $"正在处理 {i + 1}/{validFiles.Length}...";

                    // 每处理一批就更新UI
                    if (batch.Count >= batchSize || i == validFiles.Length - 1)
                    {
                        // 在UI线程上更新数据
                        Application.Current.Dispatcher.Invoke((Action)(() =>
                        {
                            foreach (var item in batch) OsuFiles.Add(item);
                        }));

                        batch.Clear();

                        // 短暂延迟，让UI有机会更新
                        await Task.Delay(1);
                    }
                }

                ProgressText = $"完成处理 {validFiles.Length} 个文件";
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] 读取文件时出错: {0}", ex.Message);
                ProgressText = $"处理出错: {ex.Message}";
            }
            finally
            {
                await Task.Delay(1000); // 显示完成信息1秒
                IsProcessing = false;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    FilteredOsuFiles = CollectionViewSource.GetDefaultView(OsuFiles);
                    Logger.WriteLine(LogLevel.Information,
                        "[FilesManagerViewModel] FilteredOsuFiles refreshed in ProcessFilesAsync, count: {0}",
                        FilteredOsuFiles.Cast<object>().Count());
                });
            }
        }

        public async Task ProcessAsync(string doPath)
        {
            SelectedFolderPath = doPath;
            ProgressValue = 0;
            ProgressText = "Loading...";
        
            try
            {
                // 在后台线程获取文件列表（包括.osz包内osu）
                var files = await Task.Run(() => BeatmapFileHelper.EnumerateOsuFiles([doPath]).ToArray());

                Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] Enumerated {0} files from {1}",
                    files.Length, doPath);

                ProgressMaximum = files.Length;
                ProgressText = $"Found {files.Length} files, processing...";

                // Clear existing data
                OsuFiles.Clear();
                FilteredOsuFiles.Refresh();

                // 分批处理文件，避免UI冻结
                const int batchSize = 50;
                var batch = new ObservableCollection<FilesManagerInfo>();

                for (var i = 0; i < files.Length; i++)
                {
                    try
                    {
                        // 在后台线程解析文件
                        var fileInfo = await Task.Run(() => ParseOsuFile(files[i]));

                        if (fileInfo != null)
                        {
                            batch.Add(fileInfo);

                        }
                        else
                        {
                            Logger.WriteLine(LogLevel.Warning, "[FilesManagerViewModel] Failed to parse {0}", files[i]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] 解析文件 {0} 时出错: {1}", files[i], ex.Message);
                    }

                    // 更新进度
                    ProgressValue = i + 1;
                    ProgressText = $"正在处理 {i + 1}/{files.Length}...";

                    // 每处理一批就更新UI
                    if (batch.Count >= batchSize || i == files.Length - 1)
                    {
                        // 在UI线程上更新数据
                        Application.Current?.Dispatcher?.Invoke((Action)(() =>
                        {
                            foreach (var item in batch) OsuFiles.Add(item);
                            FilteredOsuFiles.Refresh();
                        }));

                        batch.Clear();

                        // 短暂延迟，让UI有机会更新
                        await Task.Delay(1);
                    }
                }

                ProgressText = $"完成处理 {files.Length} 个文件";
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] 读取文件夹时出错: {0}", ex.Message);
                ProgressText = $"处理出错: {ex.Message}";
                Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] Error in ProcessAsync: {0}", ex.Message);
            }
            finally
            {
                await Task.Delay(120); // 显示完成信息
                IsProcessing = false;

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    FilteredOsuFiles = CollectionViewSource.GetDefaultView(OsuFiles);
                    Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] FilteredOsuFiles refreshed, count: {0}",
                        FilteredOsuFiles.Cast<object>().Count());
                });
            }
        }

        private FilesManagerInfo? ParseOsuFile(string filePath)
        {
            try
            {
                var beatmap = BeatmapDecoder.Decode(filePath);

                var fileInfo = new FilesManagerInfo
                {
                    FilePath = { Value = filePath },
                    Title = { Value = beatmap.MetadataSection.Title ?? string.Empty },
                    Artist = { Value = beatmap.MetadataSection.Artist ?? string.Empty },
                    Creator = { Value = beatmap.MetadataSection.Creator ?? string.Empty },
                    OD = { Value = beatmap.DifficultySection.OverallDifficulty },
                    HP = { Value = beatmap.DifficultySection.HPDrainRate },
                    Keys = (int)beatmap.DifficultySection.CircleSize,
                    Diff = { Value = beatmap.MetadataSection.Version ?? string.Empty },
                    BeatmapID = beatmap.MetadataSection.BeatmapID,
                    BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
                };

                return fileInfo;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] 解析文件 {0} 时出错: {1}", filePath, ex.Message);
                return null;
            }
        }

        // 定义OsuFileInfo类来存储解析后的数据


    }
}