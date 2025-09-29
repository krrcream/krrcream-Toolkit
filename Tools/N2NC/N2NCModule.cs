using krrTools.Core.Modules;
using OsuParsers.Beatmaps;
using krrTools.Configuration;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC转换模块
    /// </summary>
    public class N2NCModule : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCControl>
    {
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        public override string ModuleName => BaseOptionsManager.N2NCToolName;

        public override string DisplayName => "N2NC Converter";

        public override Beatmap ProcessBeatmap(Beatmap input, N2NCOptions options)
        {
            return N2NCService.ProcessBeatmap(input, options);
        }
    }
}