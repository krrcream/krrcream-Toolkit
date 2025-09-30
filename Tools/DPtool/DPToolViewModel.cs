using krrTools.Configuration;

namespace krrTools.Tools.DPtool
{
    public class DPToolViewModel(DPToolOptions options) : ToolViewModelBase<DPToolOptions>(ConverterEnum.DP, false, options), IPreviewOptionsProvider
    {
        public IToolOptions GetPreviewOptions() => Options;
    }
}