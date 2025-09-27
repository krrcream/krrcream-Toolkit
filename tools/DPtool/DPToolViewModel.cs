using krrTools.tools.Shared;

namespace krrTools.tools.DPtool
{
    public class DPToolViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public DPToolViewModel()
        {
                var saved = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.ConfigFileName);
                if (saved != null)
                {
                    Options = saved;
                }
        }

        private bool _isProcessing;
        private DPToolOptions _options = new DPToolOptions();

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        /// <summary>
        /// DP工具选项
        /// </summary>
        public DPToolOptions Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }
    }
}