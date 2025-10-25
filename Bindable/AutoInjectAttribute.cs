using System;

namespace krrTools.Bindable
{
    /// <summary>
    /// Attribute to mark classes that should automatically inject services on creation.
    /// Classes marked with this attribute will have their [Inject] properties automatically        
    /// injected when instantiated through normal constructors.
    ///
    /// This provides a more advanced form of automatic injection without manual calls.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoInjectAttribute : Attribute
    {
        /// <summary>
        /// Whether to inject services in constructors (default: true)
        /// </summary>
        public bool InjectInConstructor { get; set; } = true;
    }
}
