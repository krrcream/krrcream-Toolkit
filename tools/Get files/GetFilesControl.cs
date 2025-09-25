using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using krrTools.Tools.Shared;

#pragma warning disable CS8618 // Non-nullable fields are initialized in BuildUI

namespace krrTools.tools.Get_files
{
    public class GetFilesControl : UserControl
    {
        private static readonly InverseBooleanConverter _inverseBoolConverter = new InverseBooleanConverter();

        private DataGrid? _fileDataGrid;
        private TextBox? _titleFilterBox, _diffFilterBox, _artistFilterBox, _creatorFilterBox, _keysFilterBox, _odFilterBox, _hpFilterBox, _beatmapIdFilterBox, _beatmapSetIdFilterBox, _filePathFilterBox;
        private ProgressBar? _progressBarControl;
        private TextBlock? _progressTextBlockControl;

        public GetFilesControl()
        {
            // Initialize control UI
            BuildUI();
            var viewModel = new GetFilesViewModel();
            this.DataContext = viewModel;
            // Subscribe to language change
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // Unsubscribe when unloaded
            this.Unloaded += (_,_) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();
            // Apply application font/size if available so this window visually matches main UI
            var appRes = Application.Current?.Resources;
            if (appRes != null)
            {
                // Apply app-level font settings by setting Control dependency properties so we avoid direct type refs
                if (appRes.Contains("AppFontFamily"))
                    rootGrid.SetValue(System.Windows.Controls.Control.FontFamilyProperty, appRes["AppFontFamily"]);
                if (appRes.Contains("AppFontSize"))
                    rootGrid.SetValue(System.Windows.Controls.Control.FontSizeProperty, appRes["AppFontSize"]);
            }
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top filter grid
            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            for (int i = 0; i < 10; i++) topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _titleFilterBox = CreateBoundTextBox("TitleFilter"); topGrid.Children.Add(PlaceInGrid(_titleFilterBox, 0));
            _diffFilterBox = CreateBoundTextBox("DiffFilter"); topGrid.Children.Add(PlaceInGrid(_diffFilterBox, 1));
            _artistFilterBox = CreateBoundTextBox("ArtistFilter"); topGrid.Children.Add(PlaceInGrid(_artistFilterBox, 2));
            _creatorFilterBox = CreateBoundTextBox("CreatorFilter"); topGrid.Children.Add(PlaceInGrid(_creatorFilterBox, 3));
            _keysFilterBox = CreateBoundTextBox("KeysFilter"); topGrid.Children.Add(PlaceInGrid(_keysFilterBox, 4));
            _odFilterBox = CreateBoundTextBox("OdFilter"); topGrid.Children.Add(PlaceInGrid(_odFilterBox, 5));
            _hpFilterBox = CreateBoundTextBox("HpFilter"); topGrid.Children.Add(PlaceInGrid(_hpFilterBox, 6));
            _beatmapIdFilterBox = CreateBoundTextBox("BeatmapIdFilter"); topGrid.Children.Add(PlaceInGrid(_beatmapIdFilterBox, 7));
            _beatmapSetIdFilterBox = CreateBoundTextBox("BeatmapSetIdFilter"); topGrid.Children.Add(PlaceInGrid(_beatmapSetIdFilterBox, 8));
            _filePathFilterBox = CreateBoundTextBox("FilePathFilter"); topGrid.Children.Add(PlaceInGrid(_filePathFilterBox, 9));

            Grid.SetRow(topGrid, 0);
            rootGrid.Children.Add(topGrid);

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
            BindingOperations.SetBinding(_progressBarControl, System.Windows.UIElement.VisibilityProperty, new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
            _progressTextBlockControl = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            _progressTextBlockControl.SetBinding(TextBlock.TextProperty, new Binding("ProgressText"));
            BindingOperations.SetBinding(_progressTextBlockControl, System.Windows.UIElement.VisibilityProperty, new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
            progressPanel.Children.Add(_progressBarControl);
            progressPanel.Children.Add(_progressTextBlockControl);
            Grid.SetColumn(progressPanel, 0);
            bottomGrid.Children.Add(progressPanel);

            var setSongsBtn = SharedUIComponents.CreateStandardButton("Set Songs Path|设置 Songs 目录");
            setSongsBtn.Width = 160; setSongsBtn.Height = 40;
            setSongsBtn.SetBinding(ButtonBase.CommandProperty, new Binding("SetSongsFolderCommand"));
            // disable when processing (inverse)
            setSongsBtn.SetBinding(IsEnabledProperty, new Binding("IsProcessing") { Converter = _inverseBoolConverter });
            Grid.SetColumn(setSongsBtn, 1);
            bottomGrid.Children.Add(setSongsBtn);

            Grid.SetRow(bottomGrid, 2);
            rootGrid.Children.Add(bottomGrid);

            // Set control content
            this.Content = rootGrid;
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
            var selectedItems = (_fileDataGrid?.SelectedItems.Cast<OsuFileInfo>().ToList()) ?? new List<OsuFileInfo>();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No items selected.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = "Are you sure you want to delete " + selectedItems.Count + " .osu file(s)? This action cannot be undone.";
            string caption = "Confirm Delete";

            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var vm = (GetFilesViewModel)DataContext;
                await DeleteSelectedFilesAsync(selectedItems, vm);
            }
        }

        //异步后台删除文件
        private Task DeleteSelectedFilesAsync(List<OsuFileInfo> filesToDelete, GetFilesViewModel viewModel)
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
                    viewModel.OsuFiles.Remove(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete {file.FilePath}: {ex.Message}");
                }
            }

            // 刷新ICollectionView以更新UI
            viewModel.FilteredOsuFiles.Refresh();

            MessageBox.Show($"{filesToDelete.Count} file(s) deleted successfully.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        private void OnLanguageChanged()
        {
            try
            {
                // Rebuild UI on language change
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var dc = this.DataContext;
                    Content = null;
                    BuildUI();
                    DataContext = dc;
                }));
            }
            catch { }
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
