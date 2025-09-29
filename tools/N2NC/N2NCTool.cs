using System;
using System.Threading.Tasks;
using krrTools.tools.N2NC;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using krrTools.Tools.Shared;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// N2NC工具包装器
    /// </summary>
    public class N2NCTool : ITool
    {
        public string Name => OptionsManager.N2NCToolName;

        public IToolOptions DefaultOptions => new N2NCOptions();

        public string? ProcessFile(string filePath)
        {
            var options = OptionsManager.LoadOptions<N2NCOptions>(OptionsManager.N2NCToolName, OptionsManager.ConfigFileName) ?? new N2NCOptions();
            return ProcessFileWithOptions(filePath, options);
        }

        public async Task<string?> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() => ProcessFile(filePath));
        }

        public object? ProcessFileToData(string filePath)
        {
            var options = OptionsManager.LoadOptions<N2NCOptions>(OptionsManager.N2NCToolName, OptionsManager.ConfigFileName) ?? new N2NCOptions();
            return ProcessFileToDataWithOptions(filePath, options);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap)
        {
            var options = OptionsManager.LoadOptions<N2NCOptions>(OptionsManager.N2NCToolName, OptionsManager.ConfigFileName) ?? new N2NCOptions();
            return ProcessBeatmapToDataWithOptions(inputBeatmap, options);
        }

        public string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            // 先读取Beatmap
            var beatmap = FilesHelper.GetManiaBeatmap(filePath);
            if (beatmap == null)
                return null;

            // 处理Beatmap
            var processedBeatmap = N2NCService.ProcessBeatmap(beatmap, n2ncOptions);
            if (processedBeatmap == null)
                return null;

            // 生成输出路径
            var outputPath = BeatmapOutputHelper.GenerateOutputPath(filePath, "N2NC");

            // 写入文件
            if (BeatmapOutputHelper.WriteBeatmapToFile(processedBeatmap, outputPath))
            {
                // 处理Listener逻辑（如果需要的话）
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

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToDataWithOptions(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            var converter = new krrTools.tools.N2NC.N2NC { options = n2ncOptions };
            return converter.NToNCToData(inputBeatmap);
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            var converter = new krrTools.tools.N2NC.N2NC { options = n2ncOptions };
            return converter.NToNCToData(beatmap);
        }

        // 保留旧方法名以向后兼容，但标记为过时
        [Obsolete("Use ProcessFileWithOptions instead")]
        public object? ProcessFileToData(string filePath, IToolOptions options)
        {
            return ProcessFileToDataWithOptions(filePath, options);
        }

        [Obsolete("Use ProcessBeatmapToDataWithOptions instead")]
        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            return ProcessBeatmapToDataWithOptions(inputBeatmap, options);
        }
    }
}