using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.Preview;
using OsuParsers.Beatmaps;

namespace krrTools.Utilities
{
    public class ConverterProcessor : IPreviewProcessor
    {
        public ConverterEnum? ModuleTool { get; set; } // 当前使用的转换工具名称，在主程序中获取

        public int StartMs { get; private set; } // 谱面起始时间，用于预览控件定位

        public IModuleManager? ModuleScheduler { get; set; } // 模块调度器，从主程序传入

        public Func<IToolOptions>? OptionsProvider { get; init; } // 选项提供器，从主程序传入模块设置

        private readonly BeatmapTransformationService _transformationService;

        // 注意：转换预览必须实时生成，不可缓存。因为预览需要反映最新的选项变化和谱面状态。
        // 每次调用BuildConvertedVisual时，都应重新执行转换以确保准确性。
        // 不可添加任何形式的缓存机制，禁止深克隆！

        public ConverterProcessor(IModuleManager moduleManager, Func<IToolOptions>? optionsProvider = null)
        {
            ModuleScheduler = moduleManager;
            OptionsProvider = optionsProvider;
            _transformationService = new BeatmapTransformationService(moduleManager);
        }

        /// <summary>
        /// 处理Beatmap转换的核心方法
        /// </summary>
        public FrameworkElement BuildOriginalVisual(Beatmap input)
        {
            // 克隆beatmap以避免任何修改影响原始对象
            var clonedBeatmap = CloneBeatmap(input);
            var maniaBeatmap = clonedBeatmap;
            if (maniaBeatmap.HitObjects.Count > 0)
                StartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);
            else
                return new TextBlock { Text = "note == 0" };

            return BuildManiaTimeRowsFromNotes(maniaBeatmap);
        }

        public FrameworkElement BuildConvertedVisual(Beatmap input)
        {
            if (ModuleTool == null)
                return new TextBlock { Text = "ModuleTool == null" };

            try
            {
                // 使用转换服务应用转换
                var transformedBeatmap = _transformationService.TransformBeatmap(input, ModuleTool.Value);
                if (transformedBeatmap == null)
                    return new TextBlock { Text = "Transformation failed" };

                return BuildManiaTimeRowsFromNotes(transformedBeatmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConverterProcessor] Transformation failed: {ex.Message}");
                return new TextBlock { Text = $"转换失败: {ex.Message}" };
            }
        } // 通过方法获得转换结果，传递绘制

        private FrameworkElement BuildManiaTimeRowsFromNotes(Beatmap beatmap)
        {
            var columns = (int)beatmap.DifficultySection.CircleSize;
            if (columns == 0) return new TextBlock { Text = "BuildMania columns == 0" };

            var quarterMs = beatmap.GetBPM(true);
            var notes = new List<ManiaHitObject>();

            foreach (var hit in beatmap.HitObjects.OrderBy(h => h.StartTime))
                notes.Add(new ManiaHitObject
                {
                    Index = (int)hit.Position.X,
                    StartTime = hit.StartTime,
                    EndTime = hit.EndTime,
                    IsHold = hit.StartTime != hit.EndTime
                });
        
            if (notes.Count == 0)
            {
                return new TextBlock { Text = "转换后谱面无Mania notes" };
            }
        
            // 使用新的分层控件 - 支持小节线和音符分离绘制
            var layeredControl = new LayeredPreviewControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        
            // 传递谱面路径用于判断小节线是否需要重绘
            var beatmapPath = GetBeatmapPath(beatmap);
            layeredControl.UpdatePreview(notes, columns, quarterMs, beatmapPath);
        
            return layeredControl;
        }
    
        /// <summary>
        /// 克隆Beatmap以避免在预览时修改原始对象
        /// </summary>
        private Beatmap CloneBeatmap(Beatmap input)
        {
            // 手动克隆以避免修改原始beatmap
            var cloned = new Beatmap();
            
            // 复制所有属性
            cloned.GeneralSection = input.GeneralSection;
            // 克隆MetadataSection以避免修改Version
            cloned.MetadataSection = Activator.CreateInstance(input.MetadataSection.GetType()) as dynamic;
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
                // 其他属性如果不存在就算了
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
            
            return cloned;
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
            var title = beatmap.MetadataSection?.Title;
            return string.IsNullOrEmpty(title) ? "unknown" : title;
        }
    }
}