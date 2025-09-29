using System.Threading.Tasks;
using krrTools.tools.LNTransformer;
using OsuParsers.Decoders;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// LNTransformer工具包装器
    /// </summary>
    public class YLsLNTransformerTool : ITool
    {
        public string Name => OptionsManager.YLsLNToolName;

        public IToolOptions DefaultOptions => new YLsLNTransformerOptions();

        public string? ProcessFile(string filePath)
        {
            var options = OptionsManager.LoadOptions<YLsLNTransformerOptions>(OptionsManager.YLsLNToolName, OptionsManager.ConfigFileName) ?? new YLsLNTransformerOptions();
            return ProcessFileWithOptions(filePath, options);
        }

        public async Task<string?> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() => ProcessFile(filePath));
        }

        public object? ProcessFileToData(string filePath)
        {
            var options = OptionsManager.LoadOptions<YLsLNTransformerOptions>(OptionsManager.YLsLNToolName, OptionsManager.ConfigFileName) ?? new YLsLNTransformerOptions();
            return ProcessFileToDataWithOptions(filePath, options);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap)
        {
            var options = OptionsManager.LoadOptions<YLsLNTransformerOptions>(OptionsManager.YLsLNToolName, OptionsManager.ConfigFileName) ?? new YLsLNTransformerOptions();
            return ProcessBeatmapToDataWithOptions(inputBeatmap, options);
        }

        public string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            if (options is not YLsLNTransformerOptions lnOptions)
                return null;

            return TransformService.ProcessSingleFile(filePath, lnOptions);
        }

        public OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToDataWithOptions(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options)
        {
            // TODO: Implement direct Beatmap to Beatmap transformation
            // For now, save to temp file, process, then decode back
            if (options is not YLsLNTransformerOptions lnOptions)
                return null;

            try
            {
                // This is a temporary implementation until direct transformation is available
                var tempInputPath = System.IO.Path.GetTempFileName() + ".osu";
                var tempOutputPath = System.IO.Path.GetTempFileName() + ".osu";

                // Write input beatmap to temp file
                BeatmapOutputHelper.WriteBeatmapToFile(inputBeatmap, tempInputPath);

                // Process the temp file
                var resultPath = TransformService.ProcessSingleFile(tempInputPath, lnOptions);
                if (resultPath == null)
                    return null;

                // Decode the result
                var resultBeatmap = BeatmapDecoder.Decode(resultPath);

                // Clean up temp files
                try
                {
                    System.IO.File.Delete(tempInputPath);
                    System.IO.File.Delete(resultPath);
                }
                catch { }

                return resultBeatmap;
            }
            catch
            {
                return null;
            }
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            if (options is not YLsLNTransformerOptions lnOptions)
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
    }
}