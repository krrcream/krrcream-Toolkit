using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using krrTools.Beatmaps;

namespace krrTools.Tools.Preview
{
    /// <summary>
    /// 绘制视觉主机，用于托管DrawingVisual对象
    /// </summary>
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

    /// <summary>
    /// 分离绘制的预览控件 - 小节线和音符分开绘制，解决闪烁问题
    /// </summary>
    internal class LayeredPreviewControl : Grid
    {
        // 绘制主机
        private DrawingVisualHost _barlineHost = null!; // 小节线层（静态）
        private DrawingVisualHost _noteHost = null!; // 音符层（动态）

        // UI组件
        private Canvas _canvas = null!;
        private ScrollViewer _scrollViewer = null!;

        // 数据状态
        private List<ManiaHitObject> _notes = new();
        private int _columns;
        private double _quarterMs;
        private double _firstTime;
        private string? _currentBeatmapPath; // 用于判断是否需要重绘小节线

        // 布局状态
        private double _canvasWidth;
        private double _canvasHeight;
        private double _laneWidth;
        private double _pixelsPerMs;
        private double _totalTimeRange;
        private bool _initialScrollSet;

        // 常量
        private const double LaneSpacing = 4.0;

        public LayeredPreviewControl()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // 创建绘制主机
            _barlineHost = new DrawingVisualHost();
            _noteHost = new DrawingVisualHost();

            // 创建Canvas并添加层级
            _canvas = new Canvas
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // 按Z-Order添加：小节线在底层，音符在顶层
            _canvas.Children.Add(_barlineHost);
            _canvas.Children.Add(_noteHost);

            // 创建滚动视图
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

            Loaded += OnLoaded;
        }



        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() => RefreshDisplay(),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 更新预览数据
        /// </summary>
        public void UpdatePreview(List<ManiaHitObject> notes, int columns, double quarterMs, string? beatmapPath = null)
        {
            var oldBeatmapPath = _currentBeatmapPath;
            var oldQuarterMs = _quarterMs;

            // 更新数据
            _notes = notes.OrderBy(n => n.StartTime).ToList();
            _columns = columns;
            _quarterMs = quarterMs;
            _firstTime = _notes.Any() ? _notes.Min(n => n.StartTime) : 0;
            _currentBeatmapPath = beatmapPath;

            // 判断是否需要重绘小节线
            var needRedrawBarlines = oldBeatmapPath != _currentBeatmapPath ||
                                     Math.Abs(oldQuarterMs - _quarterMs) > 0.001;

            // Console.WriteLine($"[LayeredPreview] Update queued: notes={_notes.Count}, columns={_columns}, " +
            //                   $"needRedrawBarlines={needRedrawBarlines}");

            // 直接刷新显示
            RefreshDisplay(needRedrawBarlines);
        }

        private void RefreshDisplay(bool redrawBarlines = true)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                CalculateLayout();

                _canvas.Width = _canvasWidth;
                _canvas.Height = _canvasHeight;

                if (redrawBarlines)
                {
                    DrawBarlines();
                }

                DrawNotes();

                HandleInitialScroll();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LayeredPreview] Refresh error: {e}");
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3)
                {
                    Console.WriteLine($"[LayeredPreview] Warning: Refresh took {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        private void CalculateLayout()
        {
            var availableWidth = Math.Max(100, ActualWidth - 20);

            // 计算时间范围
            var firstTime = _firstTime;
            var lastTime = firstTime;
            if (_notes.Count > 0)
            {
                lastTime = _notes.Max(n => n.StartTime);
                var maxHoldEndTime = _notes.Where(n => n.IsHold).Select(n => n.EndTime)
                    .DefaultIfEmpty().Max();
                if (maxHoldEndTime > 0)
                    lastTime = Math.Max(lastTime, maxHoldEndTime);
            }

            _totalTimeRange = Math.Max(PreviewConstants.MinWindowLengthMs, lastTime - firstTime);

            // 计算像素密度
            const int desiredLines = 8;
            var timeRangeForLines = desiredLines * _quarterMs;
            const double minPixelsPerMs = 0.08;
            const double maxPixelsPerMs = 0.3;
            _pixelsPerMs = Math.Clamp(ActualHeight / timeRangeForLines, minPixelsPerMs, maxPixelsPerMs);

            // 计算Canvas尺寸
            var totalCanvasHeight = _totalTimeRange * _pixelsPerMs;
            var availableHeight = Math.Max(PreviewConstants.CanvasMinHeight, ActualHeight);
            _canvasHeight = Math.Max(availableHeight, totalCanvasHeight);

            // 计算列宽
            var totalSpacing = (_columns - 1) * LaneSpacing;
            var contentWidth = Math.Max(10,
                Math.Min(PreviewConstants.MaxContentWidth, availableWidth) - PreviewConstants.CanvasPadding);

            _laneWidth = Math.Clamp((contentWidth - totalSpacing) / Math.Max(1, _columns),
                PreviewConstants.LaneMinWidth, PreviewConstants.LaneMaxWidth);

            _canvasWidth = PreviewConstants.CanvasPadding + _laneWidth * _columns;
        }

        /// <summary>
        /// 绘制小节线层（静态，只在谱面或BPM变化时重绘）
        /// </summary>
        private void DrawBarlines()
        {
            _barlineHost.Clear();

            if (_quarterMs <= 0) return;

            var drawing = CreateBarlineDrawing();
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawDrawing(drawing);
            }

            _barlineHost.AddVisual(visual);
        }

        private DrawingGroup CreateBarlineDrawing()
        {
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                var measureMs = 2 * _quarterMs; // 小节间隔：2拍
                var windowStart = _firstTime;
                var windowEnd = _firstTime + _totalTimeRange;

                for (var t = windowStart; t <= windowEnd; t += measureMs)
                {
                    var relTime = t - _firstTime;
                    var y = (_totalTimeRange - relTime) * _pixelsPerMs;

                    dc.DrawRectangle(PreviewConstants.BarLineBrush, null,
                        new Rect(0, y, _canvasWidth, 1));
                }
            }

            dg.Freeze();
            return dg;
        }

        /// <summary>
        /// 绘制音符层（动态，每次设置变化都重绘）
        /// </summary>
        private void DrawNotes()
        {
            _noteHost.Clear();

            if (_notes.Count == 0) return;

            // 获取可见音符
            var visibleNotes = GetVisibleNotes();
            if (visibleNotes.Count == 0) return;

            var drawing = CreateNoteDrawing(visibleNotes);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawDrawing(drawing);
            }

            _noteHost.AddVisual(visual);
        }

        private List<ManiaHitObject> GetVisibleNotes()
        {
            var startTime = _firstTime;
            var endTime = _firstTime + _totalTimeRange;
            return _notes.Where(n => n.StartTime >= startTime && n.StartTime <= endTime).ToList();
        }

        private DrawingGroup CreateNoteDrawing(List<ManiaHitObject> notes)
        {
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                const double noteHeight = PreviewConstants.NoteFixedHeight;

                foreach (var note in notes)
                {
                    // 计算列位置
                    var lane = (int)Math.Floor(note.Index / (512.0 / Math.Max(1, _columns)));
                    lane = Math.Clamp(lane, 0, Math.Max(0, _columns - 1));

                    // 计算Y位置
                    var relStart = note.StartTime - _firstTime;
                    var yStart = (_totalTimeRange - relStart) * _pixelsPerMs;

                    // 计算矩形参数
                    var rectHeight = noteHeight;
                    var rectWidth = Math.Max(2.0, _laneWidth * 0.95);
                    var rectLeft = PreviewConstants.CanvasPadding + lane * _laneWidth +
                                   (_laneWidth - rectWidth) / 2;

                    // 绘制音符
                    if (!note.IsHold)
                    {
                        // 普通音符
                        dc.DrawRectangle(PreviewConstants.TapNoteBrush, null,
                            new Rect(rectLeft, yStart - rectHeight, rectWidth, rectHeight));
                    }
                    else
                    {
                        // 长按音符
                        var hasEnd = note.IsHold && note.EndTime > note.StartTime;

                        if (hasEnd)
                        {
                            var relEnd = note.EndTime - _firstTime;
                            var yEnd = (_totalTimeRange - relEnd) * _pixelsPerMs;
                            var holdHeight = Math.Max(rectHeight, yStart - yEnd);

                            dc.DrawRectangle(PreviewConstants.HoldHeadBrush, null,
                                new Rect(rectLeft, yEnd, rectWidth, holdHeight));
                        }
                        else
                        {
                            dc.DrawRectangle(PreviewConstants.HoldHeadBrush, null,
                                new Rect(rectLeft, yStart - rectHeight, rectWidth, rectHeight));
                        }
                    }
                }
            }

            dg.Freeze();
            return dg;
        }

        private void HandleInitialScroll()
        {
            if (!_initialScrollSet)
            {
                Dispatcher.BeginInvoke(() =>
                    _scrollViewer.ScrollToVerticalOffset(
                        Math.Max(0, _canvas.Height - _scrollViewer.ViewportHeight)));
                _initialScrollSet = true;
            }
        }
    }
}