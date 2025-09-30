using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP工具模块
    /// </summary>
    public class DPTool : ToolModuleBase<DPToolOptions, DPToolViewModel, DPToolControl>
    {
        /// <summary>
        /// 模块类型
        /// </summary>
        public override ToolModuleType ModuleType => ToolModuleType.DP;

        /// <summary>
        /// 模块显示名称
        /// </summary>
        public override string DisplayName => Strings.TabDPTool;

        /// <summary>
        /// 核心算法：处理Beatmap
        /// </summary>
        protected override Beatmap ProcessBeatmap(Beatmap input, DPToolOptions options)
        {
            var dp = new DP();
            return dp.DPBeatmapToData(input, options) ?? input;
        }
    }
}
