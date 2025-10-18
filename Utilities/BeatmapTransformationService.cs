using System;
using System.Collections.Generic;
using System.IO;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using Microsoft.Extensions.Logging;
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
                // 克隆谱面以避免修改原对象, ManiaBeatmap.FromBeatmap也已经是新对象了，防御式编程
                // IBeatmap 是为了兼容谱面转换和未来拓展
                // var clonedBeatmap = CloneBeatmap(input);
                var maniaBeatmap = input as IBeatmap ?? ManiaBeatmap.FromBeatmap(input);

                // 获取工具并应用转换
                var tool = _moduleManager.GetToolByName(converter.ToString());
                if (tool is IApplyToBeatmap applier)
                {
                    applier.ApplyToBeatmap(maniaBeatmap);
                    return maniaBeatmap as Beatmap; // ?? clonedBeatmap;
                }
                else
                {
                    Logger.WriteLine(LogLevel.Warning, "[BeatmapTransformationService] Tool {0} does not support Beatmap conversion", converter);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[BeatmapTransformationService] Transformation failed for {0}: {1}", converter, ex.Message);
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

                // 保存转换后谱面，如果文件存在则删除旧的再保存
                var outputPath = transformedBeatmap.GetOutputOsuFileName();
                var outputDir = Path.GetDirectoryName(inputPath);
                var fullOutputPath = Path.Combine(outputDir!, outputPath);

                // 检查输出路径是否已存在，记录冲突
                if (File.Exists(fullOutputPath))
                {
                    // Console.WriteLine($"[WARN] 输出路径冲突，已存在，将覆盖: {fullOutputPath}");
                    try
                    {
                        File.Delete(fullOutputPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, "[BeatmapTransformationService] 删除旧文件失败: {0}\n{1}", fullOutputPath, ex);
                        return null;
                    }
                }

                try
                {
                    transformedBeatmap.Save(fullOutputPath);
                    return fullOutputPath;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[BeatmapTransformationService] 保存失败: {0}\n{1}", fullOutputPath, ex);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[BeatmapTransformationService] 转换文件失败: {0}\n{1}", inputPath, ex);
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
                EventsSection = input.EventsSection,
                ColoursSection = input.ColoursSection,
                EditorSection = input.EditorSection,
                // 手动克隆MetadataSection
                // 以避免修改Version\CircleSize
                // 其他部分直接引用即可
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
                cloned.MetadataSection.BeatmapSetID = input.MetadataSection.BeatmapSetID;
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