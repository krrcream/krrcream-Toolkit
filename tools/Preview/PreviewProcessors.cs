using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.tools.DPtool;
using krrTools.tools.KRRLNTransformer;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;
using OsuParsers.Beatmaps;

namespace krrTools.tools.Preview
{
    public class BasePreviewProcessor : IPreviewProcessor
    {
        public virtual string ToolKey => "Preview";
        public string? CurrentTool { get; set; }
        public int? ColumnOverride { get; set; }

        public int? LastOriginalStartMs { get; private set; }
        public int? LastConvertedStartMs { get; private set; }

        public class ManiaNote
        {
            public int X;
            public int Time;
            public bool IsHold;
            public int? EndTime;
        }

        public virtual FrameworkElement BuildOriginalVisual(string[] filePaths)
        {
            return BuildPreview(filePaths, false, null, null);
        }

        protected Func<string, string, int, int, object?>? ConversionProvider { get; init; }

        public virtual FrameworkElement BuildConvertedVisual(string[] filePaths)
        {
            return BuildPreview(filePaths, true, ConversionProvider, CurrentTool);
        }
        
        private FrameworkElement BuildPreview(string[] filePaths, bool converted,
            Func<string, string, int, int, object?>? conversionProvider, string? toolName)
        {
            var path = filePaths is { Length: > 0 } ? filePaths[0] : string.Empty;
            Debug.WriteLine($"Building preview for path: {path}, converted: {converted}");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new TextBlock { Text = "(无文件)" };

            int? first = PreviewTransformation.GetFirstNonEmptyTime(path);
            if (!first.HasValue)
            {
                var full = PreviewTransformation.BuildOriginal(path, 1);
                if (full.notes.Count > 0) first = full.notes.Min(n => n.Time);
            }
            if (!first.HasValue) return new TextBlock { Text = "(无可用音符)" };

            Beatmap beatmapMeta = FilesHelper.GetManiaBeatmap(path);
            
            var quarterMs = beatmapMeta.GetBPM(true);
            int startMs = first.Value;
            int windowMs = Math.Max(PreviewConstants.MinWindowLengthMs,
                (int)Math.Round(quarterMs * PreviewConstants.PreviewWindowUnitCount / PreviewConstants.PreviewWindowUnitBeatDenominator));
            int endMs = startMs + windowMs;

            if (converted)
                LastConvertedStartMs = startMs;
            else
                LastOriginalStartMs = startMs;

            (int columns, List<ManiaNote> notes, double quarterMs) data;
            if (converted)
            {
                if (conversionProvider == null)
                    return new TextBlock { Text = "(无转换提供器)" };
                var rawData = conversionProvider(toolName ?? "", path, startMs, endMs);
                if (rawData is Beatmap beatmap)
                {
                    data = PreviewTransformation.BuildFromBeatmapWindow(beatmap, startMs, endMs);
                }
                else
                {
                    data = (0, new List<ManiaNote>(), 0.0);
                }
            }
            else
            {
                data = PreviewTransformation.BuildOriginalWindow(path, startMs, endMs);
            }

            var previewElement = BuildFromRealNotes(data);
            return previewElement;
        }

        // 从实际音符构建显示
        private FrameworkElement BuildFromRealNotes((int columns, List<ManiaNote> notes, double quarterMs) data)
        {
            if (data.columns <= 0 || data.notes.Count == 0)
                return new TextBlock { Text = "(无可用数据)" };

            var displayColumns = data.columns;
            if (ColumnOverride is > 0)
                displayColumns = ColumnOverride.Value;

            return BuildManiaTimeRowsFromNotes(data.notes, displayColumns, 10, data.quarterMs);
        }

        // 根据时间行构建动态预览控件（按时间分组、限制行数）
        private FrameworkElement BuildManiaTimeRowsFromNotes(List<ManiaNote> allNotes, int columns, int maxRows, double quarterMs = 0, Func<int, ManiaNote, ManiaNote>? noteTransform = null)
        {
            if (allNotes.Count == 0) return new TextBlock { Text = "(无数据)" };
            var timeGroups = allNotes.GroupBy(n => n.Time).OrderBy(g => g.Key).Take(maxRows).ToList();
            if (timeGroups.Count == 0) return new TextBlock { Text = "(无数据)" };

            List<(int time, List<ManiaNote> notes)> grouped;
            if (noteTransform != null)
            {
                grouped = new List<(int time, List<ManiaNote> notes)>(timeGroups.Count);
                foreach (var g in timeGroups)
                {
                    var list = new List<ManiaNote>(g.Count());
                    foreach (var n in g) list.Add(noteTransform(columns, n));
                    grouped.Add((g.Key, list));
                }
            }
            else
            {
                grouped = timeGroups.Select(g => (g.Key, g.ToList())).ToList();
            }

            // 使用动态控件显示；控件自适应父容器大小
            int displayColumns = columns; if (displayColumns <= 0) displayColumns = 1;
            var dyn = new DynamicPreviewControl(grouped, displayColumns, quarterMs)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            return dyn;
        }
    }

    internal sealed class ConverterPreviewProcessor : BasePreviewProcessor
    {
        public override string ToolKey => "Converter";
        public Func<N2NCOptions?>? ConverterOptionsProvider { get; set; }
        public Func<DPToolOptions?>? DPOptionsProvider { get; set; }
        public Func<LNTransformerOptions?>? LNOptionsProvider { get; set; }
        public Func<KRRLNTransformerOptions?>? KRRLNOptionsProvider { get; set; }
        public ConverterPreviewProcessor()
        {
            ConversionProvider = (toolName, path, start, end) =>
            {
                _ = toolName; _ = path; _ = start; _ = end; // Suppress unused parameter warnings

                var originalBeatmap = FilesHelper.GetManiaBeatmap(path);

                if (toolName == OptionsManager.N2NCToolName)
                {
                    var tool = new N2NCTool();
                    var opt = ConverterOptionsProvider?.Invoke();
                    if (opt == null) return null;
                    return tool.ProcessBeatmapToData(originalBeatmap, opt);
                }
                
                if (toolName == OptionsManager.DPToolName)
                {
                    var tool = new DPTool();
                    var opt = DPOptionsProvider?.Invoke();
                    if (opt == null) return null;
                    return tool.ProcessBeatmapToData(originalBeatmap, opt);
                }
                
                if (toolName == OptionsManager.LNToolName)
                {
                    var tool = new LNTransformerTool();
                    var opt = LNOptionsProvider?.Invoke();
                    if (opt == null) return null;
                    return tool.ProcessBeatmapToData(originalBeatmap, opt);
                }

                if (toolName == OptionsManager.KRRLNToolName)
                {
                    try
                    {
                        var tool = new KRRLNTool();
                        var opt = KRRLNOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return tool.ProcessBeatmapToData(originalBeatmap, opt);
                    }
                    catch (Exception ex)
                    {
                        // Return null to indicate no preview available
                        return null;
                    }
                }

                return originalBeatmap;
            };
        }

        public ConverterPreviewProcessor(int? columnOverride, Func<N2NCOptions?>? converterOptionsProvider) : this()
        {
            ColumnOverride = columnOverride;
            ConverterOptionsProvider = converterOptionsProvider;
        }
    }

    // TODO： 下面的继承类预览器在未来删除，改成统一的中央预览器
    internal sealed class LNPreviewProcessor : BasePreviewProcessor
    {
        public override string ToolKey => "LN Transformer";
        public Func<LNTransformerCore.LNPreviewParameters>? LNParamsProvider { get; set; }
        public LNPreviewProcessor()
        {
#pragma warning disable CS0168 // Parameters are not used in this implementation
            ConversionProvider = (toolName, path, start, end) =>
            {
                // TODO: Implement LN transformation preview using Beatmap
                return null;
            };
#pragma warning restore CS0168
        }

        public LNPreviewProcessor(int? columnOverride, Func<LNTransformerCore.LNPreviewParameters>? lnParamsProvider) : this()
        {
            ColumnOverride = columnOverride;
            LNParamsProvider = lnParamsProvider;
        }
    }

    internal sealed class DPPreviewProcessor : BasePreviewProcessor
    {
        public override string ToolKey => "DP tool";
        public Func<DPToolOptions>? DPOptionsProvider { get; set; }
        public DPPreviewProcessor()
        {
#pragma warning disable CS0168 // Parameters are not used in this implementation
            ConversionProvider = (toolName, path, start, end) =>
            {
                // TODO: Implement DP transformation preview using Beatmap
                return null;
            };
#pragma warning restore CS0168
        }

        public DPPreviewProcessor(int? columnOverride, Func<DPToolOptions>? dpOptionsProvider) : this()
        {
            ColumnOverride = columnOverride;
            DPOptionsProvider = dpOptionsProvider;
        }
    }
}
