// E:\Mug\OSU tool\krrtool\krrTools\krrTools\MainWindow.xaml.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using krrTools.Tools.GetFiles;
using krrTools.Tools.Converter;

namespace krrTools
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void KRR_LV_Button_Click(object sender, RoutedEventArgs e)
        {
            // 直接打开 KRRLVWindow 窗口
            var krrlvWindow = new krrTools.Tools.KRRLV.KRRLVWindow();
            krrlvWindow.Show();
        }
        
        private void GetFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var getFilesWindow = new GetFilesWindow();
            getFilesWindow.Show();
        }
        
        private void ConverterButton_Click(object sender, RoutedEventArgs e)
        {
            var converterWindow = new ConverterWindow();
            converterWindow.Show();
        }
        
    }
}