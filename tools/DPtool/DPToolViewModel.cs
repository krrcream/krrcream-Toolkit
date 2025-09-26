using krrTools.tools.Shared;

namespace krrTools.tools.DPtool
{
    public class DPToolViewModel : ObservableObject
    {
        public DPToolViewModel()
        {
            // Try to load saved options; if none, keep defaults
            try
            {
                var saved = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.OptionsFileName);
                if (saved != null)
                {
                    Options = saved;
                }
            }
            catch
            {
                // best-effort load; ignore errors
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