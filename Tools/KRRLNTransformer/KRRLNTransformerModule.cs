using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerModule : ToolModuleBase<KRRLNTransformerOptions, KRRLNTransformerViewModel, KRRLNTransformerView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.KRRLN;

        public override string DisplayName => Strings.TabKRRsLN;

        /// <summary>
        /// 应用转换到谱面（内部实现）
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        protected override void ApplyToBeatmapInternal(Beatmap beatmap)
        {
            var transformer = new KRRLN();
            transformer.TransformBeatmap(beatmap, _currentOptions);
        }
    }
}