using System.Threading.Tasks;
using krrTools.tools.N2NC;
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

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            return N2NCService.ProcessSingleFile(filePath, n2ncOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }

        public object? ProcessFileToData(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            var converter = new krrTools.tools.N2NC.N2NC { options = n2ncOptions };
            return converter.NToNCToData(beatmap);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            var converter = new krrTools.tools.N2NC.N2NC { options = n2ncOptions };
            return converter.NToNCToData(inputBeatmap);
        }
    }
}