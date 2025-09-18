using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace krrTools.tools.DPtool
{
    public partial class DPToolWindow : Window
    {
        private DPToolViewModel _viewModel;

        public DPToolWindow()
        {
            InitializeComponent();
            _viewModel = new DPToolViewModel();
            DataContext = _viewModel;
            
            // 初始化绑定
            SetupBindings();
        }
        
        private void SetupBindings()
        {
            // 绑定复选框
            ModifyKeysCheckBox.DataContext = _viewModel.Options;
            LMirrorCheckBox.DataContext = _viewModel.Options;
            LDensityCheckBox.DataContext = _viewModel.Options;
            RMirrorCheckBox.DataContext = _viewModel.Options;
            RDensityCheckBox.DataContext = _viewModel.Options;
            
            // 绑定滑块
            KeysSlider.DataContext = _viewModel.Options;
            LMaxKeysSlider.DataContext = _viewModel.Options;
            LMinKeysSlider.DataContext = _viewModel.Options;
            RMaxKeysSlider.DataContext = _viewModel.Options;
            RMinKeysSlider.DataContext = _viewModel.Options;
            
            // 设置绑定路径
            ModifyKeysCheckBox.SetBinding(CheckBox.IsCheckedProperty, "ModifySingleSideKeyCount");
            KeysSlider.SetBinding(Slider.ValueProperty, "SingleSideKeyCount");
            LMirrorCheckBox.SetBinding(CheckBox.IsCheckedProperty, "LMirror");
            LDensityCheckBox.SetBinding(CheckBox.IsCheckedProperty, "LDensity");
            RMirrorCheckBox.SetBinding(CheckBox.IsCheckedProperty, "RMirror");
            RDensityCheckBox.SetBinding(CheckBox.IsCheckedProperty, "RDensity");
            
            LMaxKeysSlider.SetBinding(Slider.ValueProperty, "LMaxKeys");
            LMinKeysSlider.SetBinding(Slider.ValueProperty, "LMinKeys");
            RMaxKeysSlider.SetBinding(Slider.ValueProperty, "RMaxKeys");
            RMinKeysSlider.SetBinding(Slider.ValueProperty, "RMinKeys");
        }

        private void ModifyKeysCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            KeysSlider.IsEnabled = true;
            _viewModel.Options.ModifySingleSideKeyCount = true;
        }

        private void ModifyKeysCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            KeysSlider.IsEnabled = false;
            _viewModel.Options.ModifySingleSideKeyCount = false;
        }

        private void LDensityCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            LMaxKeysSlider.IsEnabled = true;
            LMinKeysSlider.IsEnabled = true;
            _viewModel.Options.LDensity = true;
        }

        private void LDensityCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            LMaxKeysSlider.IsEnabled = false;
            LMinKeysSlider.IsEnabled = false;
            _viewModel.Options.LDensity = false;
        }

        private void RDensityCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RMaxKeysSlider.IsEnabled = true;
            RMinKeysSlider.IsEnabled = true;
            _viewModel.Options.RDensity = true;
        }

        private void RDensityCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            RMaxKeysSlider.IsEnabled = false;
            RMinKeysSlider.IsEnabled = false;
            _viewModel.Options.RDensity = false;
        }

        private void LMaxKeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LMinKeysSlider != null && e.NewValue < LMinKeysSlider.Value)
            {
                LMinKeysSlider.Value = e.NewValue;
            }
            if (_viewModel != null && _viewModel.Options != null)
            {
                _viewModel.Options.LMaxKeys = (int)e.NewValue;
            }
        }

        private void RMaxKeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RMinKeysSlider != null && e.NewValue < RMinKeysSlider.Value)
            {
                RMinKeysSlider.Value = e.NewValue;
            }
            if (_viewModel != null && _viewModel.Options != null)
            {
                _viewModel.Options.RMaxKeys = (int)e.NewValue;
            }
        }
        
        private void KeysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LMaxKeysSlider != null && RMaxKeysSlider != null)
            {
                // 更新LMaxKeysSlider和RMaxKeysSlider的最大值为KeysSlider的当前值
                LMaxKeysSlider.Maximum = e.NewValue;
                RMaxKeysSlider.Maximum = e.NewValue;

                // 如果LMaxKeysSlider或RMaxKeysSlider的当前值超过了新的最大值，则调整它们的值
                if (LMaxKeysSlider.Value > e.NewValue)
                {
                    LMaxKeysSlider.Value = e.NewValue;
                }

                if (RMaxKeysSlider.Value > e.NewValue)
                {
                    RMaxKeysSlider.Value = e.NewValue;
                }

                // 同时更新LMinKeysSlider和RMinKeysSlider的最大值绑定
                LMinKeysSlider.Maximum = e.NewValue;
                RMinKeysSlider.Maximum = e.NewValue;
            }

            // 更新ViewModel中的值
            if (_viewModel != null && _viewModel.Options != null)
            {
                _viewModel.Options.SingleSideKeyCount = (int)e.NewValue;
            }
        }
        
        private async void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // 显示进度条
                _viewModel.IsProcessing = true;
                _viewModel.ProgressValue = 0;
                _viewModel.ProgressText = "Preparing to process files...";

                // 在后台线程处理文件
                await Task.Run(() => ProcessFilesWithProgress(files));

                // 处理完成后隐藏进度条
                _viewModel.IsProcessing = false;

                // 显示完成消息
                MessageBox.Show("File processing completed!", "Processing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _viewModel.ProgressMaximum = totalFiles;
                _viewModel.ProgressText = $"Processing 0 of {totalFiles} files...";
            });

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
                        var dp = new DP();
                        DP.Options = _viewModel.Options;
                        dp.ProcessFile(filePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    }

                    processedFiles++;

                    // 更新进度条
                    Dispatcher.Invoke(() =>
                    {
                        _viewModel.ProgressValue = processedFiles;
                        _viewModel.ProgressText = $"Processing {processedFiles} of {totalFiles} files...";
                    });
                }

                // 强制进行垃圾回收以释放内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        
    }
}
