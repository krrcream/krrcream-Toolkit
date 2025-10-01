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
using System.Windows.Media;
using krrTools.Data;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.FilesManager
{
    public class FilesManagerControl : UserControl
    {
        private static readonly InverseBooleanConverter _inverseBoolConverter = new InverseBooleanConverter();

        private DataGrid? _fileDataGrid;
        private TextBox? _titleFilterBox, _diffFilterBox, _artistFilterBox, _creatorFilterBox, _keysFilterBox, _odFilterBox, _hpFilterBox, _beatmapIdFilterBox, _beatmapSetIdFilterBox, _filePathFilterBox;
        private ProgressBar? _progressBarControl;
        private TextBlock? _progressTextBlockControl;
        private Grid? _topGrid;

        public FilesManagerControl()
        {
            var viewModel = new FilesManagerViewModel
            {
                Dispatcher = Dispatcher
            };
            DataContext = viewModel;
            // Initialize control UI
            BuildUI();
            // Subscribe to language change
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // Unsubscribe when unloaded
            Unloaded += (_,_) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            // control layout only; host sets size and location
            AllowDrop = true;

            var rootGrid = new Grid();
            // Apply application font/size if available so this window visually matches main UI
            var appRes = Application.Current?.Resources;
            if (appRes != null)
            {
                // Apply app-level font settings by setting Control dependency properties so we avoid direct type refs
                if (appRes.Contains("AppFontFamily"))
                    rootGrid.SetValue(FontFamilyProperty, appRes["AppFontFamily"]);
                if (appRes.Contains("AppFontSize"))
                    rootGrid.SetValue(FontSizeProperty, appRes["AppFontSize"]);
            }
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top filter grid
            _topGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Diff
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Artist
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Creator
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Keys
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // OD
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // HP
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // beatmapID
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // setID
            _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // FilePath

            _titleFilterBox = CreateBoundTextBox("TitleFilter"); _topGrid.Children.Add(PlaceInGrid(_titleFilterBox, 0));
            _diffFilterBox = CreateBoundTextBox("DiffFilter"); _topGrid.Children.Add(PlaceInGrid(_diffFilterBox, 1));
            _artistFilterBox = CreateBoundTextBox("ArtistFilter"); _topGrid.Children.Add(PlaceInGrid(_artistFilterBox, 2));
            _creatorFilterBox = CreateBoundTextBox("CreatorFilter"); _topGrid.Children.Add(PlaceInGrid(_creatorFilterBox, 3));
            _keysFilterBox = CreateBoundTextBox("KeysFilter"); _topGrid.Children.Add(PlaceInGrid(_keysFilterBox, 4));
            _odFilterBox = CreateBoundTextBox("OdFilter"); _topGrid.Children.Add(PlaceInGrid(_odFilterBox, 5));
            _hpFilterBox = CreateBoundTextBox("HpFilter"); _topGrid.Children.Add(PlaceInGrid(_hpFilterBox, 6));
            _beatmapIdFilterBox = CreateBoundTextBox("BeatmapIdFilter"); _topGrid.Children.Add(PlaceInGrid(_beatmapIdFilterBox, 7));
            _beatmapSetIdFilterBox = CreateBoundTextBox("BeatmapSetIdFilter"); _topGrid.Children.Add(PlaceInGrid(_beatmapSetIdFilterBox, 8));
            _filePathFilterBox = CreateBoundTextBox("FilePathFilter"); _topGrid.Children.Add(PlaceInGrid(_filePathFilterBox, 9));

            Grid.SetRow(_topGrid, 0);
            rootGrid.Children.Add(_topGrid);

            // DataGrid
            _fileDataGrid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow };
            _fileDataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("FilteredOsuFiles"));

            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "Diff", Binding = new Binding("Diff"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "Creator", Binding = new Binding("Creator"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "Keys", Binding = new Binding("Keys"), Width = new DataGridLength(60) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "OD", Binding = new Binding("OD"), Width = new DataGridLength(60) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "HP", Binding = new Binding("HP"), Width = new DataGridLength(60) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "beatmapID", Binding = new Binding("BeatmapID"), Width = new DataGridLength(80) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "setID", Binding = new Binding("BeatmapSetID"), Width = new DataGridLength(80) });
            _fileDataGrid.Columns.Add(new DataGridTextColumn { Header = "FilePath", Binding = new Binding("FilePath"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });

            // Sync top grid column widths with DataGrid columns
            _fileDataGrid.LayoutUpdated += (_,_) =>
            {
                if (_topGrid != null && _fileDataGrid != null)
                {
                    for (int i = 0; i < Math.Min(_topGrid.ColumnDefinitions.Count, _fileDataGrid.Columns.Count); i++)
                    {
                        _topGrid.ColumnDefinitions[i].Width = new GridLength(_fileDataGrid.Columns[i].ActualWidth);
                    }
                }
            };

            // Context menu
            var ctx = new ContextMenu();
            var delMenu = new MenuItem { Header = "Delete", Foreground = Brushes.Black };
            delMenu.Click += DeleteMenuItem_Click;
            ctx.Items.Add(delMenu);
            _fileDataGrid.ContextMenu = ctx;

            Grid.SetRow(_fileDataGrid, 1);
            rootGrid.Children.Add(_fileDataGrid);

            // Bottom area
            var bottomGrid = new Grid { Margin = new Thickness(10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var progressPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            _progressBarControl = new ProgressBar { Width = 200, Height = 20 };
            _progressBarControl.SetBinding(RangeBase.ValueProperty, new Binding("ProgressValue"));
            _progressBarControl.SetBinding(RangeBase.MaximumProperty, new Binding("ProgressMaximum"));
            BindingOperations.SetBinding(_progressBarControl, VisibilityProperty, new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
            _progressTextBlockControl = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            _progressTextBlockControl.SetBinding(TextBlock.TextProperty, new Binding("ProgressText"));
            BindingOperations.SetBinding(_progressTextBlockControl, VisibilityProperty, new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
            progressPanel.Children.Add(_progressBarControl);
            progressPanel.Children.Add(_progressTextBlockControl);
            Grid.SetColumn(progressPanel, 0);
            bottomGrid.Children.Add(progressPanel);

            var setSongsBtn = SharedUIComponents.CreateStandardButton("Set Songs Path|设置 Songs 目录");
            setSongsBtn.Width = 160; setSongsBtn.Height = 40;
            setSongsBtn.Click += SetSongsBtn_Click;
            // disable when processing (inverse)
            setSongsBtn.SetBinding(IsEnabledProperty, new Binding("IsProcessing") { Converter = _inverseBoolConverter });
            Grid.SetColumn(setSongsBtn, 1);
            bottomGrid.Children.Add(setSongsBtn);

            Grid.SetRow(bottomGrid, 2);
            rootGrid.Children.Add(bottomGrid);

            // Set control content
            Content = rootGrid;

            // Add drag and drop events
            Drop += OnDrop;
            DragEnter += OnDragEnter;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                {
                    var vm = (FilesManagerViewModel)DataContext;
                    vm.ProcessDroppedFiles(files);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ?
                DragDropEffects.Copy : DragDropEffects.None;
        }

        private FrameworkElement PlaceInGrid(UIElement element, int column)
        {
            Grid.SetColumn(element, column);
            return (FrameworkElement)element;
        }

        private TextBox CreateBoundTextBox(string path)
        {
            var tb = new TextBox { Height = 20, Margin = new Thickness(0,0,0,5), Width = Double.NaN };
            tb.SetBinding(TextBox.TextProperty, new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            return tb;
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = (_fileDataGrid?.SelectedItems.Cast<FilesManagerInfo>().ToList()) ??
                                    new List<FilesManagerInfo>();

                if (selectedItems.Count == 0)
                {
                    MessageBox.Show(Strings.NoItemsSelected.Localize(), Strings.Delete.Localize(), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string message = "Are you sure you want to delete " + selectedItems.Count +
                                 " .osu file(s)? This action cannot be undone.";
                string caption = "Confirm Delete";

                var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var vm = (FilesManagerViewModel)DataContext;
                    await DeleteSelectedFilesAsync(selectedItems, vm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.ErrorDeletingFiles.Localize() + ": " + ex.Message, Strings.DeleteError.Localize(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //异步后台删除文件
        private Task DeleteSelectedFilesAsync(List<FilesManagerInfo> filesToDelete, FilesManagerViewModel managerViewModel)
        {
            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file.FilePath))
                    {
                        File.Delete(file.FilePath);
                    }

                    // 从主列表和过滤视图中移除
                    managerViewModel.OsuFiles.Remove(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete {file.FilePath}: {ex.Message}");
                }
            }

            // 刷新ICollectionView以更新UI
            managerViewModel.FilteredOsuFiles.Refresh();

            MessageBox.Show(string.Format(Strings.FilesDeletedSuccessfullyTemplate.Localize(), filesToDelete.Count), Strings.Delete.Localize(), MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void SetSongsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                {
                    var selectedPath = FilesHelper.ShowFolderBrowserDialog("Please select the osu! Songs folder", owner);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        var vm = (FilesManagerViewModel)DataContext;
                        await vm.ProcessAsync(selectedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error,"Error setting Songs path: " + ex.Message);
            }
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;
            return false;
        }
    }
}
