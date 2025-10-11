using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using krrTools.Configuration;

namespace krrTools.Bindable
{
    /// <summary>
    /// Reactive options wrapper that provides automatic persistence for bindable options.
    /// Replaces ObservableOptions with simplified Bindable&lt;T&gt; integration.
    /// </summary>
    /// <typeparam name="TOptions">The options type, should use Bindable&lt;T&gt; for properties.</typeparam>
    public class ReactiveOptions<TOptions> : INotifyPropertyChanged, IDisposable where TOptions : ToolOptionsBase, new()
    {
        private readonly ConverterEnum _converter;

        public TOptions Options { get; private set; }

        public ReactiveOptions(ConverterEnum converter)
        {
            _converter = converter;
            Options = BaseOptionsManager.LoadOptions<TOptions>(converter) ?? new TOptions();

            // Listen to options PropertyChanged for auto-save
            if (Options is INotifyPropertyChanged notifyOptions)
            {
                notifyOptions.PropertyChanged += OnOptionsPropertyChanged;
            }

            Console.WriteLine($"[ReactiveOptions] {typeof(TOptions).Name} reactive options initialized");
        }

        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Auto-save on property change
            BaseOptionsManager.SaveOptions(_converter, Options);
            Console.WriteLine($"[ReactiveOptions] Auto-saved {typeof(TOptions).Name}.{e.PropertyName}");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (Options is INotifyPropertyChanged notifyOptions)
            {
                notifyOptions.PropertyChanged -= OnOptionsPropertyChanged;
            }
            Console.WriteLine($"[ReactiveOptions] {typeof(TOptions).Name} disposed");
        }

        // Implicit conversion to TOptions
        public static implicit operator TOptions(ReactiveOptions<TOptions> reactive)
        {
            return reactive.Options;
        }
    }
}
