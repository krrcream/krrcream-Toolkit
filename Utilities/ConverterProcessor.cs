using System.Windows;
using System.Windows.Controls;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.Preview;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Utilities
{
    public class ConverterProcessor : IPreviewProcessor
    {
        public ConverterEnum? ModuleTool { get; set; } // 当前使用的转换工具名称，在主程序中获取

        public int StartMs { get; private set; } // 谱面起始时间，用于预览控件定位

        public IModuleManager? ModuleScheduler { get; set; } // 模块调度器，从主程序传入

        public Func<IToolOptions>? OptionsProvider { get; init; } // 选项提供器，从主程序传入模块设置

        private readonly BeatmapTransformationService? _transformationService;

        // 注意：转换预览必须实时生成，不可缓存。因为预览需要反映最新的选项变化和谱面状态。
        // 每次调用BuildConvertedVisual时，都应重新执行转换以确保准确性。
        // 不可添加任何形式的缓存机制，禁止深克隆！

        public ConverterProcessor(IModuleManager? moduleManager, Func<IToolOptions>? optionsProvider = null)
        {
            ModuleScheduler = moduleManager;
            OptionsProvider = optionsProvider;
            _transformationService = moduleManager != null ? new BeatmapTransformationService(moduleManager) : null;
        }

        /// <summary>
        /// 处理Beatmap转换的核心方法
        /// </summary>
        public FrameworkElement BuildOriginalVisual(Beatmap input)
        {
            if (input.HitObjects.Count > 0)
                StartMs = input.HitObjects.Min(n => n.StartTime);
            else
                return new TextBlock { Text = "note == 0" };

            return BuildManiaTimeRowsFromNotes(input);
        }

        public FrameworkElement BuildConvertedVisual(Beatmap input)
        {
            if (ModuleTool == null)
                return new TextBlock { Text = "ModuleTool == null" };

            if (ModuleScheduler == null)
                return new TextBlock { Text = "ModuleScheduler == null" };

            try
            {
                // 使用转换服务应用转换
                if (_transformationService == null)
                    throw new InvalidOperationException("ModuleScheduler == null");

                Beatmap transformedBeatmap = _transformationService.TransformBeatmap(input, ModuleTool.Value);
                if (transformedBeatmap.HitObjects.Count == 0)
                    return new TextBlock { Text = "Notes is 0" };

                return BuildManiaTimeRowsFromNotes(transformedBeatmap);
            }
            catch (InvalidOperationException ex) when (ex.Message == "Tool not found")
            {
                return new TextBlock { Text = "Tool not found" };
            }
            catch (InvalidOperationException ex) when (ex.Message == "ModuleScheduler == null")
            {
                return new TextBlock { Text = "ModuleScheduler == null" };
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ConverterProcessor] Transformation failed: {0}", ex.Message);
                return new TextBlock { Text = $"TransformBeatmap Fail: {ex.Message}" };
            }
        } // 通过方法获得转换结果，传递绘制

        private FrameworkElement BuildManiaTimeRowsFromNotes(Beatmap beatmap)
        {
            int columns = (int)beatmap.DifficultySection.CircleSize;
            if (columns == 0) return new TextBlock { Text = "BuildMania columns == 0" };

            double quarterMs = beatmap.GetBPM(true);
            var notes = new List<ManiaHitObject>();

            foreach (HitObject? hit in beatmap.HitObjects.OrderBy(h => h.StartTime))
            {
                notes.Add(new ManiaHitObject
                {
                    Index = (int)hit.Position.X,
                    StartTime = hit.StartTime,
                    EndTime = hit.EndTime,
                    IsHold = hit.StartTime != hit.EndTime
                });
            }

            if (notes.Count == 0) return new TextBlock { Text = "0 Mania notes" };

            // 使用新的分层控件 - 支持小节线和音符分离绘制
            var layeredControl = new LayeredPreviewControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // 传递谱面路径用于判断小节线是否需要重绘
            string beatmapPath = GetBeatmapPath(beatmap);
            layeredControl.UpdatePreview(notes, columns, quarterMs, beatmapPath);

            return layeredControl;
        }

        /// <summary>
        /// 获取谱面的唯一标识路径，用于判断小节线是否需要重绘
        /// </summary>
        private string GetBeatmapPath(Beatmap beatmap)
        {
            // 如果是内置样本，返回固定标识
            if (beatmap.MetadataSection?.Title == "Built-in Sample")
                return "builtin-sample";

            // 使用Title作为唯一标识（简化版本，避免复杂判断）
            string? title = beatmap.MetadataSection?.Title;
            return string.IsNullOrEmpty(title) ? "unknown" : title;
        }
    }
}
