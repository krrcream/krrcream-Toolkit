using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.tools.DPtool;
using krrTools.tools.N2NC;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.Preview
{
    public class BasePreviewProcessor : IPreviewProcessor
    {
        public virtual string ToolKey => "Preview";
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
            return BuildPreview(filePaths, false, null);
        }

        protected Func<string, int, int, (int columns, List<ManiaNote> notes, double quarterMs)>? ConversionProvider { get; init; }

        public virtual FrameworkElement BuildConvertedVisual(string[] filePaths)
        {
            return BuildPreview(filePaths, true, ConversionProvider);
        }
        
        private FrameworkElement BuildPreview(string[] filePaths, bool converted,
            Func<string, int, int, (int columns, List<ManiaNote> notes, double quarterMs)>? conversionProvider)
        {
            var path = filePaths is { Length: > 0 } ? filePaths[0] : string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new TextBlock { Text = "(无文件)" };

            int? first = PreviewTransformation.GetFirstNonEmptyTime(path);
            if (!first.HasValue)
            {
                var full = PreviewTransformation.BuildOriginal(path, 1);
                if (full.notes.Count > 0) first = full.notes.Min(n => n.Time);
            }
            if (!first.HasValue) return new TextBlock { Text = "(无可用音符)" };

            var quarterMs = PreviewTransformation.GetQuarterMs(path);
            int startMs = first.Value;
            int windowMs = Math.Max(PreviewConstants.MinWindowLengthMs,
                (int)Math.Round(quarterMs * PreviewConstants.PreviewWindowUnitCount / PreviewConstants.PreviewWindowUnitBeatDenominator));
            int endMs = startMs + windowMs;

            // 记录开始时间，供外部（XAML）读取并更新标题
            if (converted)
                LastConvertedStartMs = startMs;
            else
                LastOriginalStartMs = startMs;

            (int columns, List<ManiaNote> notes, double quarterMs) data;
            if (converted)
            {
                if (conversionProvider == null)
                    return new TextBlock { Text = "(无转换提供器)" };
                data = conversionProvider(path, startMs, endMs);
            }
            else
            {
                data = PreviewTransformation.BuildOriginalWindow(path, startMs, endMs);
            }

            var previewElement = BuildFromRealNotes(data);
            
            // 检查是否有背景图
            var bgImagePath = PreviewTransformation.GetBackgroundImagePath(path);
            if (!string.IsNullOrEmpty(bgImagePath) && File.Exists(bgImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"Applying background image: {bgImagePath}");
                
                // 创建背景图片画笔
                var bgImageBrush = new ImageBrush
                {
                    ImageSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(bgImagePath)),
                    Stretch = Stretch.UniformToFill,
                    Opacity = SharedUIComponents.PreviewBackgroundOpacity
                };
                
                // 应用模糊效果
                var blurEffect = new System.Windows.Media.Effects.BlurEffect
                {
                    Radius = SharedUIComponents.PreviewBackgroundBlurRadius,
                    KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                };
                
                // 创建一个包含背景和内容的Border
                var bgBorder = new Border
                {
                    Background = bgImageBrush,
                    Effect = blurEffect,
                    Child = previewElement,
                    CornerRadius = new CornerRadius(4)
                };
                
                System.Diagnostics.Debug.WriteLine("Background image applied successfully");
                return bgBorder;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No background image found for {path}");
                // 创建一个显示背景图状态的Border
                var statusBorder = new Border
                {
                    Background = SharedUIComponents.PanelBackgroundBrush,
                    Child = new TextBlock 
                    { 
                        Text = $"预览内容\n(无背景图)\n路径: {Path.GetFileName(path)}", 
                        Foreground = SharedUIComponents.UiTextBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    },
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10)
                };
                
                var grid = new Grid();
                grid.Children.Add(statusBorder);
                grid.Children.Add(previewElement);
                
                return grid;
            }
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
        public ConverterPreviewProcessor()
        {
            ConversionProvider = (path, start, end) =>
            {
                var opt = ConverterOptionsProvider?.Invoke();
                if (opt == null) return (0, new List<ManiaNote>(), 0.0);
                return PreviewTransformation.BuildConverterWindow(path, opt, start, end);
            };
        }

        public ConverterPreviewProcessor(int? columnOverride, Func<N2NCOptions?>? converterOptionsProvider) : this()
        {
            ColumnOverride = columnOverride;
            ConverterOptionsProvider = converterOptionsProvider;
        }
    }

    internal sealed class LNPreviewProcessor : BasePreviewProcessor
    {
        public override string ToolKey => "LN Transformer";
        public Func<PreviewTransformation.LNPreviewParameters>? LNParamsProvider { get; set; }
        public LNPreviewProcessor()
        {
            ConversionProvider = (path, start, end) =>
            {
                if (LNParamsProvider == null)
                    return (0, new List<ManiaNote>(), 0.0);
                var p = LNParamsProvider();
                return PreviewTransformation.BuildLNWindow(path, p, start, end);
            };
        }

        public LNPreviewProcessor(int? columnOverride, Func<PreviewTransformation.LNPreviewParameters>? lnParamsProvider) : this()
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
            ConversionProvider = (path, start, end) =>
            {
                var opt = DPOptionsProvider?.Invoke();
                if (opt == null) return (0, new List<ManiaNote>(), 0.0);
                return PreviewTransformation.BuildDPWindow(path, opt, start, end);
            };
        }

        public DPPreviewProcessor(int? columnOverride, Func<DPToolOptions>? dpOptionsProvider) : this()
        {
            ColumnOverride = columnOverride;
            DPOptionsProvider = dpOptionsProvider;
        }
    }
}
