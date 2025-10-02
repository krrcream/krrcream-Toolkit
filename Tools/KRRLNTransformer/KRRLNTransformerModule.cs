using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using System;

namespace krrTools.Tools.KRRLNTransformer
{
    /// <summary>
    /// KRRLN转换模块
    /// </summary>
    public class KRRLNTransformerModule : ToolModuleBase<KRRLNTransformerOptions, KRRLNTransformerViewModel, KRRLNTransformerView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.KRRLN;

        public override string DisplayName => Strings.TabKRRsLN;

        protected override Beatmap ProcessBeatmap(Beatmap input, KRRLNTransformerOptions options)
        {
            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(input, options);
        }

        protected override Beatmap ProcessSingleFile(string filePath, KRRLNTransformerOptions options)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) throw new Exception("Failed to load beatmap");
            return ProcessBeatmap(beatmap, options);
        }
    }
}