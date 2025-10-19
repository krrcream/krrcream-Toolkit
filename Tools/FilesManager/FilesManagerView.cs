using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DataGridExtensions;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.FilesManager
{
    public class FilesManagerView : UserControl 
    {
        private readonly FilesManagerViewModel _viewModel;
        private DataGrid? _fileDataGrid;

        private const string ToolName = "FilesManager";

        public FilesManagerView()
        {
            _viewModel = new FilesManagerViewModel();
            DataContext = _viewModel;
            AllowDrop = true;
            Focusable = true;
        
            BuildUI();

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Loaded += (_, _) =>
            {
                if (DataContext is FilesManagerViewModel)
                {
                    // DataGridExtensions will handle filtering automatically
                }
                else
                {
                    Logger.WriteLine(LogLevel.Information, "[FilesManagerView] Loaded, DataContext is null");
                }
            };
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            BuildDataSection(rootGrid);
            BuildActionSection(rootGrid);

            Content = rootGrid;

            Drop += OnDrop;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            // PreviewDrop += OnDrop;
            // PreviewDragEnter += OnDragEnter;
        }



        private void BuildDataSection(Grid rootGrid)
        {
            // DataGrid
            _fileDataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                // IsReadOnly = true, // 设置为只读模式，提高选中灵敏度
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                CanUserSortColumns = true, // 启用排序
                CanUserReorderColumns = true, // 允许列重新排序
                CanUserResizeColumns = true, // 允许调整列宽
                // AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)), // 浅色交替行，带透明度，但是会和选中高亮冲突
                GridLinesVisibility = DataGridGridLinesVisibility.All, // 显示网格线
                RowHeaderWidth = 50, // 行号列宽度
                IsEnabled = true, // 确保控件启用
                Focusable = true, // 确保可以获得焦点
                AllowDrop = true
            };

            // 启用 DataGridExtensions 高级功能
            _fileDataGrid.SetValue(DataGridFilter.IsAutoFilterEnabledProperty, true);                    
            _fileDataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Value") { Source = _viewModel.FilteredOsuFiles });

            LoadColumnOrder();
            _fileDataGrid.ColumnReordered += OnColumnReordered;

            // 设置选中行样式 - 更明显的蓝色高亮
            var rowStyle = new Style(typeof(DataGridRow));
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243)))); // 蓝色高亮
            rowStyle.Triggers.Add(selectedTrigger);
            _fileDataGrid.RowStyle = rowStyle;

            _fileDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Artist", Binding = new Binding("Artist.Value"),
                Width = 120,
                IsReadOnly = false // 可编辑
            });
            _fileDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Title", Binding = new Binding("Title.Value"), 
                Width = 120,
                IsReadOnly = false // 可编辑
            });
            _fileDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Creator", Binding = new Binding("Creator.Value"),
                Width = DataGridLength.Auto,
                IsReadOnly = false // 可编辑
            });
            _fileDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Diff", Binding = new Binding("Diff.Value"), 
                Width = 120,
                IsReadOnly = false // 可编辑
            });
            _fileDataGrid.Columns.Add(new DataGridTextColumn
                { Header = "Keys", Binding = new Binding("Keys"), Width = DataGridLength.SizeToHeader, IsReadOnly = true }); // 只读
            _fileDataGrid.Columns.Add(new DataGridTextColumn
                { Header = "OD", Binding = new Binding("OD.Value"), Width = DataGridLength.SizeToHeader, IsReadOnly = false }); // 可编辑
            _fileDataGrid.Columns.Add(new DataGridTextColumn
                { Header = "HP", Binding = new Binding("HP.Value"), Width = DataGridLength.SizeToHeader, IsReadOnly = false }); // 可编辑
            _fileDataGrid.Columns.Add(new DataGridTextColumn
                { Header = "beatmapID", Binding = new Binding("BeatmapID"), Width = DataGridLength.Auto, IsReadOnly = true }); // 只读
            _fileDataGrid.Columns.Add(new DataGridTextColumn
                { Header = "setID", Binding = new Binding("BeatmapSetID"), Width = DataGridLength.Auto, IsReadOnly = true }); // 只读
            _fileDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "FilePath", Binding = new Binding("FilePath.Value"),
                Width = DataGridLength.Auto,
                IsReadOnly = false // 可编辑
            });

            // 冻结前3列，方便查看
            _fileDataGrid.FrozenColumnCount = 3;

            // 启用列过滤
            foreach (var column in _fileDataGrid.Columns)
            {
                column.SetValue(DataGridFilterColumn.IsFilterVisibleProperty, true);
            }

            // Context menu
            var ctx = new ContextMenu();
            var delMenu = new MenuItem { Header = "Delete", Foreground = Brushes.Black };
            delMenu.Click += DeleteMenuItem_Click;
            ctx.Items.Add(delMenu);
            _fileDataGrid.ContextMenu = ctx;

            // Add Ctrl+Z undo event
            _fileDataGrid.KeyDown += OnDataGridKeyDown;

            Grid.SetRow(_fileDataGrid, 1);
            rootGrid.Children.Add(_fileDataGrid);
        }

        private void BuildActionSection(Grid rootGrid)
        {
            // Bottom area
            var bottomGrid = new Grid { Margin = new Thickness(10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Stats
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) }); // Progress
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Load button

            // 统计信息面板
            var statsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var totalFilesText = new TextBlock
            {
                Text = "Total: ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            var totalFilesValue = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            totalFilesValue.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = _viewModel.TotalFileCount });

            var filteredFilesText = new TextBlock
            {
                Text = "Files: ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 5, 0)
            };
            var filteredFilesValue = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Blue
            };
            filteredFilesValue.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = _viewModel.FilteredFileCount, FallbackValue = "0" });            statsPanel.Children.Add(totalFilesText);
            statsPanel.Children.Add(totalFilesValue);
            statsPanel.Children.Add(filteredFilesText);
            statsPanel.Children.Add(filteredFilesValue);

            bottomGrid.Children.Add(statsPanel);
            Grid.SetColumn(statsPanel, 0);

            var progressPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            bottomGrid.Children.Add(progressPanel);
            Grid.SetColumn(progressPanel, 1);

            var loadBtn = SharedUIComponents.CreateStandardButton("Load Folder|加载文件夹");
            loadBtn.Width = double.NaN;
            loadBtn.Height = 40;
            loadBtn.Click += SetSongsBtn_Click;

            bottomGrid.Children.Add(loadBtn);
            Grid.SetColumn(loadBtn, 2);

            Grid.SetRow(bottomGrid, 2);
            rootGrid.Children.Add(bottomGrid);
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                {
                    var vm = (FilesManagerViewModel)DataContext;
                    if (vm != null)
                        vm.ProcessDroppedFiles(files);
                    else
                        Logger.WriteLine(LogLevel.Error, "[FilesManagerView] DataContext is null");
                }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = _fileDataGrid?.SelectedItems.Cast<FilesManagerInfo>().ToList() ??
                                    new List<FilesManagerInfo>();

                if (selectedItems.Count == 0)
                {
                    MessageBox.Show(Strings.NoItemsSelected.Localize(), Strings.Delete.Localize(), MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var message = "Are you sure you want to delete " + selectedItems.Count +
                              " .osu file(s)? This action cannot be undone.";
                var caption = "Confirm Delete";

                var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var vm = (FilesManagerViewModel)DataContext;
                    await DeleteSelectedFilesAsync(selectedItems, vm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.ErrorDeletingFiles.Localize() + ": " + ex.Message, Strings.DeleteError.Localize(),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //异步后台删除文件
        private Task DeleteSelectedFilesAsync(List<FilesManagerInfo> filesToDelete, FilesManagerViewModel managerViewModel)
        {
            foreach (var file in filesToDelete)
                try
                {
                    if (File.Exists(file.FilePath.Value)) File.Delete(file.FilePath.Value);

                    // 从主列表和过滤视图中移除
                    managerViewModel.OsuFiles.Value.Remove(file);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[FilesManagerView] Failed to delete {0}: {1}", file.FilePath.Value,
                        ex.Message);
                }

            // 刷新ICollectionView以更新UI
            managerViewModel.FilteredOsuFiles.Value.Refresh();

            MessageBox.Show(string.Format(Strings.FilesDeletedSuccessfullyTemplate.Localize(), filesToDelete.Count),
                Strings.Delete.Localize(), MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dc = DataContext;
                Content = null;
                BuildUI();
                DataContext = dc;
            }));
        }

        private void LoadColumnOrder()
        {
            if (_fileDataGrid == null) return;
            var config = BaseOptionsManager.GetGlobalSettings();
            if (config.DataGridColumnOrders.Value.TryGetValue(ToolName, out var orders) && orders.Count == _fileDataGrid.Columns.Count)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    _fileDataGrid.Columns[i].DisplayIndex = orders[i];
                }
            }
        }

        private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
        {
            SaveColumnOrder();
        }

        private void SaveColumnOrder()
        {
            if (_fileDataGrid == null) return;
            var orders = new List<int>();
            foreach (var col in _fileDataGrid.Columns.OrderBy(c => c.DisplayIndex))
            {
                orders.Add(_fileDataGrid.Columns.IndexOf(col));
            }
            var config = BaseOptionsManager.GetGlobalSettings();
            config.DataGridColumnOrders.Value[ToolName] = orders;
            BaseOptionsManager.SetGlobalSettingsSilent(config);
        }

        private void OnDataGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                foreach (var item in _viewModel.OsuFiles.Value)
                {
                    item.UndoAll();
                }
                e.Handled = true;
            }
        }

        private async void SetSongsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var owner = Window.GetWindow(this);
                var selectedPath = FilesHelper.ShowFolderBrowserDialog("Please select the osu! Songs folder", owner);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    var vm = (FilesManagerViewModel)DataContext;
                    vm.SelectedFolderPath.Value = selectedPath;
                    await vm.ProcessFilesAsync(BeatmapFileHelper.EnumerateOsuFiles([selectedPath]).ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error setting Songs path: {ex.Message}");
            }
        }
    }
}