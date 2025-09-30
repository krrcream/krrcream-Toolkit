using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC转换模块
    /// </summary>
    public class N2NCModule : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCControl>
    {
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        public override string DisplayName => Strings.TabN2NC;

        protected override Beatmap ProcessBeatmap(Beatmap input, N2NCOptions options)
        {
            var converter = new N2NC { options = options };
            var resultBeatmap = converter.NToNCToData(input);
            return resultBeatmap;
        }
    }
}