using krrTools.tools.Listener;
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

        public bool IsProcessing
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// DP工具选项
        /// </summary>
        public DPToolOptions Options
        {
            get;
            set => SetProperty(ref field, value);
        } = new DPToolOptions();
    }
}