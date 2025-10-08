using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Localization;
using Border = Wpf.Ui.Controls.Border;
using Button = Wpf.Ui.Controls.Button;
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.UI
{
    public sealed class FileDropZone : Border
    {
        private readonly TextBlock DropHint;
        private readonly Button StartConversionButton;
        private readonly FileDropZoneViewModel _viewModel;

        // 本地化字符串对象
        private readonly DynamicLocalizedString _startButtonTextLocalized = new(Strings.StartButtonText);

        public FileDropZone(FileDropZoneViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            AllowDrop = true;
            Background = new SolidColorBrush(Color.FromArgb(160, 245, 248, 255));
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 175, 200, 255));
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(6);
            Margin = new Thickness(8, 2, 8, 5);
            Padding = new Thickness(12);
            Height = 60;

            DropHint = new TextBlock
            {
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xB5)),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            DropHint.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding("DisplayText"));

            StartConversionButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 6, 8, 6),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(8, 0, 6, 0),
                MinWidth = 92,
            };
            StartConversionButton.SetBinding(ContentControl.ContentProperty, new Binding("Value") { Source = _startButtonTextLocalized });
            StartConversionButton.SetBinding(VisibilityProperty, new Binding("IsConversionEnabled") { Converter = new BooleanToVisibilityConverter() });

            InitializeUI();
        }

        private void InitializeUI()
        {
            Child = new Grid()
            {
                Children = { DropHint, StartConversionButton }
            };

            Drop += OnDrop;
            StartConversionButton.Click += StartConversionButton_Click;
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += FileDropZone_Unloaded;
        }

        private void OnLanguageChanged()
        {
            // ViewModel handles localization
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            var osuFiles = _viewModel.CollectOsuFiles(files);
            if (osuFiles.Count == 0) return;

            _viewModel.SetFiles(osuFiles.ToArray(), source: FileDropZoneViewModel.FileSource.Dropped);
        }

        private void StartConversionButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConvertFiles();
        }

        private void FileDropZone_Unloaded(object sender, RoutedEventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        // Public methods for external access
        public void SetManualMode(string[]? files, FileDropZoneViewModel.FileSource source = FileDropZoneViewModel.FileSource.Dropped)
        {
            _viewModel.SetFiles(files, source);
        }
    }
}