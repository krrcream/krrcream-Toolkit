using System;
using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP转换模块
    /// </summary>
    public class DPToolModule : ToolModuleBase<DPToolOptions, DPToolViewModel, DPToolView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.DP;

        public override string DisplayName => Strings.TabDPTool;

        protected override Beatmap ProcessBeatmap(Beatmap input, DPToolOptions options)
        {
            var dp = new DP();
            var resultBeatmap = dp.ProcessBeatmapToData(input, options);
            return resultBeatmap;
        }

        protected override Beatmap ProcessSingleFile(string filePath, DPToolOptions options)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) throw new Exception("[DPTool]Failed to load beatmap");
            if (beatmap.GeneralSection.ModeId != 3) throw new Exception("Not mania");
            return ProcessBeatmap(beatmap, options);
        }
    }
}