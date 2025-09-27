using System.Threading.Tasks;
using krrTools.tools.DPtool;
using OsuParsers.Beatmaps;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// DP工具包装器
    /// </summary>
    public class DPTool : ITool
    {
        public string Name => OptionsManager.DPToolName;

        public IToolOptions DefaultOptions => new DPToolOptions();

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not DPToolOptions dpOptions)
                return null;

            var dp = new DP();
            return dp.ProcessFile(filePath, dpOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }

        public object? ProcessFileToData(string filePath, IToolOptions options)
        {
            throw new System.NotImplementedException();
        }

        public Beatmap? ProcessBeatmapToData(Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not DPToolOptions dpOptions)
                return null;

            var dp = new DP();
            return dp.DPBeatmapToData(inputBeatmap, dpOptions);
        }
    }
}