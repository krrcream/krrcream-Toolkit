using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using krrTools;
using krrTools.Data;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.Tools.FilesManager;

public partial class FilesManagerViewModel : ObservableObject
{
    // TODO: 改为WPF控件，更换输入框过滤功能的控件功能，找一个类似Excel的筛选功能
    public FilesManagerViewModel()
    {
        // 初始化过滤视图
        _progressValue = 0;
        FilteredOsuFiles = CollectionViewSource.GetDefaultView(OsuFiles);
        FilteredOsuFiles.Filter = FilterPredicate;
        Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] FilteredOsuFiles initialized with {0} items",
            OsuFiles.Count);
    }

    [ObservableProperty] private ObservableCollection<FilesManagerInfo> _osuFiles = new();

    [ObservableProperty] private ICollectionView _filteredOsuFiles;

    // 各列的筛选条件
    [ObservableProperty] private string _titleFilter = "";

    [ObservableProperty] private string _diffFilter = "";

    [ObservableProperty] private string _artistFilter = "";

    [ObservableProperty] private string _creatorFilter = "";

    [ObservableProperty] private string _keysFilter = "";

    [ObservableProperty] private string _odFilter = "";

    [ObservableProperty] private string _hpFilter = "";

    [ObservableProperty] private string _beatmapIdFilter = "";

    [ObservableProperty] private string _beatmapSetIdFilter = "";

    [ObservableProperty] private string _filePathFilter = "";

    [ObservableProperty] private bool _isProcessing;

    [ObservableProperty] private int _progressValue;

    [ObservableProperty] private int _progressMaximum = 100;

    [ObservableProperty] private string _progressText = string.Empty;

    [ObservableProperty] private string _selectedFolderPath = "";


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
                FilteredOsuFiles.Filter = FilterPredicate;
                OnPropertyChanged(nameof(FilteredOsuFiles));
                Logger.WriteLine(LogLevel.Information,
                    "[FilesManagerViewModel] FilteredOsuFiles refreshed in ProcessFilesAsync, count: {0}",
                    FilteredOsuFiles.Cast<object>().Count());
            });
        }
    }

    public async Task ProcessAsync(string doPath)
    {
        Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] Starting ProcessAsync for {0}", doPath);
        SelectedFolderPath = doPath;
        Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] SelectedFolderPath set to: {0}",
            SelectedFolderPath);
        ProgressValue = 0;
        ProgressText = "Loading...";
        var parsedCount = 0;

        try
        {
            // 在后台线程获取文件列表（包括.osz包内osu）
            var files = await Task.Run(() => FilesHelper.EnumerateOsuFiles([doPath]).ToArray());

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
                        parsedCount++;
                        Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] Parsed {0} successfully",
                            files[i]);
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
                        Logger.WriteLine(LogLevel.Information,
                            "[FilesManagerViewModel] Added {0} items to OsuFiles, total count: {1}", batch.Count,
                            OsuFiles.Count);
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
            Logger.WriteLine(LogLevel.Information, "[FilesManagerViewModel] ProcessAsync completed, parsed {0} files",
                parsedCount);
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                FilteredOsuFiles = CollectionViewSource.GetDefaultView(OsuFiles);
                FilteredOsuFiles.Filter = FilterPredicate;
                OnPropertyChanged(nameof(FilteredOsuFiles));
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

    private bool FilterPredicate(object item)
    {
        if (item is not FilesManagerInfo fileInfo) return false;

        // 转换为小写进行模糊匹配
        var title = fileInfo.Title.Value.ToLower();
        var diff = fileInfo.Diff.Value.ToLower();
        var artist = fileInfo.Artist.Value.ToLower();
        var creator = fileInfo.Creator.Value.ToLower();
        var keys = fileInfo.Keys.ToString();
        var od = fileInfo.OD.Value.ToString(CultureInfo.InvariantCulture);
        var hp = fileInfo.HP.Value.ToString(CultureInfo.InvariantCulture);
        var beatmapId = fileInfo.BeatmapID.ToString();
        var beatmapSetId = fileInfo.BeatmapSetID.ToString();
        var filePath = fileInfo.FilePath.Value.ToLower();

        // 检查是否满足所有筛选条件
        return (string.IsNullOrEmpty(TitleFilter) || title.Contains(TitleFilter.ToLower())) &&
               (string.IsNullOrEmpty(DiffFilter) || diff.Contains(DiffFilter.ToLower())) &&
               (string.IsNullOrEmpty(ArtistFilter) || artist.Contains(ArtistFilter.ToLower())) &&
               (string.IsNullOrEmpty(CreatorFilter) || creator.Contains(CreatorFilter.ToLower())) &&
               (string.IsNullOrEmpty(KeysFilter) || keys.Contains(KeysFilter)) &&
               (string.IsNullOrEmpty(OdFilter) || od.Contains(OdFilter)) &&
               (string.IsNullOrEmpty(HpFilter) || hp.Contains(HpFilter)) &&
               (string.IsNullOrEmpty(BeatmapIdFilter) || beatmapId.Contains(BeatmapIdFilter)) &&
               (string.IsNullOrEmpty(BeatmapSetIdFilter) || beatmapSetId.Contains(BeatmapSetIdFilter)) &&
               (string.IsNullOrEmpty(FilePathFilter) || filePath.Contains(FilePathFilter.ToLower()));
    }

    // 当任一筛选属性发生变化时，刷新过滤视图以更新UI。
    // CommunityToolkit 的 [ObservableProperty] 会生成以下形式的局部方法：
    // partial void On<PropertyName>Changed(<type> value)
    // 在这里实现这些方法以在属性变更时触发过滤刷新。
    partial void OnTitleFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnDiffFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnArtistFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnCreatorFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnKeysFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnOdFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnHpFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnBeatmapIdFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnBeatmapSetIdFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    partial void OnFilePathFilterChanged(string value)
    {
        _ = value;
        RefreshFilter();
    }

    // 在 UI 线程上安全地刷新 ICollectionView
    private void RefreshFilter()
    {
        try
        {
            // FilteredOsuFiles 为非空成员（在构造函数中初始化），因此不再进行重复的 null 检查。
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
                FilteredOsuFiles.Refresh();
            else
                Application.Current?.Dispatcher?.Invoke(() => FilteredOsuFiles.Refresh());
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[FilesManagerViewModel] RefreshFilter error: {0}", ex.Message);
        }
    }
}