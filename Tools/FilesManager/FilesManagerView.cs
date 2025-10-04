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
using Microsoft.Extensions.Logging;
using System.Windows.Media;
using krrTools.Data;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.FilesManager;

public class FilesManagerView : UserControl
{
    private readonly FilesManagerViewModel _viewModel;

    // 取反，数据加载时禁用按钮
    private static readonly InverseBoolBtn _inverseBoolBtn = new();

    private DataGrid? _fileDataGrid;

    private TextBox? _titleFilterBox,
        _diffFilterBox,
        _artistFilterBox,
        _creatorFilterBox;
    private ComboBox? _keysFilterBox;
    private TextBox? _odFilterBox,
        _hpFilterBox,
        _beatmapIdFilterBox,
        _beatmapSetIdFilterBox,
        _filePathFilterBox;

    private ProgressBar? _progressBarControl;
    private TextBlock? _progressTextBlockControl;
    private Grid? _topGrid;

    public FilesManagerView()
    {
        _viewModel = new FilesManagerViewModel();
        DataContext = _viewModel;

        BuildUI();

        SharedUIComponents.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            if (DataContext is FilesManagerViewModel vm)
            {
                vm.FilteredOsuFiles.Refresh();
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
        AllowDrop = true;

        var rootGrid = new Grid();

        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        BuildFilterSection(rootGrid);
        BuildDataSection(rootGrid);
        BuildActionSection(rootGrid);

        Content = rootGrid;

        Drop += OnDrop;
        DragEnter += OnDragEnter;
        PreviewDrop += OnDrop;
        PreviewDragEnter += OnDragEnter;
    }

    private void BuildFilterSection(Grid rootGrid)
    {
        // Top filter grid
        _topGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 5),
            Height = 40,
        };
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Diff
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Artist
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Creator
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Keys
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // OD
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // HP
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // beatmapID
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // setID
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) }); // FilePath
        _topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Clear button

        _titleFilterBox = CreateBoundTextBox("TitleFilter");
        _topGrid.Children.Add(PlaceInGrid(_titleFilterBox, 0));
        _diffFilterBox = CreateBoundTextBox("DiffFilter");
        _topGrid.Children.Add(PlaceInGrid(_diffFilterBox, 1));
        _artistFilterBox = CreateBoundTextBox("ArtistFilter");
        _topGrid.Children.Add(PlaceInGrid(_artistFilterBox, 2));
        _creatorFilterBox = CreateBoundTextBox("CreatorFilter");
        _topGrid.Children.Add(PlaceInGrid(_creatorFilterBox, 3));
        _keysFilterBox = CreateBoundComboBox("KeysFilter", ["", "4", "5", "6", "7", "8", "9", "10", "12", "14", "16", "18"]);
        _topGrid.Children.Add(PlaceInGrid(_keysFilterBox, 4));
        _odFilterBox = CreateBoundTextBox("OdFilter");
        _topGrid.Children.Add(PlaceInGrid(_odFilterBox, 5));
        _hpFilterBox = CreateBoundTextBox("HpFilter");
        _topGrid.Children.Add(PlaceInGrid(_hpFilterBox, 6));
        _beatmapIdFilterBox = CreateBoundTextBox("BeatmapIdFilter");
        _topGrid.Children.Add(PlaceInGrid(_beatmapIdFilterBox, 7));
        _beatmapSetIdFilterBox = CreateBoundTextBox("BeatmapSetIdFilter");
        _topGrid.Children.Add(PlaceInGrid(_beatmapSetIdFilterBox, 8));
        _filePathFilterBox = CreateBoundTextBox("FilePathFilter");
        _topGrid.Children.Add(PlaceInGrid(_filePathFilterBox, 9));

        // Add clear filters button
        var clearButton = new Button
        {
            Content = "清除过滤",
            Height = 20,
            Margin = new Thickness(5, 0, 0, 5),
            Padding = new Thickness(10, 0, 10, 0)
        };
        clearButton.Click += ClearFiltersButton_Click;
        _topGrid.Children.Add(PlaceInGrid(clearButton, 10));

        Grid.SetRow(_topGrid, 0);
        rootGrid.Children.Add(_topGrid);
    }

    private void BuildDataSection(Grid rootGrid)
    {
        // DataGrid
        _fileDataGrid = new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow
        };
        _fileDataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("FilteredOsuFiles"));

        _fileDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Artist", Binding = new Binding("Artist.Value"),
            Width = 120
        });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Title", Binding = new Binding("Title.Value"), Width = 120
        });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Creator", Binding = new Binding("Creator.Value"),
            Width = DataGridLength.Auto
        });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Diff", Binding = new Binding("Diff.Value"), Width = 120
        });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
            { Header = "Keys", Binding = new Binding("Keys"), Width = DataGridLength.SizeToHeader, IsReadOnly = true });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
            { Header = "OD", Binding = new Binding("OD.Value"), Width = DataGridLength.SizeToHeader });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
            { Header = "HP", Binding = new Binding("HP.Value"), Width = DataGridLength.SizeToHeader });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
            { Header = "beatmapID", Binding = new Binding("BeatmapID"), Width = DataGridLength.Auto, IsReadOnly = true });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
            { Header = "setID", Binding = new Binding("BeatmapSetID"), Width = DataGridLength.Auto, IsReadOnly = true });
        _fileDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "FilePath", Binding = new Binding("FilePath.Value"),
            Width = DataGridLength.Auto
        });

        // Sync top grid column widths with DataGrid columns
        _fileDataGrid.LayoutUpdated += (_, _) =>
        {
            if (_topGrid != null && _fileDataGrid != null)
                for (var i = 0; i < Math.Min(_topGrid.ColumnDefinitions.Count, _fileDataGrid.Columns.Count); i++)
                    _topGrid.ColumnDefinitions[i].Width = new GridLength(_fileDataGrid.Columns[i].ActualWidth);
        };

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
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Path display
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) }); // Progress
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Load button

        var pathTextBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
            FontWeight = FontWeights.Bold
        };
        pathTextBlock.SetBinding(TextBlock.TextProperty, new Binding("SelectedFolderPath"));
        bottomGrid.Children.Add(pathTextBlock);
        Grid.SetColumn(pathTextBlock, 0);

        var progressPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        _progressBarControl = new ProgressBar { Width = 200, Height = 20 };
        _progressBarControl.SetBinding(RangeBase.ValueProperty, new Binding("ProgressValue"));
        _progressBarControl.SetBinding(RangeBase.MaximumProperty, new Binding("ProgressMaximum"));
        BindingOperations.SetBinding(_progressBarControl, VisibilityProperty,
            new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
        _progressTextBlockControl = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        };
        _progressTextBlockControl.SetBinding(TextBlock.TextProperty, new Binding("ProgressText"));
        BindingOperations.SetBinding(_progressTextBlockControl, VisibilityProperty,
            new Binding("IsProcessing") { Converter = new BooleanToVisibilityConverter() });
        progressPanel.Children.Add(_progressBarControl);
        progressPanel.Children.Add(_progressTextBlockControl);

        bottomGrid.Children.Add(progressPanel);
        Grid.SetColumn(progressPanel, 1);

        var loadBtn = SharedUIComponents.CreateStandardButton("Load Folder|加载文件夹");
        loadBtn.Width = double.NaN;
        loadBtn.Height = 40;
        loadBtn.Click += SetSongsBtn_Click;

        loadBtn.SetBinding(IsEnabledProperty, new Binding("IsProcessing") { Converter = _inverseBoolBtn });
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

    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FilesManagerViewModel vm)
        {
            vm.TitleFilter = "";
            vm.DiffFilter = "";
            vm.ArtistFilter = "";
            vm.CreatorFilter = "";
            vm.KeysFilter = "";
            vm.OdFilter = "";
            vm.HpFilter = "";
            vm.BeatmapIdFilter = "";
            vm.BeatmapSetIdFilter = "";
            vm.FilePathFilter = "";
        }
    }

    private FrameworkElement PlaceInGrid(UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        return (FrameworkElement)element;
    }

    private TextBox CreateBoundTextBox(string path)
    {
        var tb = new TextBox { Height = 20, Margin = new Thickness(0, 0, 0, 5), Width = double.NaN };
        tb.SetBinding(TextBox.TextProperty,
            new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return tb;
    }

    private ComboBox CreateBoundComboBox(string path, string[] items)
    {
        var cb = new ComboBox { Height = 20, Margin = new Thickness(0, 0, 0, 5), Width = double.NaN, ItemsSource = items };
        cb.SetBinding(Selector.SelectedItemProperty,
            new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return cb;
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
                managerViewModel.OsuFiles.Remove(file);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[FilesManagerView] Failed to delete {0}: {1}", file.FilePath.Value,
                    ex.Message);
            }

        // 刷新ICollectionView以更新UI
        managerViewModel.FilteredOsuFiles.Refresh();

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

    private void OnDataGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            foreach (var item in _viewModel.OsuFiles)
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
                await vm.ProcessAsync(selectedPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error setting Songs path: {ex.Message}");
        }
    }
}

public class InverseBoolBtn : IValueConverter
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