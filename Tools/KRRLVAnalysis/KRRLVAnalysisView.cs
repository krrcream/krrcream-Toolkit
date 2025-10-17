using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.UI;

namespace krrTools.Tools.KRRLVAnalysis
{
    public class KRRLVAnalysisView : UserControl
    {
        private readonly KRRLVAnalysisViewModel _analysisViewModel;
        private DataGrid? dataGrid;

        private const string ToolName = "KRRLVAnalysis";

        public KRRLVAnalysisView()
        {
            _analysisViewModel = new KRRLVAnalysisViewModel();
            DataContext = _analysisViewModel;

            BuildUI();
            
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            // control layout only; host sets size and location
            AllowDrop = true;
            Focusable = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // DataGrid for results
            dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                AllowDrop = true
            };
            dataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("OsuFiles.Value"));

            LoadColumnOrder();
            dataGrid.ColumnReordered += OnColumnReordered;

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = 140 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = 140 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Diff", Binding = new Binding("Diff"), Width = 140 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "BPM", Binding = new Binding("BPM"), Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "OD", Binding = new Binding("OD") { StringFormat = "F1" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "HP", Binding = new Binding("HP") { StringFormat = "F1" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Keys", Binding = new Binding("Keys"), Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Notes", Binding = new Binding("NotesCount"), Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "LN%", Binding = new Binding("LNPercent") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Max KPS", Binding = new Binding("MaxKPS") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Avg KPS", Binding = new Binding("AvgKPS") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "XXY SR", Binding = new Binding("XxySR") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "KRR LV", Binding = new Binding("KrrLV") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "YLS LV", Binding = new Binding("YlsLV") { StringFormat = "F2" }, Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding("Status"), Width = DataGridLength.Auto });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "FileName", Binding = new Binding("FileName"), Width = DataGridLength.Auto });
            
            Grid.SetRow(dataGrid, 0);
            root.Children.Add(dataGrid);

            var buttonGrid = new Grid { Margin = new Thickness(5) };
            buttonGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            buttonGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            buttonGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            
            var progressBar = new ProgressBar
            {
                Height = 20,
                Width = Double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
                Minimum = 0,
                Maximum = 100
            };
            progressBar.SetBinding(RangeBase.ValueProperty, new Binding("ProgressValue.Value"));
            progressBar.SetBinding(VisibilityProperty, new Binding("IsProgressVisible.Value")
            {
                Converter = new BooleanToVisibilityConverter()
            });

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
            saveBtn.SetBinding(ButtonBase.CommandProperty, new Binding("SaveCommand"));

            Grid.SetRow(progressBar, 0);
            Grid.SetColumnSpan(progressBar, 2); // 进度条跨两列
            Grid.SetRow(loadBtn, 1);
            Grid.SetColumn(loadBtn, 0);
            Grid.SetRow(saveBtn, 1);
            Grid.SetColumn(saveBtn, 1);
            buttonGrid.Children.Add(progressBar);
            buttonGrid.Children.Add(loadBtn);
            buttonGrid.Children.Add(saveBtn);

            Grid.SetRow(buttonGrid, 1);
            root.Children.Add(buttonGrid);

            Content = root;

            Drop += Window_Drop;
            DragEnter += Window_DragEnter;
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
    }
}
