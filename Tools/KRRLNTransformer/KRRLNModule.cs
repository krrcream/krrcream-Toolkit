using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.KRRLNTransformer
{
    /// <summary>
    /// KRRLN转换模块
    /// </summary>
    public class KRRLNModule : ToolModuleBase<KRRLNTransformerOptions, KRRLNTransformerViewModel, KRRLNTransformerControl>
    {
        public override ToolModuleType ModuleType => ToolModuleType.KRRLN;

        public override string DisplayName => Strings.TabKRRsLN;

        protected override Beatmap ProcessBeatmap(Beatmap input, KRRLNTransformerOptions options)
        {
            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(input, options);
        }
    }
}