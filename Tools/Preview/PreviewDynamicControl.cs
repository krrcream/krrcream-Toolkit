using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using krrTools.Beatmaps;

namespace krrTools.Tools.Preview
{
    internal class PreviewDynamicControl : Grid
    {
        private readonly List<ManiaBeatmap.PreViewManiaNote> _notes;
        private readonly int _columns;
        private readonly double _quarterMs;
        private readonly double _firstTime;
        private readonly Canvas _canvas;
        private readonly ScrollViewer _scrollViewer;
        private double _lastAvailableHeight = -1;
        private double _lastAvailableWidth = -1;

        public PreviewDynamicControl(List<(int time, List<ManiaBeatmap.PreViewManiaNote> notes)> grouped, int columns, double quarterMs)
        {
            // grouped 已排序；展开为单一 notes 列表并记录第一个时间点
            _notes = grouped.SelectMany(g => g.notes).ToList();
            _firstTime = grouped.First().time;
            _columns = columns;
            _quarterMs = quarterMs;

            // 将 Canvas 放在 ScrollViewer 中，支持滚动
            _canvas = new Canvas { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
            _scrollViewer = new ScrollViewer
            {
                Content = _canvas,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(_scrollViewer);

            SizeChanged += DynamicPreviewControl_SizeChanged;
            _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(Redraw), System.Windows.Threading.DispatcherPriority.Loaded);

            Redraw();
        }

        private void DynamicPreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            double availableWidth = Math.Max(100, ActualWidth - 20); // 保留内边距

            // 时间窗口：基于固定行间距计算
            double firstTime = _firstTime;

            // 计算整个谱面的时间范围，用于确定canvas的总高度
            double lastTime = firstTime;
            if (_notes.Count > 0)
            {
                lastTime = _notes.Max(n => n.StartTime);
                var maxHoldEndTime = _notes.Where(n => n.EndTime.HasValue).Select(n => n.EndTime.GetValueOrDefault()).DefaultIfEmpty().Max();
                if (maxHoldEndTime > 0)
                {
                    lastTime = Math.Max(lastTime, maxHoldEndTime);
                }
            }
            double totalTimeRange = Math.Max(PreviewConstants.MinWindowLengthMs, lastTime - firstTime);

            // 高度：容器可用高度
            double availableHeight = Math.Max(PreviewConstants.CanvasMinHeight, Math.Max(0, ActualHeight - 20));

            // 固定行间距：每个音符8px高度
            const double fixedNoteSpacing = 10.0;

            // 时间窗口：基于固定间距和容器高度计算
            // 假设每_quarterMs对应fixedNoteSpacing的像素高度
            double timeWindow = _quarterMs > 0
                ? (availableHeight / fixedNoteSpacing) * _quarterMs
                : PreviewConstants.MinWindowLengthMs;

            // 确保时间窗口不超过总时间范围
            timeWindow = Math.Min(totalTimeRange, Math.Max(PreviewConstants.MinWindowLengthMs, timeWindow));

            // Canvas高度：基于总时间范围和固定间距计算，确保能显示所有笔记
            double totalCanvasHeight = _quarterMs > 0 ? (totalTimeRange / _quarterMs) * fixedNoteSpacing : availableHeight;
            double canvasHeight = Math.Max(availableHeight, totalCanvasHeight);

            // 如果尺寸没明显变化则跳过重绘
            if (Math.Abs(_lastAvailableHeight - canvasHeight) < 0.5 && Math.Abs(_lastAvailableWidth - availableWidth) < 0.5)
            {
                return;
            }
            _lastAvailableHeight = canvasHeight;
            _lastAvailableWidth = availableWidth;

            // 计算列宽，避免除以 0；列数为 0 时用于计算的列数视作 1，但绘制时仍以真实列数为准
            int calcCols = Math.Max(1, _columns);
            double maxPossibleContentWidth = Math.Min(PreviewConstants.MaxContentWidth, availableWidth);
            double lanesAreaAvailable = Math.Max(10, maxPossibleContentWidth - PreviewConstants.CanvasLeftPadding);
            double laneWidth = lanesAreaAvailable / calcCols;
            laneWidth = Math.Min(PreviewConstants.LaneMaxWidth, Math.Max(PreviewConstants.LaneMinWidth, laneWidth));

            double lanesAreaWidth = laneWidth * _columns;
            double canvasWidth = PreviewConstants.CanvasLeftPadding + lanesAreaWidth;

            // 设置 canvas 并清空
            _canvas.Width = canvasWidth;
            _canvas.Height = canvasHeight;
            _canvas.Children.Clear();

            double mapBase = PreviewConstants.MapBase;
            double mapSpan = Math.Max(0, _canvas.Height - 20);

            DrawBackgroundLanes(laneWidth);

            // 计算可见区域：始终只绘制当前滚动位置的可见内容
            double verticalOffset = _scrollViewer.VerticalOffset;
            double viewportHeight = _scrollViewer.ViewportHeight;
            double visibleStartY = verticalOffset;
            double visibleEndY = verticalOffset + viewportHeight;

            double visibleStartRel = Math.Max(0, ((mapBase + mapSpan - visibleEndY) / mapSpan) * totalTimeRange);
            double visibleEndRel = Math.Min(totalTimeRange, ((mapBase + mapSpan - visibleStartY) / mapSpan) * totalTimeRange);

            double visibleStartTime = firstTime + visibleStartRel;
            double visibleEndTime = firstTime + visibleEndRel;

            List<ManiaBeatmap.PreViewManiaNote> notesToDraw = _notes.Where(n => n.StartTime <= visibleEndTime && (n.EndTime ?? n.StartTime) >= visibleStartTime).ToList();

            DrawNotes(notesToDraw, laneWidth, firstTime, timeWindow, mapBase, mapSpan);
            DrawQuarterLines(firstTime, timeWindow, mapBase, mapSpan, canvasWidth);
        }

        private void DrawBackgroundLanes(double laneWidth)
        {
            for (int c = 0; c < _columns; c++)
            {
                var laneRect = new Rectangle
                {
                    Width = laneWidth,
                    Height = _canvas.Height,
                    Fill = (c % 2 == 0) ? PreviewConstants.LaneEvenBrush : PreviewConstants.LaneOddBrush
                };
                Canvas.SetLeft(laneRect, PreviewConstants.CanvasLeftPadding + c * laneWidth);
                Canvas.SetTop(laneRect, 0);
                _canvas.Children.Add(laneRect);
            }
        }

        private void DrawNotes(List<ManiaBeatmap.PreViewManiaNote> notes, double laneWidth, double firstTime, double timeWindow, double mapBase, double mapSpan)
        {
            // 本地辅助：构建带描边的矩形
            Rectangle MakeRect(double w, double h, Brush fill, double radius = 0)
            {
                var r = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = fill,
                    Stroke = PreviewConstants.OutlineBrush,
                    StrokeThickness = 0.7
                };
                if (radius > 0)
                {
                    r.RadiusX = radius;
                    r.RadiusY = radius;
                }
                return r;
            }

            // 绘制音符
            foreach (var n in notes)
            {
                // 计算 落在 第几列（X: 0..512 映射到列）
                var lane = (int)Math.Floor(n.Index / (512.0 / Math.Max(1, _columns)));
                if (lane < 0) lane = 0; else if (lane >= _columns) lane = Math.Max(0, _columns - 1);

                double relStart = Math.Max(0, Math.Min(timeWindow, n.StartTime - firstTime));
                double yStart = mapBase + mapSpan - (relStart / timeWindow) * mapSpan;

                double rectHeight = PreviewConstants.NoteFixedHeight;
                double rectWidth = Math.Max(2.0, laneWidth - 2.0 * PreviewConstants.NoteSideMargin);
                double rectLeft = PreviewConstants.CanvasLeftPadding + lane * laneWidth + PreviewConstants.NoteSideMargin;

                if (!n.IsHold)
                {
                    var tapRect = MakeRect(rectWidth, rectHeight, PreviewConstants.TapNoteBrush);
                    tapRect.ToolTip = $"Tap {n.StartTime}";
                    Canvas.SetLeft(tapRect, rectLeft);
                    Canvas.SetTop(tapRect, yStart - rectHeight);
                    SetZIndex(tapRect, 10);
                    _canvas.Children.Add(tapRect);
                }
                else
                {
                    bool hasEnd = n.EndTime.HasValue;
                    double relEndRaw = hasEnd ? (n.EndTime.GetValueOrDefault() - firstTime) : double.NaN;
                    bool endInWindow = hasEnd && (relEndRaw > 0) && (relEndRaw <= timeWindow);
                    bool endAfterStart = hasEnd && (n.EndTime.GetValueOrDefault() > n.StartTime);

                    if (endInWindow && endAfterStart)
                    {
                        double relEnd = Math.Max(0, Math.Min(timeWindow, n.EndTime.GetValueOrDefault() - firstTime));
                        double yEndIn = mapBase + mapSpan - (relEnd / timeWindow) * mapSpan;

                        // 计算整个长按音符的完整高度
                        double holdTop = yEndIn;
                        double holdHeight = Math.Max(rectHeight, yStart - yEndIn); // 至少有头部的高度

                        // 创建一个完整的长按矩形，无圆角，使用统一的颜色
                        var holdRect = MakeRect(rectWidth, holdHeight, PreviewConstants.HoldHeadBrush);
                        holdRect.ToolTip = (object)$"Hold {n.StartTime} → {n.EndTime.GetValueOrDefault()}";
                        Canvas.SetLeft(holdRect, rectLeft);
                        Canvas.SetTop(holdRect, holdTop);
                        SetZIndex(holdRect, 10);
                        _canvas.Children.Add(holdRect);
                    }
                    else
                    {
                        // 长按音符的结束部分不在可见区域内，只绘制头部
                        var headRectOnly = MakeRect(rectWidth, rectHeight, PreviewConstants.HoldHeadBrush);
                        headRectOnly.ToolTip = hasEnd ? (object)$"Hold {n.StartTime} → {n.EndTime.GetValueOrDefault()}" : (object)$"Hold {n.StartTime}";
                        Canvas.SetLeft(headRectOnly, rectLeft);
                        Canvas.SetTop(headRectOnly, yStart - rectHeight);
                        SetZIndex(headRectOnly, 10);
                        _canvas.Children.Add(headRectOnly);
                    }
                }
            }
        }

        private void DrawQuarterLines(double firstTime, double timeWindow, double mapBase, double mapSpan, double canvasWidth)
        {
            // 绘制节拍线
            if (_quarterMs > 0)
            {
                var windowStart = firstTime;
                var windowEnd = firstTime + timeWindow;
                var startQ = Math.Ceiling(windowStart / _quarterMs) * _quarterMs;
                for (double t = startQ; t <= windowEnd; t += _quarterMs)
                {
                    double rel = Math.Max(0, Math.Min(timeWindow, t - windowStart));
                    double y = mapBase + mapSpan - (rel / timeWindow) * mapSpan;
                    var qline = new Rectangle { Width = canvasWidth, Height = 1, Fill = PreviewConstants.QuarterLineBrush };
                    Canvas.SetLeft(qline, 0);
                    Canvas.SetTop(qline, y);
                    SetZIndex(qline, 0);
                    _canvas.Children.Add(qline);
                }
            }
        }
    }
}
