using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using krrTools.Beatmaps;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.Preview
{
    /// <summary>
    ///     绘制视觉主机，用于托管DrawingVisual对象
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

        protected override int VisualChildrenCount
        {
            get => _children.Count;
        }

        protected override Visual GetVisualChild(int index)
        {
            return _children[index];
        }
    }

    /// <summary>
    ///     分离绘制的预览控件 - 小节线和音符分开绘制，解决闪烁问题
    /// </summary>
    internal class LayeredPreviewControl : Grid
    {
        // 常量
        private const double lane_spacing = 4.0;

        // 绘制主机
        private DrawingVisualHost barlineHost = null!; // 小节线层（静态）

        // UI组件
        private Canvas _canvas = null!;
        private double _canvasHeight;

        // 布局状态
        private double _canvasWidth;
        private int _columns;
        private string? _currentBeatmapPath; // 用于判断是否需要重绘小节线
        private double _firstTime;
        private bool _initialScrollSet;
        private double _laneWidth;
        private bool _needsRefresh; // 标记是否需要刷新
        private DrawingVisualHost _noteHost = null!; // 音符层（动态）

        // 数据状态
        private List<ManiaHitObject> _notes = new List<ManiaHitObject>();
        private bool _pendingRedrawBarlines; // 待处理的barlines重绘标志
        private double _pixelsPerMs;
        private double _quarterMs;
        private ScrollViewer _scrollViewer = null!;
        private double _totalTimeRange;

        public LayeredPreviewControl()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // 创建绘制主机
            barlineHost = new DrawingVisualHost();
            _noteHost = new DrawingVisualHost();

            // 创建Canvas并添加层级
            _canvas = new Canvas
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // 按Z-Order添加：小节线在底层，音符在顶层
            _canvas.Children.Add(barlineHost);
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
            // 如果有待处理的刷新请求，现在执行
            if (_needsRefresh)
            {
                _needsRefresh = false;
                Dispatcher.BeginInvoke(() => RefreshDisplay(_pendingRedrawBarlines),
                                       DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        ///     更新预览数据
        /// </summary>
        public void UpdatePreview(List<ManiaHitObject> notes, int columns, double quarterMs, string? beatmapPath = null)
        {
            string? oldBeatmapPath = _currentBeatmapPath;
            double oldQuarterMs = _quarterMs;

            // 更新数据
            _notes = notes.OrderBy(n => n.StartTime).ToList();
            _columns = columns;
            _quarterMs = quarterMs;
            _firstTime = _notes.Any() ? _notes.Min(n => n.StartTime) : 0;
            _currentBeatmapPath = beatmapPath;

            // 判断是否需要重绘小节线
            bool needRedrawBarlines = oldBeatmapPath != _currentBeatmapPath ||
                                      Math.Abs(oldQuarterMs - _quarterMs) > 0.001;

            // Console.WriteLine($"[LayeredPreview] Update queued: notes={_notes.Count}, columns={_columns}, " +
            //                   $"needRedrawBarlines={needRedrawBarlines}");

            // 如果控件还没有加载，标记需要刷新，等待OnLoaded
            if (!IsLoaded)
            {
                _needsRefresh = true;
                _pendingRedrawBarlines = needRedrawBarlines;
            }
            else
            {
                // 直接刷新显示
                RefreshDisplay(needRedrawBarlines);
            }
        }

        private void RefreshDisplay(bool redrawBarlines = true)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                CalculateLayout();

                _canvas.Width = _canvasWidth;
                _canvas.Height = _canvasHeight;

                if (redrawBarlines) DrawBarlines();

                DrawNotes();

                HandleInitialScroll();
            }
            catch (Exception e)
            {
                Logger.WriteLine(LogLevel.Error, "[LayeredPreview] Refresh error: {0}", e);
            }
            finally
            {
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 3) // 用于调试高耗时，及重复刷新事件
                {
                    Logger.WriteLine(LogLevel.Debug, "[LayeredPreview] Refresh took {0} ms",
                                     stopwatch.ElapsedMilliseconds);
                }
            }
        }

        private void CalculateLayout()
        {
            double availableWidth = Math.Max(100, ActualWidth - 20);

            // 计算时间范围
            double firstTime = _firstTime;
            double lastTime = firstTime;

            if (_notes.Count > 0)
            {
                lastTime = _notes.Max(n => n.StartTime);
                int maxHoldEndTime = _notes.Where(n => n.IsHold).Select(n => n.EndTime)
                                           .DefaultIfEmpty().Max();
                if (maxHoldEndTime > 0)
                    lastTime = Math.Max(lastTime, maxHoldEndTime);
            }

            _totalTimeRange = Math.Max(PreviewConstants.MIN_WINDOW_LENGTH_MS, lastTime - firstTime);

            // 计算像素密度
            const int desired_lines = 8;
            double timeRangeForLines = desired_lines * _quarterMs;
            const double min_pixels_per_ms = 0.08;
            const double max_pixels_per_ms = 0.3;
            _pixelsPerMs = Math.Clamp(ActualHeight / timeRangeForLines, min_pixels_per_ms, max_pixels_per_ms);

            // 计算Canvas尺寸
            double totalCanvasHeight = _totalTimeRange * _pixelsPerMs;
            double availableHeight = Math.Max(PreviewConstants.CANVAS_MIN_HEIGHT, ActualHeight);
            _canvasHeight = Math.Max(availableHeight, totalCanvasHeight);

            // 计算列宽
            double totalSpacing = (_columns - 1) * lane_spacing;
            double contentWidth = Math.Max(10,
                                           Math.Min(PreviewConstants.MAX_CONTENT_WIDTH, availableWidth) -
                                           PreviewConstants.CANVAS_PADDING);

            _laneWidth = Math.Clamp((contentWidth - totalSpacing) / Math.Max(1, _columns),
                                    PreviewConstants.LANE_MIN_WIDTH, PreviewConstants.LANE_MAX_WIDTH);

            _canvasWidth = PreviewConstants.CANVAS_PADDING + (_laneWidth * _columns);
        }

        /// <summary>
        ///     绘制小节线层（静态，只在谱面或BPM变化时重绘）
        /// </summary>
        private void DrawBarlines()
        {
            barlineHost.Clear();

            if (_quarterMs <= 0) return;

            DrawingGroup drawing = CreateBarlineDrawing();
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) dc.DrawDrawing(drawing);

            barlineHost.AddVisual(visual);
        }

        private DrawingGroup CreateBarlineDrawing()
        {
            var dg = new DrawingGroup();

            using (DrawingContext dc = dg.Open())
            {
                double measureMs = 2 * _quarterMs; // 小节间隔：2拍
                double windowStart = _firstTime;
                double windowEnd = _firstTime + _totalTimeRange;

                for (double t = windowStart; t <= windowEnd; t += measureMs)
                {
                    double relTime = t - _firstTime;
                    double y = (_totalTimeRange - relTime) * _pixelsPerMs;

                    dc.DrawRectangle(PreviewConstants.BAR_LINE_BRUSH, null,
                                     new Rect(0, y, _canvasWidth, 1));
                }
            }

            dg.Freeze();
            return dg;
        }

        /// <summary>
        ///     绘制音符层（动态，每次设置变化都重绘）
        /// </summary>
        private void DrawNotes()
        {
            _noteHost.Clear();

            if (_notes.Count == 0) return;

            // 获取可见音符
            List<ManiaHitObject> visibleNotes = GetVisibleNotes();
            if (visibleNotes.Count == 0) return;

            DrawingGroup drawing = CreateNoteDrawing(visibleNotes);
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) dc.DrawDrawing(drawing);

            _noteHost.AddVisual(visual);
        }

        private List<ManiaHitObject> GetVisibleNotes()
        {
            double startTime = _firstTime;
            double endTime = _firstTime + _totalTimeRange;
            return _notes.Where(n => n.StartTime >= startTime && n.StartTime <= endTime).ToList();
        }

        private DrawingGroup CreateNoteDrawing(List<ManiaHitObject> notes)
        {
            var dg = new DrawingGroup();

            using (DrawingContext dc = dg.Open())
            {
                const double note_height = PreviewConstants.NOTE_FIXED_HEIGHT;

                foreach (ManiaHitObject note in notes)
                {
                    // 计算列位置
                    int lane = (int)Math.Floor(note.Index / (512.0 / Math.Max(1, _columns)));
                    lane = Math.Clamp(lane, 0, Math.Max(0, _columns - 1));

                    // 计算Y位置
                    double relStart = note.StartTime - _firstTime;
                    double yStart = (_totalTimeRange - relStart) * _pixelsPerMs;

                    // 计算矩形参数
                    double rectWidth = Math.Max(2.0, _laneWidth * 0.95);
                    double rectLeft = PreviewConstants.CANVAS_PADDING + (lane * _laneWidth) +
                                      ((_laneWidth - rectWidth) / 2);

                    // 绘制音符
                    if (!note.IsHold)
                    {
                        // 普通音符
                        dc.DrawRectangle(PreviewConstants.TAP_NOTE_BRUSH, null,
                                         new Rect(rectLeft, yStart - note_height, rectWidth, note_height));
                    }
                    else
                    {
                        // 长按音符
                        bool hasEnd = note.IsHold && note.EndTime > note.StartTime;

                        if (hasEnd)
                        {
                            double relEnd = note.EndTime - _firstTime;
                            double yEnd = (_totalTimeRange - relEnd) * _pixelsPerMs;
                            double holdHeight = Math.Max(note_height, yStart - yEnd);

                            dc.DrawRectangle(PreviewConstants.HOLD_HEAD_BRUSH, null,
                                             new Rect(rectLeft, yEnd, rectWidth, holdHeight));
                        }
                        else
                        {
                            dc.DrawRectangle(PreviewConstants.HOLD_HEAD_BRUSH, null,
                                             new Rect(rectLeft, yStart - note_height, rectWidth, note_height));
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
                Dispatcher.BeginInvoke(() => _scrollViewer.ScrollToVerticalOffset(
                                           Math.Max(0, _canvas.Height - _scrollViewer.ViewportHeight)));
                _initialScrollSet = true;
            }
        }
    }
}
