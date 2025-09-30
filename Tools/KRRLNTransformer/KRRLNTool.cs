using System.Threading.Tasks;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Data;

namespace krrTools.Tools.KRRLNTransformer
{
    /// <summary>
    /// KRRLNTransformer工具包装器
    /// </summary>
    public class KRRLNTool : ITool
    {
        public string Name => BaseOptionsManager.KRRsLNToolName;

        public IToolOptions DefaultOptions => new KRRLNTransformerOptions();

        public string? ProcessFile(string filePath)
        {
            var options = BaseOptionsManager.LoadOptions<KRRLNTransformerOptions>(BaseOptionsManager.KRRsLNToolName, BaseOptionsManager.ConfigFileName) ?? new KRRLNTransformerOptions();
            return ProcessFileWithOptions(filePath, options);
        }

        public async Task<string?> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() => ProcessFile(filePath));
        }

        public object? ProcessFileToData(string filePath)
        {
            var options = BaseOptionsManager.LoadOptions<KRRLNTransformerOptions>(BaseOptionsManager.KRRsLNToolName, BaseOptionsManager.ConfigFileName) ?? new KRRLNTransformerOptions();
            return ProcessFileToDataWithOptions(filePath, options);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap)
        {
            var options = BaseOptionsManager.LoadOptions<KRRLNTransformerOptions>(BaseOptionsManager.KRRsLNToolName, BaseOptionsManager.ConfigFileName) ?? new KRRLNTransformerOptions();
            return ProcessBeatmapToDataWithOptions(inputBeatmap, options);
        }

        public string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var transformer = new KRRLN();
            var beatmap = transformer.ProcessFiles(filePath, krrlnOptions);
            string outputPath = BeatmapOutputHelper.GenerateOutputPath(filePath, "KRRLN");
            return BeatmapOutputHelper.WriteBeatmapToFile(beatmap, outputPath) ? outputPath : null;
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToDataWithOptions(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(inputBeatmap, krrlnOptions);
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            if (options is not KRRLNTransformerOptions krrlnOptions)
                return null;

            var beatmap = FilesHelper.GetManiaBeatmap(filePath);

            var transformer = new KRRLN();
            return transformer.ProcessBeatmapToData(beatmap, krrlnOptions);
        }
    }
}