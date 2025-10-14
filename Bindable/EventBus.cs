using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OsuParsers.Beatmaps;
using krrTools.Beatmaps;

namespace krrTools.Bindable
{
    /// <summary>
    /// Simple event bus for publishing and subscribing to events.
    /// Alternative to Rx.NET's ISubject.
    /// </summary>
    /// <summary>
    /// 事件总线接口，用于发布-订阅模式的解耦通信。
    /// 
    /// 推荐使用属性注入方式获取 IEventBus 实例：
    /// <code>
    /// [Inject] private IEventBus EventBus { get; set; } = null!;
    /// </code>
    /// 
    /// 使用方式：
    /// 1. 标记属性：[Inject] private IEventBus EventBus { get; set; } = null!;
    /// 2. 自动注入：继承 ReactiveViewModelBase 或手动调用 Injector.InjectServices(this);
    /// 3. 发布事件：EventBus.Publish(new MyEvent());
    /// 4. 订阅事件：EventBus.Subscribe&lt;MyEvent&gt;(HandleEvent);
    /// 
    /// 优势：
    /// - 避免构造函数参数过多
    /// - 支持条件注入（如果已有值则不注入）
    /// - 自动处理服务解析失败
    /// - 统一的依赖注入模式
    /// 
    /// 不推荐的方式（已废弃）：
    /// - 构造函数注入：public MyClass(IEventBus eventBus)
    /// - 手动获取：App.Services.GetRequiredService&lt;IEventBus&gt;()
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 发布事件，默认优先级为0。
        /// 事件将异步处理，不会阻塞调用者。
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        void Publish<T>(T eventData);

        /// <summary>
        /// 订阅事件，返回订阅句柄。使用完毕后应调用 Dispose() 取消订阅。
        /// </summary>
        /// <typeparam name="T">要订阅的事件类型</typeparam>
        /// <param name="handler">事件处理函数</param>
        /// <returns>订阅句柄，用于取消订阅</returns>
        IDisposable Subscribe<T>(Action<T> handler);

        /// <summary>
        /// 发布事件，指定优先级。优先级高的先处理（数字越大优先级越高）。
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        /// <param name="priority">优先级，默认为0</param>
        void Publish<T>(T eventData, int priority);
    }

    public class EventBus : IEventBus
    {
        private event Action<object>? _eventPublished;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<(object Event, int Priority)>> _priorityQueues = new();
        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void Publish<T>(T eventData)
        {
            Publish(eventData, 0); // 默认优先级
        }

        public void Publish<T>(T eventData, int priority)
        {
            if (eventData == null) return;

            var eventType = typeof(T);
            var queue = _priorityQueues.GetOrAdd(eventType, _ => new ConcurrentQueue<(object, int)>());
            queue.Enqueue((eventData, priority));

            // 异步处理队列，避免阻塞发布者
            _ = ProcessQueueAsync(eventType, _cts.Token);
        }

        private async Task ProcessQueueAsync(Type eventType, CancellationToken cancellationToken)
        {
            await _processingSemaphore.WaitAsync(cancellationToken);
            try
            {
                var queue = _priorityQueues.GetOrAdd(eventType, _ => new ConcurrentQueue<(object, int)>());
                
                // 收集所有待处理事件
                var events = new List<(object Event, int Priority)>();
                while (queue.TryDequeue(out var item))
                {
                    events.Add(item);
                }
                
                if (events.Count == 0) return;
                
                // 按优先级排序（高优先级先处理）
                events.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                
                // 逐个发布事件
                foreach (var (evt, _) in events)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    try
                    {
                        _eventPublished?.Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但不中断处理
                        Console.WriteLine($"[EventBus] Error processing event {evt.GetType().Name}: {ex.Message}");
                    }
                    
                    // 小延迟避免过度占用CPU
                    await Task.Delay(1, cancellationToken);
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            Action<object> wrapper = obj => 
            { 
                if (obj is T t) 
                {
                    try
                    {
                        handler(t);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EventBus] Error in event handler for {typeof(T).Name}: {ex.Message}");
                    }
                }
            };
            _eventPublished += wrapper;
            return new Unsubscriber(() => _eventPublished -= wrapper);
        }

        private class Unsubscriber(Action unsubscribe) : IDisposable
        {
            public void Dispose() => unsubscribe();
        }

        // 清理资源
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Event raised when a beatmap changes (simplified unified event)
    /// </summary>
    public enum BeatmapChangeType
    {
        None,
        /// <summary>
        /// 监听器来源变更
        /// </summary>
        FromMonitoring,
        /// <summary>
        /// 拖拽来源变更
        /// </summary>
        FromDropZone
    }

    /// <summary>
    /// 值变化事件接口，包含新旧值对比
    /// </summary>
    public interface IValueChangedEvent<T>
    {
        T OldValue { get; set; }
        T NewValue { get; set; }
    }

    /// <summary>
    /// 值变化事件基类
    /// </summary>
    public class ValueChangedEvent<T> : IValueChangedEvent<T>
    {
        public T OldValue { get; set; } = default!;
        public T NewValue { get; set; } = default!;
    }

    public class BeatmapChangedEvent
    {
        public required string FilePath { get; set; }
        public required string FileName { get; set; }
        
        /// <summary>
        /// Event type to distinguish between path change and full analysis
        /// </summary>
        public BeatmapChangeType ChangeType { get; set; }
        
        /// <summary>
        /// 只读的谱面对象，提供完整的谱面数据访问（可选）
        /// </summary>
        public Beatmap? Beatmap { get; set; }

        /// <summary>
        /// 构造函数，不强制要求Beatmap对象
        /// </summary>
        public BeatmapChangedEvent()
        {
        }

        /// <summary>
        /// 构造函数，设置Beatmap对象，事件携带只读谱面数据
        /// </summary>
        public BeatmapChangedEvent(Beatmap beatmap) : this()
        {
            Beatmap = beatmap;
        }
    }

    public class MonitoringEnabledChangedEvent : ValueChangedEvent<bool> { }

    public class OsuRunningEvent : ValueChangedEvent<bool> { }
    
    /// <summary>
    /// Event raised when beatmap analysis is completed and results are available
    /// </summary>
    public class AnalysisResultChangedEvent
    {        
        /// <summary>
        /// The complete analysis result
        /// </summary>
        public required OsuAnalysisResult AnalysisResult { get; set; }
    }

    /// <summary>
    /// Event raised when preview refresh is requested
    /// </summary>
    public class PreviewRefreshEvent : ValueChangedEvent<bool> { }

    /// <summary>
    /// Event raised when file source changes in FileDropZone
    /// </summary>
    public class FileSourceChangedEvent : ValueChangedEvent<Configuration.FileSource>
    {
        public string[]? Files { get; }
        
        public FileSourceChangedEvent(Configuration.FileSource oldSource, Configuration.FileSource newSource, string[]? files)
        {
            OldValue = oldSource;
            NewValue = newSource;
            Files = files;
        }
    }
}