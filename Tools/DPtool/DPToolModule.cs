using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP转换模块
    /// </summary>
    public class DPToolModule : ToolModuleBase<DPToolOptions, DPToolViewModel, DPToolView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.DP;

        public override string DisplayName => Strings.TabDPTool;

        /// <summary>
        /// 应用转换到谱面（内部实现）
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        protected override void ApplyToBeatmapInternal(Beatmap beatmap)
        {
            var transformer = new DP();
            transformer.TransformBeatmap(beatmap, _currentOptions);
        }
    }
}