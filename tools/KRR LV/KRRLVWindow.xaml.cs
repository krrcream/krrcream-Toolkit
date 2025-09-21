using System;
using System.Windows;

namespace krrTools.tools.KRR_LV
{
    public partial class KRRLVWindow
    {
        private readonly KRRLVViewModel _viewModel;

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
        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}