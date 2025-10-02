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
            return dp.DPBeatmapToData(input, options);
        }

        protected override Beatmap ProcessSingleFile(string filePath, DPToolOptions options)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) throw new Exception("Failed to load beatmap");
            return ProcessBeatmap(beatmap, options);
        }
    }
}