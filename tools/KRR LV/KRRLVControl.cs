using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Tools.Shared;

namespace krrTools.tools.KRR_LV
{
    public class KRRLVControl : UserControl
    {
        private readonly KRRLVViewModel _viewModel;

        public KRRLVControl()
        {
            // Initialize view and ViewModel
            BuildUI();
            _viewModel = new KRRLVViewModel();
            this.DataContext = _viewModel;
            // subscribe to language changes
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // unsubscribe when unloaded
            this.Unloaded += (_,_) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void BuildUI()
        {
            // control layout only; host sets size and location
            this.AllowDrop = true;

            var root = new Grid();
            var info = new TextBlock
            {
                Text = "Drag and drop .osu files or folders here.",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21))
            };
            root.Children.Add(info);
            Content = root;

            this.Drop += Window_Drop;
            this.DragEnter += Window_DragEnter;
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
        // When control is unloaded, dispose ViewModel
        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _viewModel.Dispose();
        }

        private void OnLanguageChanged()
        {

                this.Dispatcher.BeginInvoke(new Action(() =>
                {

                        var dc = this.DataContext;
                        Content = null;
                        BuildUI();
                        DataContext = dc;

                }));

        }
    }
}