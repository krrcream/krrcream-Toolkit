using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace krrTools.Bindable
{
    /// <summary>
    /// Base class for reactive ViewModels using the custom Bindable system.
    /// <para>Alternative to ReactiveUI's ReactiveObject.</para>
    /// Automatically injects services marked with [Inject] attribute.
    /// <para></para>
    /// 推荐使用属性注入方式注入依赖服务：
    /// <code>[Inject] public IServiceType ServiceName { get; set; } = null!;</code>
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
            foreach (IDisposable d in Disposables) d.Dispose();
            Disposables.Clear();
        }

        protected void SetupAutoBindableNotifications()
        {
            // Handle fields
            FieldInfo[] fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
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
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    if (prop.GetValue(this) is INotifyPropertyChanged bindable)
                        bindable.PropertyChanged += (_, _) => OnPropertyChanged(prop.Name);
                }
            }
        }
    }
}
