using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace krrTools.Tools.Converter
{
    public partial class ConverterWindow : Window
    {
        private ConverterViewModel _viewModel;
        
        public ConverterWindow()
        {
            InitializeComponent();
            _viewModel = new ConverterViewModel();
            DataContext = _viewModel;
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
            Converter.options = _viewModel.GetConversionOptions();
            
            // 处理每个文件
            foreach (string filePath in allOsuFiles)
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
    }
}
