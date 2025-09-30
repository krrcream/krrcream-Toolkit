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
    internal class DynamicPreviewControl : Grid
    {
        private readonly List<ManiaBeatmap.PreViewManiaNote> _notes;
        private readonly int _columns;
        private readonly double _quarterMs;
        private readonly double _firstTime;
        private readonly Canvas _canvas;
        private double _lastAvailableHeight = -1;
        private double _lastAvailableWidth = -1;

        public DynamicPreviewControl(List<(int time, List<ManiaBeatmap.PreViewManiaNote> notes)> grouped, int columns, double quarterMs)
        {
            // grouped 已排序；展开为单一 notes 列表并记录第一个时间点
            _notes = grouped.SelectMany(g => g.notes).ToList();
            _firstTime = grouped.First().time;
            _columns = columns;
            _quarterMs = quarterMs;

            // 将 Canvas 作为子元素，交由外层 ScrollViewer 控制滚动
            _canvas = new Canvas { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Stretch };
            Children.Add(_canvas);

            SizeChanged += DynamicPreviewControl_SizeChanged;
            Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(Redraw), System.Windows.Threading.DispatcherPriority.Loaded);

            Redraw();
        }

        private void DynamicPreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            double availableWidth = Math.Max(100, ActualWidth - 20); // 保留内边距

            // 时间窗口（尽量与 PreviewProcessor 保持一致）
            double firstTime = _firstTime;
            double quarterWindow = _quarterMs > 0
                ? _quarterMs * PreviewConstants.PreviewWindowUnitCount / PreviewConstants.PreviewWindowUnitBeatDenominator
                : Math.Max(PreviewConstants.MinWindowLengthMs, (_notes.Count > 0 ? _notes.Max(n => n.StartTime) : 0) - firstTime);
            double timeWindow = Math.Max(PreviewConstants.MinWindowLengthMs, quarterWindow);

            // 高度：以控件可用高度为准，保底
            double availableHeight = Math.Max(PreviewConstants.CanvasMinHeight, Math.Max(0, ActualHeight - 20));
            double canvasHeight = availableHeight;

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

            // 背景 lane
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
            foreach (var n in _notes)
            {
                // 计算 落在 第几列（X: 0..512 映射到列）
                var lane = (int)Math.Floor(n.Index / (512.0 / calcCols));
                if (lane < 0) lane = 0; else if (lane >= _columns) lane = Math.Max(0, _columns - 1);

                double relStart = Math.Max(0, Math.Min(timeWindow, n.StartTime - firstTime));
                double yStart = mapBase + mapSpan - (relStart / timeWindow) * mapSpan;

                double rectHeight = PreviewConstants.NoteFixedHeight;
                double rectWidth = Math.Max(2.0, laneWidth - 2.0 * PreviewConstants.NoteSideMargin);
                double rectLeft = PreviewConstants.CanvasLeftPadding + lane * laneWidth + PreviewConstants.NoteSideMargin;

                if (!n.IsHold)
                {
                    var tapRect = MakeRect(rectWidth, rectHeight, PreviewConstants.TapNoteBrush, 3);
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

                    double headTop = yStart - rectHeight;

                    if (endInWindow && endAfterStart)
                    {
                        double relEnd = Math.Max(0, Math.Min(timeWindow, n.EndTime.GetValueOrDefault() - firstTime));
                        double yEndIn = mapBase + mapSpan - (relEnd / timeWindow) * mapSpan;

                        double bodyTop = yEndIn;
                        double bodyHeight = Math.Max(0, yStart - yEndIn);

                        if (bodyHeight > 0)
                        {
                            var bodyRect = MakeRect(rectWidth, bodyHeight, PreviewConstants.HoldBodyBrush);
                            Canvas.SetLeft(bodyRect, rectLeft);
                            Canvas.SetTop(bodyRect, bodyTop);
                            SetZIndex(bodyRect, 5);
                            _canvas.Children.Add(bodyRect);
                        }

                        var headRect = MakeRect(rectWidth, rectHeight, PreviewConstants.HoldHeadBrush, 3);
                        headRect.ToolTip = (object)$"Hold {n.StartTime} → {n.EndTime.GetValueOrDefault()}";
                        Canvas.SetLeft(headRect, rectLeft);
                        Canvas.SetTop(headRect, headTop);
                        SetZIndex(headRect, 10);
                        _canvas.Children.Add(headRect);

                        double tailTop = yEndIn - rectHeight;
                        var tailRect = MakeRect(rectWidth, rectHeight, PreviewConstants.HoldHeadBrush, 3);
                        Canvas.SetLeft(tailRect, rectLeft);
                        Canvas.SetTop(tailRect, tailTop);
                        SetZIndex(tailRect, 10);
                        _canvas.Children.Add(tailRect);
                    }
                    else
                    {
                        var headRectOnly = MakeRect(rectWidth, rectHeight, PreviewConstants.HoldHeadBrush, 3);
                        headRectOnly.ToolTip = hasEnd ? (object)$"Hold {n.StartTime} → {n.EndTime.GetValueOrDefault()}" : (object)$"Hold {n.StartTime}";
                        Canvas.SetLeft(headRectOnly, rectLeft);
                        Canvas.SetTop(headRectOnly, headTop);
                        SetZIndex(headRectOnly, 10);
                        _canvas.Children.Add(headRectOnly);
                    }
                }
            }

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
                    var qline = new Rectangle { Width = _canvas.Width, Height = 1, Fill = PreviewConstants.QuarterLineBrush };
                    Canvas.SetLeft(qline, 0);
                    Canvas.SetTop(qline, y);
                    SetZIndex(qline, 0);
                    _canvas.Children.Add(qline);
                }
            }
        }
    }
}
