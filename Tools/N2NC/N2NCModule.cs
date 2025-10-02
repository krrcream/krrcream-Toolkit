using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using System;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC转换模块
    /// </summary>
    public class N2NCModule : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        public override string DisplayName => Strings.TabN2NC;

        protected override Beatmap ProcessBeatmap(Beatmap input, N2NCOptions options)
        {
            var converter = new N2NC();
            var resultBeatmap = converter.ProcessBeatmapToData(input, options);
            return resultBeatmap;
        }

        protected override Beatmap ProcessSingleFile(string filePath, N2NCOptions options)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null) throw new Exception("Failed to load beatmap");
            return ProcessBeatmap(beatmap, options);
        }
    }
}