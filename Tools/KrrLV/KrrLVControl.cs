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
            Unloaded += (_,_) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            // control layout only; host sets size and location
            AllowDrop = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock
            {
                Text = "Drag and drop .osu files or folders here.",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33))
            };
            Grid.SetRow(info, 0);
            root.Children.Add(info);

            var buttonGrid = new Grid { Margin = new Thickness(10) };
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var browseBtn = SharedUIComponents.CreateStandardButton("Browse|浏览");
            browseBtn.Width = 100; browseBtn.Height = 40;
            browseBtn.Click += BrowseBtn_Click;
            Grid.SetColumn(browseBtn, 0);
            buttonGrid.Children.Add(browseBtn);

            var openPathBtn = SharedUIComponents.CreateStandardButton("Open Path|打开路径");
            openPathBtn.Width = 120; openPathBtn.Height = 40;
            openPathBtn.SetBinding(ButtonBase.CommandProperty, new Binding("OpenPathCommand"));
            Grid.SetColumn(openPathBtn, 1);
            buttonGrid.Children.Add(openPathBtn);

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

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
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

                Dispatcher.BeginInvoke(new Action(() =>
                {

                        var dc = DataContext;
                        Content = null;
                        BuildUI();
                        DataContext = dc;

                }));

        }
    }
}