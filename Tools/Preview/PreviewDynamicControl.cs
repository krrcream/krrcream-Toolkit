using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Beatmaps;

namespace krrTools.Tools.Preview;

internal class DrawingVisualHost : FrameworkElement
{
    private readonly VisualCollection _children;

    public DrawingVisualHost()
    {
        _children = new VisualCollection(this);
    }

    public void AddVisual(DrawingVisual visual)
    {
        _children.Add(visual);
    }

    public void Clear()
    {
        _children.Clear();
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];
}

internal class PreviewDynamicControl : Grid
{
    private readonly List<ManiaHitObject> _notes;
    private readonly int _columns;
    private readonly double _quarterMs;
    private readonly double _firstTime;
    private readonly Canvas _canvas;
    private readonly ScrollViewer _scrollViewer;
    private readonly DrawingVisualHost _visualHost;
    private double _lastAvailableHeight = -1;
    private double _lastAvailableWidth = -1;
    private bool _initialScrollSet;

    // 多线程预计算
    private Task? _calculationTask;
    
    // 增量更新：跟踪变化
    private List<ManiaHitObject> _lastNotes;

    private const double LaneSpacing = 4.0;

    public PreviewDynamicControl(List<(int time, List<ManiaHitObject> notes)> grouped, int columns,
        double quarterMs)
    {
        // grouped 已排序；展开为单一 notes 列表并记录第一个时间点
        _notes = grouped.SelectMany(g => g.notes).OrderBy(n => n.StartTime).ToList();
        _firstTime = grouped.First().time;
        _columns = columns;
        _quarterMs = quarterMs;

        // 初始化增量更新字段
        _lastNotes = new List<ManiaHitObject>(_notes);
        _calculationTask = Task.CompletedTask;

        // 初始化 DrawingVisualHost
        _visualHost = new DrawingVisualHost();

        // 将 Canvas 放在 ScrollViewer 中，支持滚动
        _canvas = new Canvas
        {
            Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _canvas.Children.Add(_visualHost);
        _scrollViewer = new ScrollViewer
        {
            Content = _canvas,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Children.Add(_scrollViewer);

        // 启用硬件加速
        RenderOptions.SetEdgeMode(_canvas, EdgeMode.Aliased);
        CacheMode = new BitmapCache();

        Loaded += (_, _) =>
            Dispatcher.BeginInvoke(new Action(Redraw), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Redraw()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 增量更新：检查数据是否变化
            var notesChanged = !_notes.SequenceEqual(_lastNotes);

            if (!notesChanged && Math.Abs(_lastAvailableHeight - _canvas.Height) < 5 &&
                Math.Abs(_lastAvailableWidth - _canvas.Width) < 5) return;
            _lastNotes = new List<ManiaHitObject>(_notes);

            var availableWidth = Math.Max(100, ActualWidth - 20); // 保留内边距

            // 时间窗口：基于固定行间距计算
            var firstTime = _firstTime;

            // 计算整个谱面的时间范围，用于确定canvas的总高度
            var lastTime = firstTime;
            if (_notes.Count > 0)
            {
                lastTime = _notes.Max(n => n.StartTime);
                var maxHoldEndTime = _notes.Where(n => n.IsHold).Select(n => n.EndTime)
                    .DefaultIfEmpty().Max();
                if (maxHoldEndTime > 0) lastTime = Math.Max(lastTime, maxHoldEndTime);
            }

            var totalTimeRange = Math.Max(PreviewConstants.MinWindowLengthMs, lastTime - firstTime);
// 设定最大和最小密度
            const double maxPixelsPerMs = 0.3;
            const double minPixelsPerMs = 0.08;
            const int maxNotes = 2000;

// 线性插值，音符越多，pixelsPerMs 越小
            var t = Math.Clamp(_notes.Count / (double)maxNotes, 0, 1);
            var pixelsPerMs = maxPixelsPerMs * t + minPixelsPerMs * (1 - t);
            
            // Canvas高度：基于总时间范围和像素每毫秒计算
            var totalCanvasHeight = totalTimeRange * pixelsPerMs;

            // 高度：容器可用高度
            var availableHeight = Math.Max(PreviewConstants.CanvasMinHeight, ActualHeight);
            var canvasHeight = Math.Max(availableHeight, totalCanvasHeight);

            _lastAvailableHeight = canvasHeight;
            _lastAvailableWidth = availableWidth;

            var totalSpacing = (_columns - 1) * LaneSpacing;
            var contentWidth = Math.Max(10, 
                Math.Min(PreviewConstants.MaxContentWidth, availableWidth) - PreviewConstants.CanvasPadding);

            var laneWidth = Math.Clamp((contentWidth - totalSpacing) / Math.Max(1, _columns), PreviewConstants.LaneMinWidth, 
                PreviewConstants.LaneMaxWidth);
            
            var canvasWidth = PreviewConstants.CanvasPadding + laneWidth * _columns;

            // 设置 canvas
            _canvas.Width = canvasWidth;
            _canvas.Height = canvasHeight;

            // 后台计算和绘制
            if (_calculationTask is { IsCompleted: false })
                _calculationTask.Wait(); // 等待之前的任务完成
            _calculationTask = Task.Run(() => CalculateAndDraw(firstTime, totalTimeRange, pixelsPerMs, laneWidth, canvasWidth));

            // 初始滚动到底部，显示时间最早的部分
            if (!_initialScrollSet)
            {
                Dispatcher.BeginInvoke(() =>
                    _scrollViewer.ScrollToVerticalOffset(Math.Max(0, _canvas.Height - _scrollViewer.ViewportHeight)));
                _initialScrollSet = true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[PreviewDynamicControl] Redraw error: {e}");
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"Redraw took {stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private async Task CalculateAndDraw(double firstTime, double totalTimeRange, double pixelsPerMs, double laneWidth, double canvasWidth)
    {
        try
        {
            // 获取可见音符
            var visibleNotes = GetVisibleNotes(firstTime, totalTimeRange);

            // 创建 Drawing
            var drawing = CreateDrawing(visibleNotes, laneWidth, firstTime, pixelsPerMs, totalTimeRange, canvasWidth);

            // 在 UI 线程更新
            await Dispatcher.InvokeAsync(() =>
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawDrawing(drawing);
                }
                _visualHost.Clear();
                _visualHost.AddVisual(dv);
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[PreviewDynamicControl] CalculateAndDraw error: {e}");
        }
    }

    private List<ManiaHitObject> GetVisibleNotes(double firstTime, double totalTimeRange)
    {
        var startTime = firstTime;
        var endTime = firstTime + totalTimeRange;
        var startIndex = LowerBound(_notes, startTime);
        var endIndex = LowerBound(_notes, endTime + 1); // > endTime
        return _notes.GetRange(startIndex, endIndex - startIndex);
    }

    private int LowerBound(List<ManiaHitObject> list, double time)
    {
        int low = 0, high = list.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (list[mid].StartTime < time) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private DrawingGroup CreateDrawing(List<ManiaHitObject> notes, double laneWidth, double firstTime,
        double pixelsPerMs, double totalTimeRange, double canvasWidth)
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            const double noteHeight = PreviewConstants.NoteFixedHeight;

            // 绘制节拍线
            if (_quarterMs > 0)
            {
                var windowStart = firstTime;
                var windowEnd = firstTime + totalTimeRange;
                var startQ = Math.Ceiling(windowStart / _quarterMs) * _quarterMs;
                for (var t = startQ; t <= windowEnd; t += _quarterMs)
                {
                    var relTime = t - firstTime;
                    var y = (totalTimeRange - relTime) * pixelsPerMs;

                    dc.DrawRectangle(PreviewConstants.BarLineBrush, null, new Rect(0, y, canvasWidth, 1));
                }
            }

            // 绘制音符
            foreach (var n in notes)
            {
                // 计算 落在 第几列（X: 0..512 映射到列）
                var lane = (int)Math.Floor(n.Index / (512.0 / Math.Max(1, _columns)));
                if (lane < 0) lane = 0;
                else if (lane >= _columns) lane = Math.Max(0, _columns - 1);

                var relStart = n.StartTime - firstTime;
                var yStart = (totalTimeRange - relStart) * pixelsPerMs;

                var rectHeight = noteHeight;
                var rectWidth = Math.Max(2.0, laneWidth * 0.95); // 音符宽度为 laneWidth 的 x%，提供左右边距
                var rectLeft = PreviewConstants.CanvasPadding + lane * laneWidth + (laneWidth - rectWidth) / 2; // 居中对齐

                if (!n.IsHold)
                {
                    dc.DrawRectangle(PreviewConstants.TapNoteBrush, null, new Rect(rectLeft, yStart - rectHeight, rectWidth, rectHeight));
                }
                else
                {
                    var hasEnd = n.IsHold;
                    var endAfterStart = hasEnd && n.EndTime > n.StartTime;

                    if (hasEnd && endAfterStart)
                    {
                        var relEnd = n.EndTime - firstTime;
                        var yEndIn = (totalTimeRange - relEnd) * pixelsPerMs;

                        // 计算整个长按音符的完整高度
                        var holdTop = yEndIn;
                        var holdHeight = Math.Max(rectHeight, yStart - yEndIn); // 至少有头部的高度

                        dc.DrawRectangle(PreviewConstants.HoldHeadBrush, null, new Rect(rectLeft, holdTop, rectWidth, holdHeight));
                    }
                    else
                    {
                        // 长按音符的结束部分不在可见区域内，只绘制头部
                        dc.DrawRectangle(PreviewConstants.HoldHeadBrush, null, new Rect(rectLeft, yStart - rectHeight, rectWidth, rectHeight));
                    }
                }
            }
        }
        dg.Freeze();
        return dg;
    }
}