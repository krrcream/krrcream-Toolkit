using krrTools.tools.Shared;

namespace krrTools.tools.DPtool
{
    public class DPToolViewModel() : ToolViewModelBase<DPToolOptions>(OptionsManager.DPToolName, autoSave: false)
    {
        private bool _isProcessing;

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
    }
}