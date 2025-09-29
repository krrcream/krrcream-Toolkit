using krrTools.Configuration;

namespace krrTools.Tools.DPtool
{
    public class DPToolViewModel() : ToolViewModelBase<DPToolOptions>(BaseOptionsManager.DPToolName, autoSave: false)
    {
        private bool _isProcessing;

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
    }
}