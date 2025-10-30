using System;
using System.IO;
using System.Threading.Tasks;
using krrTools.Bindable;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// 谱面分析服务 - 负责谱面文件解析、数据分析和结果发布
    /// </summary>
    public class BeatmapAnalysisService
    {
        // 公共属性注入事件总线
        [Inject]
        private IEventBus eventBus { get; set; } = null!;

        public BeatmapAnalysisService()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            // 订阅路径变化事件，收到后进行完整分析
            eventBus.Subscribe<BeatmapChangedEvent>(OnBeatmapPathChanged);
        }

        /// <summary>
        /// 处理谱面文件
        /// </summary>
        private async Task processBeatmapAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) ||
                !File.Exists(filePath) ||
                !Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Run(async () =>
            {
                try
                {
                    // 完整分析
                    OsuAnalysisResult analysisResult = await OsuAnalyzer.AnalyzeAsync(filePath);

                    Logger.WriteLine(LogLevel.Information,
                                     "[BeatmapAnalysisService] Beatmap analyzed: {0}, Keys: {1}, SR: {2:F2}",
                                     analysisResult.Title, analysisResult.KeyCount, analysisResult.XXY_SR);

                    // 发布专门的分析结果变化事件
                    eventBus.Publish(new AnalysisResultChangedEvent
                    {
                        AnalysisResult = analysisResult
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[BeatmapAnalysisService] ProcessBeatmapAsync failed: {0}",
                                     ex.Message);
                }
            });
        }

        /// <summary>
        /// 处理谱面路径变化事件 - 进行完整分析
        /// </summary>
        private void OnBeatmapPathChanged(BeatmapChangedEvent e)
        {
            // 只处理路径变化事件
            if (e.ChangeType != BeatmapChangeType.FromMonitoring) return;
            // 异步处理新谱面
            _ = processBeatmapAsync(e.FilePath);
        }
    }
}
