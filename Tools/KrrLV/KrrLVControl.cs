using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Data;
using krrTools.UI;

namespace krrTools.Tools.KrrLV
{
    public class KrrLVControl : UserControl
    {
        private readonly KrrLVViewModel _viewModel;

        public KrrLVControl()
        {
            _viewModel = new KrrLVViewModel();
            DataContext = _viewModel;
            // Initialize view and ViewModel
            BuildUI();
            // subscribe to language changes
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // unsubscribe when unloaded
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            // control layout only; host sets size and location
            AllowDrop = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // DataGrid for results
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };
            dataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("OsuFiles"));
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "FileName", Binding = new Binding("FileName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Diff", Binding = new Binding("Diff"), Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Keys", Binding = new Binding("Keys"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "BPM", Binding = new Binding("BPM"), Width = 80 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "OD", Binding = new Binding("OD"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "HP", Binding = new Binding("HP"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "LN%", Binding = new Binding("LNPercent"), Width = 80 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "XXY SR", Binding = new Binding("XxySR"), Width = 80 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "KRR LV", Binding = new Binding("KrrLV"), Width = 80 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "YLS LV", Binding = new Binding("YlsLV"), Width = 80 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding("Status"), Width = 100 });

            Grid.SetRow(dataGrid, 0);
            root.Children.Add(dataGrid);

            var buttonGrid = new Grid { Margin = new Thickness(10) };
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var loadBtn = SharedUIComponents.CreateStandardButton("Load Folder|加载文件夹");
            loadBtn.Width = 150; loadBtn.Height = 40;
            loadBtn.Click += LoadBtn_Click;
            Grid.SetColumn(loadBtn, 0);
            buttonGrid.Children.Add(loadBtn);

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

                _viewModel.ProcessDroppedFiles(files);
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
                _viewModel.PathInput = selected;
                _viewModel.ProcessDroppedFiles([selected]);
            }
        }

        private void OnLanguageChanged()
        {
            // Update UI strings if needed
        }
    }
}
