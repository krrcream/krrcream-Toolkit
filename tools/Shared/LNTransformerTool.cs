using System.Threading.Tasks;
using krrTools.tools.LNTransformer;
using OsuParsers.Decoders;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// LNTransformer工具包装器
    /// </summary>
    public class LNTransformerTool : ITool
    {
        public string Name => OptionsManager.LNToolName;

        public IToolOptions DefaultOptions => new LNTransformerOptions();

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not LNTransformerOptions lnOptions)
                return null;

            return TransformService.ProcessSingleFile(filePath, lnOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }

        public object? ProcessFileToData(string filePath, IToolOptions options)
        {
            if (options is not LNTransformerOptions lnOptions)
                return null;

            // Process the file and get the output path
            var outputPath = TransformService.ProcessSingleFile(filePath, lnOptions);
            if (outputPath == null)
                return null;

            // Decode the processed file into Beatmap
            try
            {
                return BeatmapDecoder.Decode(outputPath);
            }
            catch
            {
                return null;
            }
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            // TODO: Implement direct Beatmap to Beatmap transformation
            return null;
        }
    }
}