using System.Windows;

namespace krrTools.UI
{
    public partial class ProcessingWindow : Window
    {
        public ProcessingWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int current, int total)
        {
            progressBar.Maximum = total;
            progressBar.Value = current;
            progressText.Text = $"进度: {current} / {total}";
        }
    }
}