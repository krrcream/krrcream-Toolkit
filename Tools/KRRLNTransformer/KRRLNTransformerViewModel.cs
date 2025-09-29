using krrTools.Configuration;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel()
        : ToolViewModelBase<KRRLNTransformerOptions>(BaseOptionsManager.KRRsLNToolName, autoSave: true);
}