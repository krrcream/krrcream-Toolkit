using System.Threading.Tasks;
using krrTools.tools.DPtool;
using OsuParsers.Beatmaps;
using krrTools.Tools.Shared;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// DP工具包装器
    /// </summary>
    public class DPTool : ITool
    {
        public string Name => OptionsManager.DPToolName;

        public IToolOptions DefaultOptions => new DPToolOptions();

        public string? ProcessFile(string filePath)
        {
            var options = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.ConfigFileName) ?? new DPToolOptions();
            return ProcessFileWithOptions(filePath, options);
        }

        public async Task<string?> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() => ProcessFile(filePath));
        }

        public object? ProcessFileToData(string filePath)
        {
            var options = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.ConfigFileName) ?? new DPToolOptions();
            return ProcessFileToDataWithOptions(filePath, options);
        }

        public Beatmap? ProcessBeatmapToData(Beatmap inputBeatmap)
        {
            var options = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.ConfigFileName) ?? new DPToolOptions();
            return ProcessBeatmapToDataWithOptions(inputBeatmap, options);
        }

        public string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            if (options is not DPToolOptions dpOptions)
                return null;

            var dp = new DP();
            return dp.ProcessFile(filePath, dpOptions);
        }

        public Beatmap? ProcessBeatmapToDataWithOptions(Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not DPToolOptions dpOptions)
                return null;

            var dp = new DP();
            return dp.DPBeatmapToData(inputBeatmap, dpOptions);
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            // DP tool primarily works with beatmaps, so return the processed beatmap
            if (options is not DPToolOptions dpOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);
            var dp = new DP();
            return dp.DPBeatmapToData(beatmap, dpOptions);
        }
    }
}