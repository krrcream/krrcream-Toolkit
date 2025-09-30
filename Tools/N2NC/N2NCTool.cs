using System;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Data;
using krrTools.Localization;
using krrTools.Tools.Listener;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC工具模块
    /// </summary>
    public class N2NCTool : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCControl>
    {
        /// <summary>
        /// 模块类型
        /// </summary>
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        /// <summary>
        /// 模块显示名称
        /// </summary>
        public override string DisplayName => Strings.TabN2NC;

        /// <summary>
        /// 核心算法：处理Beatmap
        /// </summary>
        protected override Beatmap ProcessBeatmap(Beatmap input, N2NCOptions options)
        {
            var converter = new N2NC { options = options };
            return converter.NToNCToData(input);
        }

        public IToolOptions DefaultOptions => new N2NCOptions();

        public string? ProcessFile(string filePath, IToolOptions? options = null)
        {
            var opts = options ?? BaseOptionsManager.LoadOptions<N2NCOptions>(ConverterEnum.N2NC) ?? new N2NCOptions();
            if (opts is not N2NCOptions n2ncOptions)
                return null;

            // 先读取Beatmap
            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            // 处理Beatmap
            var processedBeatmap = N2NCService.ProcessBeatmap(beatmap, n2ncOptions);

            // 生成输出路径
            var outputPath = BeatmapOutputHelper.GenerateOutputPath(filePath, "N2NC");

            // 写入文件
            if (BeatmapOutputHelper.WriteBeatmapToFile(processedBeatmap, outputPath))
            {
                // 处理Listener逻辑（如果需要的话），未来应该抽象成独立的服务
                try
                {
                    if (ListenerControl.IsOpen)
                    {
                        var oszPath = OsuAnalyzer.AddNewBeatmapToSongFolder(outputPath);
                        return oszPath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"添加到歌曲文件夹失败: {ex.Message}");
                }

                return outputPath;
            }

            return null;
        }

        public Beatmap? ProcessBeatmapToData(Beatmap inputBeatmap, IToolOptions? options = null)
        {
            if (options is not N2NCOptions n2ncOptions)
                n2ncOptions = new N2NCOptions();

            var converter = new N2NC { options = n2ncOptions };
            return converter.NToNCToData(inputBeatmap);
        }

        public object? TestFileToData(string filePath)
        {
            var options = new N2NCOptions();
            return ProcessFileToDataWithOptions(filePath, options);
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            var converter = new N2NC { options = n2ncOptions };
            return converter.NToNCToData(beatmap);
        }
    }
}