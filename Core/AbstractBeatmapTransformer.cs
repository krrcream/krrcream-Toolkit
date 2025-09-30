using System;
using System.Collections.Generic;
using System.IO;
using krrTools.Beatmaps;
using krrTools.Data;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Core
{
    public abstract class AbstractBeatmapTransformer<TOptions> where TOptions : class
    {
        protected static readonly ILogger _logger = LoggerFactoryHolder.CreateLogger<AbstractBeatmapTransformer<TOptions>>();

        public Beatmap ProcessFile(string filePath, TOptions options)
        {
            _logger.LogInformation("转换器读取转换: {FilePath}", filePath);
            var beatmap = LoadBeatmap(filePath);
            ProcessBeatmap(beatmap, options);
            ModifyMetadata(beatmap, options);
            return beatmap;
        }

        public Beatmap ProcessBeatmapToData(Beatmap beatmap, TOptions options)
        {
            ProcessBeatmap(beatmap, options);
            ModifyMetadata(beatmap, options);
            return beatmap;
        }

        protected virtual ManiaBeatmap LoadBeatmap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件未找到: {filePath}");
            }

            if (Path.GetExtension(filePath).ToLower() != ".osu")
            {
                throw new ArgumentException("文件扩展名必须为.osu");
            }

            var beatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
            beatmap.InputFilePath = filePath;
            return beatmap;
        }

        protected void ProcessBeatmap(Beatmap beatmap, TOptions options)
        {
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options);
        }

        protected abstract int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, TOptions options);

        protected abstract void ApplyChangesToHitObjects(Beatmap beatmap, int[,] processedMatrix, TOptions options);

        protected abstract void ModifyMetadata(Beatmap beatmap, TOptions options);

        protected abstract string SaveBeatmap(Beatmap beatmap, string originalPath);
    }
}