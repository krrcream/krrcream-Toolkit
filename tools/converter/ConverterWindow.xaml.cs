using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using System.Text.Json;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;

namespace krrTools.Tools.Converter
{
    public partial class ConverterWindow : Window
    {
        private ConverterViewModel _viewModel;
        
        private readonly string _configPath;
        
        public ConverterWindow()
        {
            InitializeComponent();
            _viewModel = new ConverterViewModel();
            DataContext = _viewModel;
            
            // 获取项目根目录并构建配置文件路径
            string projectDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(projectDirectory, "converterConfig.fq");
            
            // 加载配置
            LoadConfiguration();
            
            // 注册窗口关闭事件
            this.Closing += ConverterWindow_Closing;
        }

        private async void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                
                // 显示进度条
                ProgressPanel.Visibility = Visibility.Visible;
                ConversionProgressBar.Value = 0;
                ProgressTextBlock.Text = "Preparing to process files...";
                
                // 在后台线程处理文件
                await Task.Run(() => ProcessFilesWithProgress(files));
                
                // 处理完成后隐藏进度条
                ProgressPanel.Visibility = Visibility.Collapsed;
                
                // 显示完成消息
                MessageBox.Show("File conversion completed!", "Conversion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ProcessFilesWithProgress(string[] files)
        {
            var allOsuFiles = new List<string>();
            
            // 收集所有.osu文件
            foreach (string path in files)
            {
                if (File.Exists(path))
                {
                    if (Path.GetExtension(path).ToLower() == ".osu")
                    {
                        allOsuFiles.Add(path);
                    }
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        string[] osuFiles = Directory.GetFiles(path, "*.osu", SearchOption.AllDirectories);
                        allOsuFiles.AddRange(osuFiles);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing directory {path}: {ex.Message}");
                    }
                }
            }
            
            int totalFiles = allOsuFiles.Count;
            int processedFiles = 0;
            
            // 更新UI显示总文件数
            Dispatcher.Invoke(() =>
            {
                ConversionProgressBar.Maximum = totalFiles;
                ProgressTextBlock.Text = $"Processing 0 of {totalFiles} files...";
            });
            
            // 设置转换选项
            var converter = new Converter();
            converter.options = _viewModel.GetConversionOptions();
            
            // 分批处理文件，每批处理1000个文件
            const int batchSize = 1000;
            for (int i = 0; i < allOsuFiles.Count; i += batchSize)
            {
                var batch = allOsuFiles.Skip(i).Take(batchSize).ToList();
                
                // 处理当前批次的文件
                foreach (string filePath in batch)
                {
                    try
                    {
                        converter.NTONC(filePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    }
                    
                    processedFiles++;
                    
                    // 更新进度条
                    Dispatcher.Invoke(() =>
                    {
                        ConversionProgressBar.Value = processedFiles;
                        ProgressTextBlock.Text = $"Processing {processedFiles} of {totalFiles} files...";
                    });
                }
                
                // 强制进行垃圾回收以释放内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

                // 窗口关闭时保存配置
        private void ConverterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfiguration();
        }

        // 保存配置到文件
        private void SaveConfiguration()
        {
            try
            {
                var config = new
                {
                    TargetKeys = _viewModel.TargetKeys,
                    MaxKeys = _viewModel.MaxKeys,
                    MinKeys = _viewModel.MinKeys,
                    TransformSpeed = _viewModel.TransformSpeed,
                    Seed = _viewModel.Seed,
                    Is4KSelected = _viewModel.Is4KSelected,
                    Is5KSelected = _viewModel.Is5KSelected,
                    Is6KSelected = _viewModel.Is6KSelected,
                    Is7KSelected = _viewModel.Is7KSelected,
                    Is8KSelected = _viewModel.Is8KSelected,
                    Is9KSelected = _viewModel.Is9KSelected,
                    Is10KSelected = _viewModel.Is10KSelected,
                    Is10KPlusSelected = _viewModel.Is10KPlusSelected
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                // 可以选择记录日志或显示错误消息
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        // 从文件加载配置
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("TargetKeys", out JsonElement targetKeys))
                        _viewModel.TargetKeys = targetKeys.GetDouble();
                    
                    if (root.TryGetProperty("MaxKeys", out JsonElement maxKeys))
                        _viewModel.MaxKeys = maxKeys.GetDouble();
                    
                    if (root.TryGetProperty("MinKeys", out JsonElement minKeys))
                        _viewModel.MinKeys = minKeys.GetDouble();
                    
                    if (root.TryGetProperty("TransformSpeed", out JsonElement transformSpeed))
                        _viewModel.TransformSpeed = transformSpeed.GetDouble();
                    
                    if (root.TryGetProperty("Seed", out JsonElement seed) && seed.ValueKind != JsonValueKind.Null)
                        _viewModel.Seed = seed.GetInt32();
                    
                    // 加载复选框状态
                    if (root.TryGetProperty("Is4KSelected", out JsonElement is4KSelected))
                        _viewModel.Is4KSelected = is4KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is5KSelected", out JsonElement is5KSelected))
                        _viewModel.Is5KSelected = is5KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is6KSelected", out JsonElement is6KSelected))
                        _viewModel.Is6KSelected = is6KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is7KSelected", out JsonElement is7KSelected))
                        _viewModel.Is7KSelected = is7KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is8KSelected", out JsonElement is8KSelected))
                        _viewModel.Is8KSelected = is8KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is9KSelected", out JsonElement is9KSelected))
                        _viewModel.Is9KSelected = is9KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is10KSelected", out JsonElement is10KSelected))
                        _viewModel.Is10KSelected = is10KSelected.GetBoolean();
                    
                    if (root.TryGetProperty("Is10KPlusSelected", out JsonElement is10KPlusSelected))
                        _viewModel.Is10KPlusSelected = is10KPlusSelected.GetBoolean();
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认值
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
        }
        
        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void TargetKeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 当目标键数改变时，根据规则更新最大键数滑块位置
            if (_viewModel != null && MaxKeysSlider != null)
            {
                if (_viewModel.MaxKeys >= 10)
                    MaxKeysSlider.Value = 10;
                else
                    MaxKeysSlider.Value = _viewModel.TargetKeys;
            }
    
            // 同步更新最小键数滑块的最大值
            if (_viewModel != null && MinKeysSlider != null)
            {
                MinKeysSlider.Maximum = _viewModel.MinKeysMaximum;
                // 当最大键数改变时，更新最小键数的值
                // 如果最大键数等于1，最小键数等于1；否则最小键数等于2
                if (_viewModel.MaxKeys == 1)
                    _viewModel.MinKeys = 1;
                else
                    _viewModel.MinKeys = 2;
        
                // 确保最小键数不超过最大键数
                if (_viewModel.MinKeys > _viewModel.MinKeysMaximum)
                    MinKeysSlider.Value = _viewModel.MinKeysMaximum;
            }
        }
        
        private void GenerateSeedButton_Click(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            SeedTextBox.Text = random.Next().ToString();
        }
        
        private void PresetHyperlink_Click(object sender, RoutedEventArgs e)
        {
            // 传递当前的 ViewModel 和当前窗口实例
            PresetWindow presetWindow = new PresetWindow(this.DataContext as ConverterViewModel, this);
            presetWindow.Owner = this;
            presetWindow.ShowDialog();
        }

        public void ApplyPreset(PresetData preset)
        {
            _viewModel.TargetKeys = preset.TargetKeys;
            _viewModel.MaxKeys = preset.MaxKeys;
            _viewModel.MinKeys = preset.MinKeys;
            _viewModel.TransformSpeed = preset.TransformSpeed;
            _viewModel.Seed = preset.Seed;
        }
        private void OpenOsuListenerButton_Click(object sender, RoutedEventArgs e)
        {
            var listenerWindow = new ListenerView(this, 1); // 1表示Converter窗口
            listenerWindow.Show();
        }

        // 添加处理单个文件的方法
        public void ProcessSingleFile(string filePath)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "File Not Found", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
        
                // 检查文件扩展名是否为.osu
                if (Path.GetExtension(filePath).ToLower() != ".osu")
                {
                    MessageBox.Show("Selected file is not a valid .osu file", "Invalid File", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
        
                var converter = new Converter();
                converter.options = _viewModel.GetConversionOptions();
                string newFilepath = converter.NTONC(filePath);
                OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath);
                
                MessageBox.Show("File processed successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing file: {ex.Message}", "Processing Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        
    }
}
