using System;
using System.Collections.Generic;
using System.IO;
using krrTools.Beatmaps;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Core
{
    // 谱面转换抽象类
    public abstract class AbstractBeatmapTransformer<TOptions> where TOptions : class
    {
        public Beatmap ProcessFile(string filePath, TOptions options)
        {
            var beatmap = LoadBeatmap(filePath);
            ProcessBeatmap(beatmap, options);
            // ModifyMetadata(beatmap, options);
            return beatmap;
        }

        protected Beatmap ProcessBeatmapToData(Beatmap beatmap, TOptions options)
        {
            ProcessBeatmap(beatmap, options);
            // ModifyMetadata(beatmap, options);
            return beatmap;
        }

        protected void SaveBeatmap(Beatmap beatmap, TOptions options, string originalPath)
        {
            ModifyMetadata(beatmap, options);
        }
        
        protected virtual ManiaBeatmap LoadBeatmap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.WriteLine(LogLevel.Error, "读取文件失败: 文件未找到 - {FilePath}", filePath);
                throw new FileNotFoundException($"文件未找到: {filePath}");
            }

            if (Path.GetExtension(filePath).ToLower() != ".osu")
            {
                Logger.WriteLine(LogLevel.Error, "读取文件失败: 文件扩展名不是.osu - {FilePath}", filePath);
                throw new ArgumentException("文件扩展名必须为.osu");
            }

            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap == null)
            {
                Logger.WriteLine(LogLevel.Error, "读取文件失败: 无法解析谱面文件 - {FilePath}", filePath);
                throw new InvalidDataException("无法解析谱面文件");
            }

            var maniaBeatmap = beatmap.GetManiaBeatmap(filePath);
            if (maniaBeatmap.HitObjects.Count == 0)
            {
                Logger.WriteLine(LogLevel.Warning, "读取文件警告: 谱面为空 - {FilePath}", filePath);
            }

            if ((int)beatmap.GeneralSection.Mode != 3) // 3是Mania模式
            {
                Logger.WriteLine(LogLevel.Warning, "读取文件警告: 非Mania谱面 - {FilePath}, 模式: {Mode}", filePath, beatmap.GeneralSection.Mode);
            }

            maniaBeatmap.FilePath = filePath;
            return maniaBeatmap;
        }

        private void ProcessBeatmap(Beatmap beatmap, TOptions options)
        {
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options);
        }

        protected abstract int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, TOptions options);

        protected abstract void ApplyChangesToHitObjects(Beatmap beatmap, int[,] processedMatrix, TOptions options);

        protected abstract void ModifyMetadata(Beatmap beatmap, TOptions options);
    }
}