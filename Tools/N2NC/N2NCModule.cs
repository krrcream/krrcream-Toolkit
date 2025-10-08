using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC转换模块
    /// </summary>
    public class N2NCModule : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        public override string DisplayName => Strings.TabN2NC;

        /// <summary>
        /// 应用转换到谱面（内部实现）
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        protected override void ApplyToBeatmapInternal(Beatmap beatmap)
        {
            var transformer = new N2NC();
            transformer.TransformBeatmap(beatmap, _currentOptions);
        }
    }
}