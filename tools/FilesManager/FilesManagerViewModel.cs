using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using krrTools.Tools.Shared;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.tools.FilesManager
{
    
    public class OsuFileInfo
    {
        public string? Title { get; set; }
        public string? Diff { get; set; }
        public string? Artist { get; set; }
        public string? Creator { get; set; }
        public string? FilePath { get; set; }
        public int Keys { get; set; }
        public double OD { get; set; }
        public double HP { get; set; }
        public int BeatmapID { get; set; }
        public int BeatmapSetID { get; set; }
    }
    
    public partial class FilesManagerViewModel : ObservableObject
    {
        
        public FilesManagerViewModel()
        {
            // 初始化过滤视图
            _progressValue = 0;
            FilteredOsuFiles = CollectionViewSource.GetDefaultView(OsuFiles);
            FilteredOsuFiles.Filter = FilterPredicate;
        }
        
        [ObservableProperty] 
        private ObservableCollection<OsuFileInfo> _osuFiles = new();
        
        [ObservableProperty]
        private ICollectionView _filteredOsuFiles;

        // 各列的筛选条件
        [ObservableProperty]
        private string _titleFilter = "";

        [ObservableProperty]
        private string _diffFilter = "";

        [ObservableProperty]
        private string _artistFilter = "";

        [ObservableProperty]
        private string _creatorFilter = "";

        [ObservableProperty]
        private string _keysFilter = "";

        [ObservableProperty]
        private string _odFilter = "";

        [ObservableProperty]
        private string _hpFilter = "";

        [ObservableProperty]
        private string _beatmapIdFilter = "";

        [ObservableProperty]
        private string _beatmapSetIdFilter = "";

        [ObservableProperty]
        private string _filePathFilter = "";
        
        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private int _progressMaximum = 100;

        [ObservableProperty]
        private string _progressText = string.Empty;

   
        
        
        [RelayCommand]
        private async Task SetSongsFolderAsync()
        {
            var selectedPath = FilesHelper.ShowFolderBrowserDialog("Please select the osu! Songs folder");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                await ProcessAsync(selectedPath);
            }
        }

        private async Task ProcessAsync(string doPath)
        {
            IsProcessing = true;
            ProgressValue = 0;
            ProgressText = "Loading...";

            try
            {
                // 在后台线程获取文件列表（包括.osz包内osu）
                var files = await Task.Run(() => FilesHelper.EnumerateOsuFiles([doPath]).ToArray());

                ProgressMaximum = files.Length;
                ProgressText = $"Found {files.Length} files, processing...";

                // Clear existing data
                OsuFiles.Clear();

                // 分批处理文件，避免UI冻结
                const int batchSize = 50;
                var batch = new ObservableCollection<OsuFileInfo>();
                
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        // 在后台线程解析文件
                        var fileInfo = await Task.Run(() => ParseOsuFile(files[i]));
                        
                        if (fileInfo != null)
                        {
                            batch.Add(fileInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"解析文件 {files[i]} 时出错: {ex.Message}");
                    }

                    // 更新进度
                    ProgressValue = i + 1;
                    ProgressText = $"正在处理 {i + 1}/{files.Length}...";

                    // 每处理一批就更新UI
                    if (batch.Count >= batchSize || i == files.Length - 1)
                    {
                        // 在UI线程上更新数据
                        Application.Current.Dispatcher.Invoke((Action)(() =>
                        {
                            foreach (var item in batch)
                            {
                                OsuFiles.Add(item);
                            }
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
                System.Diagnostics.Debug.WriteLine($"读取文件夹时出错: {ex.Message}");
                ProgressText = $"处理出错: {ex.Message}";
            }
            finally
            {
                await Task.Delay(1000); // 显示完成信息1秒
                IsProcessing = false;
            }
        }

        private OsuFileInfo? ParseOsuFile(string filePath)
        {
            try
            {
                var beatmap = BeatmapDecoder.Decode(filePath);
                
                var fileInfo = new OsuFileInfo
                {
                    FilePath = filePath,
                    Title = beatmap.MetadataSection.Title ?? string.Empty,
                    Artist = beatmap.MetadataSection.Artist ?? string.Empty,
                    Creator = beatmap.MetadataSection.Creator ?? string.Empty,
                    OD = beatmap.DifficultySection.OverallDifficulty,
                    HP = beatmap.DifficultySection.HPDrainRate,
                    Keys = (int)beatmap.DifficultySection.CircleSize,
                    Diff = beatmap.MetadataSection.Version ?? string.Empty,
                    BeatmapID = beatmap.MetadataSection.BeatmapID,
                    BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
                };

                return fileInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析文件 {filePath} 时出错: {ex.Message}");
                return null;
            }
        }

        // 定义OsuFileInfo类来存储解析后的数据
        
        private bool FilterPredicate(object item)
        {
            if (item is not OsuFileInfo fileInfo) return false;

            // 转换为小写进行模糊匹配
            string title = fileInfo.Title?.ToLower() ?? "";
            string diff = fileInfo.Diff?.ToLower() ?? "";
            string artist = fileInfo.Artist?.ToLower() ?? "";
            string creator = fileInfo.Creator?.ToLower() ?? "";
            string keys = fileInfo.Keys.ToString();
            string od = fileInfo.OD.ToString(CultureInfo.InvariantCulture);
            string hp = fileInfo.HP.ToString(CultureInfo.InvariantCulture);
            string beatmapId = fileInfo.BeatmapID.ToString();
            string beatmapSetId = fileInfo.BeatmapSetID.ToString();
            string filePath = fileInfo.FilePath?.ToLower() ?? "";

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
        partial void OnTitleFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnDiffFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnArtistFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnCreatorFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnKeysFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnOdFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnHpFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnBeatmapIdFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnBeatmapSetIdFilterChanged(string value) { _ = value; RefreshFilter(); }
        partial void OnFilePathFilterChanged(string value) { _ = value; RefreshFilter(); }

        // 在 UI 线程上安全地刷新 ICollectionView
        private void RefreshFilter()
        {
            try
            {
                // FilteredOsuFiles 为非空成员（在构造函数中初始化），因此不再进行重复的 null 检查。
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                {
                    FilteredOsuFiles.Refresh();
                }
                else
                {
                    Application.Current?.Dispatcher?.Invoke(() => FilteredOsuFiles.Refresh());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RefreshFilter error: " + ex.Message);
            }
        }
    }
}
