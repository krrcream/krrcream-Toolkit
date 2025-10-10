using System;

namespace krrTools.Bindable
{
    /// <summary>
    /// Simple event bus for publishing and subscribing to events.
    /// Alternative to Rx.NET's ISubject.
    /// </summary>
    public interface IEventBus
    {
        void Publish<T>(T eventData);
        IDisposable Subscribe<T>(Action<T> handler);
    }

    public class EventBus : IEventBus
    {
        private event Action<object> _eventPublished;

        public void Publish<T>(T eventData)
        {
            _eventPublished?.Invoke(eventData);
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            Action<object> wrapper = obj => { if (obj is T t) handler(t); };
            _eventPublished += wrapper;
            return new Unsubscriber(() => _eventPublished -= wrapper);
        }

        private class Unsubscriber : IDisposable
        {
            private readonly Action _unsubscribe;
            public Unsubscriber(Action unsubscribe) => _unsubscribe = unsubscribe;
            public void Dispose() => _unsubscribe();
        }
    }

    /// <summary>
    /// Event raised when a file has changed (e.g., beatmap analyzed)
    /// </summary>
    public class FileChangedEvent
    {
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? ChangeType { get; set; }
    }

    /// <summary>
    /// Event raised when preview refresh is requested
    /// </summary>
    public class PreviewRefreshEvent
    {
        public string? Reason { get; set; }
        public bool ForceRedraw { get; set; }
    }
}