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
        /// 应用转换到谱面（内部实现）- 获取最新的运行时设置
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        protected override void ApplyToBeatmapInternal(Beatmap beatmap)
        {
            // 获取最新的选项设置 - 响应式系统实时更新
            var options = GetLatestOptions();
            
            var transformer = new DP();
            transformer.TransformBeatmap(beatmap, options);
        }
    }
}