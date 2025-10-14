using System;
using System.Collections.Generic;
using System.IO;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Utilities
{
    /// <summary>
    /// 谱面转换服务 - 封装转换逻辑，供预览和文件转换复用
    /// </summary>
    public class BeatmapTransformationService
    {
        private readonly IModuleManager _moduleManager;

        public BeatmapTransformationService(IModuleManager moduleManager)
        {
            _moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
        }

        /// <summary>
        /// 应用转换模块到谱面
        /// </summary>
        /// <param name="input">输入谱面</param>
        /// <param name="converter">转换模块枚举</param>
        /// <returns>转换后谱面，失败返回null暴露异常</returns>
        public Beatmap? TransformBeatmap(Beatmap input, ConverterEnum converter)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            try
            {
                // 克隆谱面以避免修改原对象
                var clonedBeatmap = CloneBeatmap(input);
                var maniaBeatmap = clonedBeatmap as IBeatmap ?? ManiaBeatmap.FromBeatmap(clonedBeatmap);

                // 获取工具并应用转换
                var tool = _moduleManager.GetToolByName(converter.ToString());
                if (tool is IApplyToBeatmap applier)
                {
                    applier.ApplyToBeatmap(maniaBeatmap);
                    return maniaBeatmap as Beatmap ?? clonedBeatmap;
                }
                else
                {
                    Console.WriteLine($"[BeatmapTransformationService] Tool {converter} does not support Beatmap conversion");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BeatmapTransformationService] Transformation failed for {converter}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载、转换并保存谱面
        /// </summary>
        /// <param name="inputPath">输入文件路径</param>
        /// <param name="converter">转换模块枚举</param>
        /// <returns>输出文件路径，失败返回null</returns>
        public string? TransformAndSaveBeatmap(string inputPath, ConverterEnum converter)
        {
            try
            {
                // 解码谱面
                var beatmap = BeatmapDecoder.Decode(inputPath).GetManiaBeatmap();
                if (beatmap == null)
                {
                    return null;
                }

                // 转换谱面
                var transformedBeatmap = TransformBeatmap(beatmap, converter);
                if (transformedBeatmap == null)
                {
                    return null;
                }

                // 保存转换后谱面
                var outputPath = transformedBeatmap.GetOutputOsuFileName();
                var outputDir = Path.GetDirectoryName(inputPath);
                var fullOutputPath = Path.Combine(outputDir!, outputPath);
                transformedBeatmap.Save(fullOutputPath);
                return fullOutputPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 转换文件失败: {inputPath}\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// 克隆Beatmap以避免修改原始对象
        /// </summary>
        private Beatmap CloneBeatmap(Beatmap input)
        {
            // 手动克隆以避免修改原始beatmap
            var cloned = new Beatmap
            {
                // 复制所有属性
                GeneralSection = input.GeneralSection,
                // 克隆MetadataSection以避免修改Version
                MetadataSection = Activator.CreateInstance(input.MetadataSection.GetType()) as dynamic
            };

            if (cloned.MetadataSection != null)
            {
                cloned.MetadataSection.Title = input.MetadataSection.Title;
                cloned.MetadataSection.TitleUnicode = input.MetadataSection.TitleUnicode;
                cloned.MetadataSection.Artist = input.MetadataSection.Artist;
                cloned.MetadataSection.ArtistUnicode = input.MetadataSection.ArtistUnicode;
                cloned.MetadataSection.Creator = input.MetadataSection.Creator;
                cloned.MetadataSection.Version = input.MetadataSection.Version;
                cloned.MetadataSection.Source = input.MetadataSection.Source;
                cloned.MetadataSection.Tags = input.MetadataSection.Tags;
            }
            // 克隆DifficultySection以避免修改CircleSize
            cloned.DifficultySection = Activator.CreateInstance(input.DifficultySection.GetType()) as dynamic;
            if (cloned.DifficultySection != null)
            {
                cloned.DifficultySection.HPDrainRate = input.DifficultySection.HPDrainRate;
                cloned.DifficultySection.CircleSize = input.DifficultySection.CircleSize;
                cloned.DifficultySection.OverallDifficulty = input.DifficultySection.OverallDifficulty;
                cloned.DifficultySection.ApproachRate = input.DifficultySection.ApproachRate;
                cloned.DifficultySection.SliderMultiplier = input.DifficultySection.SliderMultiplier;
                cloned.DifficultySection.SliderTickRate = input.DifficultySection.SliderTickRate;
            }

            cloned.TimingPoints = new List<OsuParsers.Beatmaps.Objects.TimingPoint>(input.TimingPoints);
            cloned.HitObjects = new List<OsuParsers.Beatmaps.Objects.HitObject>(input.HitObjects);
            cloned.OriginalFilePath = input.OriginalFilePath;

            return cloned;
        }
    }
}