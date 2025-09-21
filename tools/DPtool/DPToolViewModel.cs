using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using krrTools.tools.Listener;

namespace krrTools.tools.DPtool
{
    public class DPToolViewModel : ObservableObject
    {
        private bool _isProcessing = false;
        private double _progressValue = 0;
        private double _progressMaximum = 100;
        private string _progressText = "Ready";
        private DPToolOptions _options = new DPToolOptions();

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public double ProgressMaximum
        {
            get => _progressMaximum;
            set => SetProperty(ref _progressMaximum, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }
        
        /// <summary>
        /// DP工具选项
        /// </summary>
        public DPToolOptions Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }
        
        /// <summary>
        /// 打开osu!监听器窗口
        /// </summary>
        public void OpenOsuListener()
        {
            var listenerWindow = new ListenerView();
            listenerWindow.Show();
        }
    }
}