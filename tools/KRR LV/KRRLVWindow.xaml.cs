using System;
using System.IO;
using System.Windows;

namespace krrTools.Tools.KRRLV
{
    public partial class KRRLVWindow : Window
    {
        private KRRLVViewModel _viewModel;

        public KRRLVWindow()
        {
            InitializeComponent();
            _viewModel = new KRRLVViewModel();
            DataContext = _viewModel;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                _viewModel.ProcessDroppedFiles(files);
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}