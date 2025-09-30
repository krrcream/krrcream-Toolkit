using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP转换模块
    /// </summary>
    public class DPModule : ToolModuleBase<DPToolOptions, DPToolViewModel, DPToolControl>
    {
        public override ToolModuleType ModuleType => ToolModuleType.DP;

        public override string DisplayName => Strings.TabDPTool;

        protected override Beatmap ProcessBeatmap(Beatmap input, DPToolOptions options)
        {
            var dp = new DP();
            return dp.DPBeatmapToData(input, options);
        }
    }
}