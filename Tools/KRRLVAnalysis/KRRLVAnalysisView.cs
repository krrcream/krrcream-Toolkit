using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.KRRLVAnalysis
{
    /// <summary>
    /// LV分析器的列配置
    /// 统一管理UI列和导出功能的属性映射
    /// </summary>
    public static class KRRLVAnalysisColumnConfig
    {
        /// <summary>
        /// 列配置：(属性名, 显示名, 宽度, 格式)
        /// </summary>
        public static readonly (string Property, string Header, double Width, string Format)[] Columns =
        [
            ("Title", "Title", 140.0, ""),
            ("Artist", "Artist", 140.0, ""),
            ("Diff", "Diff", 140.0, ""),
            ("BPMDisplay", "BPM", double.NaN, ""),
            ("OD", "OD", double.NaN, "F1"),
            ("HP", "HP", double.NaN, "F1"),
            ("KeyCount", "Keys", double.NaN, ""),
            ("NotesCount", "Notes", double.NaN, ""),
            ("LNPercent", "LN%", double.NaN, "F2"),
            ("MaxKPS", "Max KPS", double.NaN, "F2"),
            ("AvgKPS", "Avg KPS", double.NaN, "F2"),
            ("XXY_SR", "XXY SR", double.NaN, "F2"),
            ("KRR_LV", "KRR LV", double.NaN, "F2"),
            ("YLs_LV", "YLS LV", double.NaN, "F2"),
            ("Status", "Status", double.NaN, ""),
            ("FilePath", "FilePath", double.NaN, "")
        ];

        /// <summary>
        /// 导出属性配置：(属性名, 显示名)
        /// </summary>
        public static readonly (string Property, string Header)[] ExportProperties =
        [
            ("KRR_LV", "KRR LV"),
            ("YLs_LV", "YLS LV"),
            ("XXY_SR", "XXY SR"),
            ("Title", "Title"),
            ("Diff", "Diff"),
            ("Artist", "Artist"),
            ("Creator", "Creator"),
            ("KeyCount", "Keys"),
            ("NotesCount", "Notes"),
            ("MaxKPS", "Max KPS"),
            ("AvgKPS", "Avg KPS"),
            ("BPMDisplay", "BPM"),
            ("OD", "OD"),
            ("HP", "HP"),
            ("LNPercent", "LN%"),
            ("BeatmapID", "beatmapID"),
            ("BeatmapSetID", "beatmapSetId"),
            ("FilePath", "filePath")
        ];
    }

    public class KRRLVAnalysisView : UserControl
    {
        private readonly KRRLVAnalysisViewModel _analysisViewModel;
        private DataGrid? dataGrid;

        private const string ToolName = "KRRLVAnalysis";

        public KRRLVAnalysisView()
        {
            _analysisViewModel = new KRRLVAnalysisViewModel();
            DataContext = _analysisViewModel;
            AllowDrop = true;
            Focusable = true;
            
            BuildUI();
            
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 数据表行
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮行

            // DataGrid for results
            dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                AllowDrop = true,
                // 启用虚拟化以提升大数据集性能
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true,
                // 优化渲染性能
                MaxHeight = double.PositiveInfinity,
                MaxWidth = double.PositiveInfinity
            };

            // 设置虚拟化面板的滚动单位
            VirtualizingPanel.SetScrollUnit(dataGrid, ScrollUnit.Pixel);
            dataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Value") { Source = _analysisViewModel.FilteredOsuFiles });

            LoadColumnOrder();
            dataGrid.ColumnReordered += OnColumnReordered;

            // 动态生成列
            GenerateDataGridColumns();

            // Buttons at the bottom
            var buttonGrid = new Grid { Margin = new Thickness(5) };
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var loadBtn = SharedUIComponents.CreateStandardButton("Load Folder|加载文件夹");
            loadBtn.Width = Double.NaN; 
            loadBtn.Height = 35;
            loadBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            loadBtn.Margin = new Thickness(0, 0, 5, 0); // 右边距5px，与导出按钮间隔
            loadBtn.Click += LoadBtn_Click;

            var saveBtn = SharedUIComponents.CreateStandardButton("Export|导出");
            saveBtn.Width = Double.NaN; 
            saveBtn.Height = 35;
            saveBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            saveBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(KRRLVAnalysisViewModel.SaveCommand)));

            // 测试按钮 - 临时添加用于调试进度条
            // var testBtn = SharedUIComponents.CreateStandardButton("Test Progress|测试进度");
            // testBtn.Width = Double.NaN; 
            // testBtn.Height = 35;
            // testBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            // testBtn.Click += TestBtn_Click;
            // Grid.SetColumn(testBtn, 1);
            // buttonGrid.Children.Add(testBtn);
            
            
            Grid.SetRow(dataGrid, 0);
            Grid.SetRow(buttonGrid, 1);
            Grid.SetColumn(loadBtn, 0);
            Grid.SetColumn(saveBtn, 2);

            buttonGrid.Children.Add(loadBtn);

            buttonGrid.Children.Add(saveBtn);

            root.Children.Add(dataGrid);
            root.Children.Add(buttonGrid);

            Content = root;

            Drop += Window_Drop;
            DragEnter += Window_DragEnter;
            DragOver += Window_DragOver;
        }

        private void GenerateDataGridColumns()
        {
            if (dataGrid == null) return;

            // 使用共享的列配置
            foreach (var config in KRRLVAnalysisColumnConfig.Columns)
            {
                var binding = new Binding($"Result.{config.Property}");
                if (!string.IsNullOrEmpty(config.Format))
                {
                    binding.StringFormat = config.Format;
                }

                var column = new DataGridTextColumn
                {
                    Header = config.Header,
                    Binding = binding,
                    Width = double.IsNaN(config.Width) ? DataGridLength.Auto : new DataGridLength(config.Width)
                };

                dataGrid.Columns.Add(column);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                    return;

                _analysisViewModel.ProcessDroppedFiles(files);
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ?
                DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ?
                DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var selected = FilesHelper.ShowFolderBrowserDialog("选择文件夹", owner);
            if (!string.IsNullOrEmpty(selected))
            {
                _analysisViewModel.PathInput.Value = selected;
                _analysisViewModel.ProcessDroppedFiles([selected]);
            }
        }

        private void OnLanguageChanged()
        {
            // Update UI strings if needed
        }

        private void LoadColumnOrder()
        {
            if (dataGrid == null) return;
            var config = BaseOptionsManager.GetGlobalSettings();
            if (config.DataGridColumnOrders.Value.TryGetValue(ToolName, out var orders) && orders.Count == dataGrid.Columns.Count)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    dataGrid.Columns[i].DisplayIndex = orders[i];
                }
            }
        }

        private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
        {
            SaveColumnOrder();
        }

        private void SaveColumnOrder()
        {
            if (dataGrid == null) return;
            var orders = new List<int>();
            foreach (var col in dataGrid.Columns.OrderBy(c => c.DisplayIndex))
            {
                orders.Add(dataGrid.Columns.IndexOf(col));
            }
            var config = BaseOptionsManager.GetGlobalSettings();
            config.DataGridColumnOrders.Value[ToolName] = orders;
            BaseOptionsManager.SetGlobalSettingsSilent(config);
        }

        // private void TestBtn_Click(object sender, RoutedEventArgs e)
        // {
        //     // 测试进度条功能
        //     Logger.WriteLine(LogLevel.Information, "[DEBUG] Test button clicked - testing global progress bar");
        //     
        //     // 模拟进度条显示和更新
        //     Task.Run(async () =>
        //     {
        //         await Application.Current.Dispatcher.BeginInvoke(() =>
        //         {
        //             _analysisViewModel.ProgressValue.Value = 0;
        //             Logger.WriteLine(LogLevel.Information, "[DEBUG] Test: Global progress bar shown");
        //         });
        //
        //         for (int i = 0; i <= 100; i += 10)
        //         {
        //             await Task.Delay(200);
        //             await Application.Current.Dispatcher.BeginInvoke(() =>
        //             {
        //                 _analysisViewModel.ProgressValue.Value = i;
        //                 Logger.WriteLine(LogLevel.Information, $"[DEBUG] Test: Global progress updated to {i}%");
        //             });
        //         }
        //
        //         await Task.Delay(500);
        //         await Application.Current.Dispatcher.BeginInvoke(() =>
        //         {
        //             Logger.WriteLine(LogLevel.Information, "[DEBUG] Test: Global progress bar test completed");
        //         });
        //     });
        // }
    }
}
