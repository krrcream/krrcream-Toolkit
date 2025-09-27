using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace krrTools.tools.DPtool
{
    /// <summary>
    /// A small reusable base class that implements <see cref="INotifyPropertyChanged"/> and provides a <see cref="SetProperty{T}"/> helper.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for <paramref name="propertyName"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed. Automatically supplied by the compiler when omitted.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets <paramref name="storage"/> to <paramref name="value"/> and raises <see cref="PropertyChanged"/> if the value actually changed.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="storage">Reference to the backing field.</param>
        /// <param name="value">New value to set.</param>
        /// <param name="propertyName">The property name (automatically supplied by the compiler when omitted).</param>
        /// <returns>True if the value was changed; false if the new value equals the old value.</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
