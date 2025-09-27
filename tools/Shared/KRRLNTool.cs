using System.IO;
using System.Threading.Tasks;
using krrTools.tools.KRRLNTransformer;
using krrTools.Tools.Shared;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// KRRLNTransformer工具包装器
    /// </summary>
    public class KRRLNTool : ITool
    {
        public string Name => OptionsManager.KRRLNToolName;

        public IToolOptions DefaultOptions => new KRRLNTransformerOptions();

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var transformer = new KRRLN();
            var beatmap = transformer.ProcessFiles(filePath, krrlnOptions);
            string? dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            string outputPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + "_KRRLN.osu");
            File.WriteAllText(outputPath, beatmap.ToString());
            return outputPath;
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }

        public object? ProcessFileToData(string filePath, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(beatmap, krrlnOptions);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(inputBeatmap, krrlnOptions);
        }
    }
}