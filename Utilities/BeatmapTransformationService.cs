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
    public class BeatmapTransformationService(IModuleManager moduleManager)
    {
        private readonly IModuleManager _moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));

        /// <summary>
        /// 应用转换模块到谱面
        /// </summary>
        /// <param name="input">输入谱面, 外部应预处理异常，保证传入的是可以正常转换的mania谱面</param>
        /// <param name="converter">转换模块枚举</param>
        /// <returns>转换后谱面，失败返回原始谱面</returns>
        public Beatmap TransformBeatmap(Beatmap input, ConverterEnum converter)
        {
            try
            {
                var maniaBeatmap = input;

                // 获取工具并应用转换
                var tool = _moduleManager.GetToolByName(converter.ToString());
                if (tool is IApplyToBeatmap applier)
                {
                    applier.ApplyToBeatmap(maniaBeatmap);
                    return maniaBeatmap;
                }
                
                throw new InvalidOperationException("Tool not found");
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[BeatmapTransformationService] Transformation failed for {0}: {1}", converter, ex.Message);
                throw;
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
        
        // 如果需要克隆Beatmap方法，最好由库实现，而不是手动克隆
    }
}