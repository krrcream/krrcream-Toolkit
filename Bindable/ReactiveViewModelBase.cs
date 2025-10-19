using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.Bindable
{
    /// <summary>
    /// Base class for reactive ViewModels using the custom Bindable system.
    /// Alternative to ReactiveUI's ReactiveObject.
    /// Automatically injects services marked with [Inject] attribute.
    /// 
    /// 推荐使用属性注入方式注入依赖服务：
    /// [Inject] public IServiceType ServiceName { get; set; } = null!;
    /// 这样可以避免构造函数参数过多，并支持条件注入。
    /// </summary>
    public abstract class ReactiveViewModelBase : INotifyPropertyChanged, IDisposable
    {
        protected readonly List<IDisposable> Disposables = new List<IDisposable>();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected ReactiveViewModelBase()
        {
            // Automatically inject services marked with [Inject] attribute
            this.InjectServices();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            foreach (var d in Disposables) d.Dispose();
            Disposables.Clear();
        }

        protected void SetupAutoBindableNotifications()
        {
            // Handle fields
            var fields = GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    if (field.GetValue(this) is INotifyPropertyChanged bindable)
                    {
                        string propName = field.Name.TrimStart('_');
                        propName = char.ToUpper(propName[0]) + propName.Substring(1);
                        bindable.PropertyChanged += (_, _) => OnPropertyChanged(propName);
                    }
                }
            }

            // Handle properties
            var properties = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    if (prop.GetValue(this) is INotifyPropertyChanged bindable)
                    {
                        bindable.PropertyChanged += (_, _) => OnPropertyChanged(prop.Name);
                    }
                }
            }
        }
    }
}