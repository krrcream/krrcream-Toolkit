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
using Button = Wpf.Ui.Controls.Button;

namespace krrTools.Tools.KRRLVAnalysis
{
#region LV分析器的列配置， 统一管理UI列和导出功能的属性映射

    public static class KRRLVAnalysisColumnConfig
    {
        /// <summary>
        /// 列配置：(属性名, 显示名, 宽度, 格式)
        /// </summary>
        public static readonly (string Property, string Header, double Width, string Format)[] Columns =
        [
            ("XXY_SR", "XXY SR", double.NaN, "F2"),
            ("KRR_LV", "KRR LV", double.NaN, "F2"),
            ("YLs_LV", "YLS LV", double.NaN, "F2"),

            ("MaxKPS", "Max KPS", double.NaN, "F2"),
            ("AvgKPS", "Avg KPS", double.NaN, "F2"),
            ("LN_Percent", "LN%", double.NaN, "F2"),
            ("KeyCount", "Keys", double.NaN, ""),
            ("NotesCount", "Notes", double.NaN, ""),

            ("Title", "Title", double.NaN, ""),
            ("Artist", "Artist", double.NaN, ""),
            ("Diff", "Diff", double.NaN, ""),
            ("Creator", "Creator", double.NaN, ""),
            ("BPMDisplay", "BPM", double.NaN, ""),
            ("OD", "OD", double.NaN, "F1"),
            ("HP", "HP", double.NaN, "F1"),
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
            ("LN_Percent", "LN%"),
            ("BeatmapID", "beatmapID"),
            ("BeatmapSetID", "beatmapSetId"),
            ("FilePath", "filePath")
        ];
    }

#endregion

    public class KRRLVAnalysisView : UserControl
    {
        private readonly KRRLVAnalysisViewModel _analysisViewModel;
        private DataGrid? dataGrid;

        // 用于保存和加载列顺序的工具名称
        private const string ToolName = "KRRLVAnalysis";

        public KRRLVAnalysisView()
        {
            _analysisViewModel = new KRRLVAnalysisViewModel();
            DataContext = _analysisViewModel;
            AllowDrop = true;
            Focusable = true;

            BuildUI();
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
            dataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Value") { Source = _analysisViewModel.OsuFiles });

            LoadColumnOrder();
            dataGrid.ColumnReordered += OnColumnReordered;

            // 动态生成列
            GenerateDataGridColumns();

            // Buttons at the bottom
            var buttonGrid = new Grid { Margin = new Thickness(5) };
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button loadBtn = SharedUIComponents.CreateStandardButton("Load Folder|加载文件夹");
            loadBtn.Width = double.NaN;
            loadBtn.Height = 35;
            loadBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            // loadBtn.Margin              =  new Thickness(0, 0, 5, 0); // 右边距5px，与导出按钮间隔
            loadBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(KRRLVAnalysisViewModel.BrowseCommand)));

            Button saveBtn = SharedUIComponents.CreateStandardButton("Export|导出");
            saveBtn.Width = double.NaN;
            saveBtn.Height = 35;
            saveBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            saveBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(KRRLVAnalysisViewModel.SaveCommand)));

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
            foreach ((string Property, string Header, double Width, string Format) config in KRRLVAnalysisColumnConfig.Columns)
            {
                string bindingPath = $"Result.{config.Property}"; // 通过Result属性访问OsuAnalysisResult的属性

                var binding = new Binding(bindingPath);

                if (!string.IsNullOrEmpty(config.Format)) binding.StringFormat = config.Format;

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
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                    return;

                _analysisViewModel.ProcessDroppedFiles(files);
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void LoadColumnOrder()
        {
            if (dataGrid == null) return;

            GlobalSettings config = BaseOptionsManager.GetGlobalSettings();

            if (config.DataGridColumnOrders.Value.TryGetValue(ToolName, out List<int>? orders) && orders.Count == dataGrid.Columns.Count)
            {
                for (int i = 0; i < orders.Count; i++)
                    dataGrid.Columns[i].DisplayIndex = orders[i];
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

            foreach (DataGridColumn? col in dataGrid.Columns.OrderBy(c => c.DisplayIndex)) orders.Add(dataGrid.Columns.IndexOf(col));

            GlobalSettings config = BaseOptionsManager.GetGlobalSettings();
            config.DataGridColumnOrders.Value[ToolName] = orders;
            BaseOptionsManager.SetGlobalSettingsSilent(config);
        }
    }
}
