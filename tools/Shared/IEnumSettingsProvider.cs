using System;
using System.ComponentModel;

namespace krrTools.Tools.Shared
{
    // Provider that exposes get/set by enum key and notifies on changes
    public interface IEnumSettingsProvider : INotifyPropertyChanged
    {
        object? GetValue(Enum key);
        void SetValue(Enum key, object? value);
    }
}
