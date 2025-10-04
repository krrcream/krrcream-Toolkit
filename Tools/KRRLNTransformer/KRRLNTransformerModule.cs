using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using System;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerModule : ToolModuleBase<KRRLNTransformerOptions, KRRLNTransformerViewModel, KRRLNTransformerView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.KRRLN;

        public override string DisplayName => Strings.TabKRRsLN;

        protected override Beatmap ProcessBeatmap(Beatmap input, KRRLNTransformerOptions options)
        {
            var transformer = new KRRLN();
            var resultBeatmap = transformer.ProcessBeatmapToData(input, options);
            return resultBeatmap;
        }

        protected override Beatmap ProcessSingleFile(string filePath, KRRLNTransformerOptions options)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) throw new Exception("[KRRLN]Failed to load beatmap");
            if (beatmap.GeneralSection.ModeId != 3) throw new Exception("Not mania");
            return ProcessBeatmap(beatmap, options);
        }
    }
}