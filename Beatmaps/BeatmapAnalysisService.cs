using System;
using System.Threading.Tasks;
using krrTools.Bindable;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// 谱面分析服务 - 负责谱面文件解析、数据分析和结果发布
    /// </summary>
    public class BeatmapAnalysisService
    {
        private readonly BeatmapCacheManager _cacheManager = new();

        // 公共属性注入事件总线
        [Inject]
        private IEventBus EventBus { get; set; } = null!;

        public BeatmapAnalysisService()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();
            
            // 订阅路径变化事件，收到后进行完整分析
            EventBus.Subscribe<BeatmapChangedEvent>(OnBeatmapPathChanged);
        }

        /// <summary>
        /// 处理谱面文件
        /// </summary>
        public async Task ProcessBeatmapAsync(string fullPath)
        {
            // 快速检查是否为有效的Mania谱面文件 (包含文件有效性 + Mode检查)
            if (!BeatmapAnalyzer.IsManiaBeatmap(fullPath)) return;

            await Task.Run(() =>
            {
                try
                {
                    // 响应式防重复处理检查
                    if (!_cacheManager.CanProcessFile(fullPath)) return;

                    // 使用BeatmapAnalyzer进行完整分析
                    var analysisResult = BeatmapAnalyzer.Analyze(fullPath);
                    if (analysisResult == null) return;

                    Logger.WriteLine(LogLevel.Debug,
                        "[BeatmapAnalysisService] Beatmap analyzed: {0}, Keys: {1}, SR: {2:F2}",
                        analysisResult.Title ?? "Unknown", analysisResult.Keys, analysisResult.XXY_SR);


                    // 发布专门的分析结果变化事件
                    EventBus.Publish(new AnalysisResultChangedEvent
                    {
                        AnalysisResult = analysisResult,
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
            
            BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = e.FilePath);
            
            // 异步处理新谱面
            _ = ProcessBeatmapAsync(e.FilePath);
        }

        /// <summary>
        /// 快速检查是否为有效的Mania谱面文件 (兼容性方法)
        /// </summary>
        public bool IsManiaBeatmapQuickCheck(string? filePath)
        {
            return BeatmapAnalyzer.IsManiaBeatmap(filePath);
        }
    }
}