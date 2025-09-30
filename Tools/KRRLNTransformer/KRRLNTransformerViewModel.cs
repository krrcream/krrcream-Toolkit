using krrTools.Configuration;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel(KRRLNTransformerOptions options)
        : ToolViewModelBase<KRRLNTransformerOptions>(ConverterEnum.KRRLN, true, options), IPreviewOptionsProvider
    {
        public IToolOptions GetPreviewOptions() => Options;
    }
}