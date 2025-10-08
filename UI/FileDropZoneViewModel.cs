using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.Tools.Preview;
using krrTools.Utilities;
using OsuParsers.Decoders;

namespace krrTools.UI
{
    public class FileDropZoneViewModel : INotifyPropertyChanged
    {
        public enum FileSource
        {
            None,
            Dropped,
            Listened
        }

        private FileSource _currentSource = FileSource.None;
        private string[]? _stagedPaths;
        private string? _backgroundPath;

        // 依赖注入
        private readonly PreviewViewDual? _previewDual;
        private readonly FileDispatcher? _fileDispatcher;
        private readonly Func<ConverterEnum>? _getActiveTabTag;

        // 本地化
        private readonly DynamicLocalizedString _dropHintLocalized = new(Strings.DropHint);
        private readonly DynamicLocalizedString _dropFilesHintLocalized = new(Strings.DropFilesHint);

        // 属性
        private string _displayText = string.Empty;
        public string DisplayText
        {
            get => _displayText;
            private set => SetProperty(ref _displayText, value);
        }

        private bool _isConversionEnabled;
        public bool IsConversionEnabled
        {
            get => _isConversionEnabled;
            private set => SetProperty(ref _isConversionEnabled, value);
        }

        public event EventHandler<string[]>? FilesDropped;

        public FileDropZoneViewModel(PreviewViewDual? previewDual, FileDispatcher? fileDispatcher, Func<ConverterEnum>? getActiveTabTag)
        {
            _previewDual = previewDual;
            _fileDispatcher = fileDispatcher;
            _getActiveTabTag = getActiveTabTag;
            UpdateDisplayText();
        }

        public void SetFiles(string[]? files, FileSource source = FileSource.Dropped)
        {
            _stagedPaths = files;
            _backgroundPath = null; // Will be determined later
            _currentSource = files is { Length: > 0 } ? source : FileSource.None;
            UpdateDisplayText();
            IsConversionEnabled = _stagedPaths is { Length: > 0 };
            LoadPreviewIfAvailable();
            FilesDropped?.Invoke(this, _stagedPaths ?? []);
        }

        private void LoadPreviewIfAvailable()
        {
            if (_stagedPaths is { Length: > 0 } && _previewDual != null)
            {
                try
                {
                    _previewDual.LoadPreview(_stagedPaths[0]);
                    if (string.IsNullOrEmpty(_backgroundPath))
                    {
                        try
                        {
                            var beatmap = BeatmapDecoder.Decode(_stagedPaths[0]);
                            if (beatmap != null && !string.IsNullOrWhiteSpace(beatmap.EventsSection.BackgroundImage))
                            {
                                _backgroundPath = Path.Combine(Path.GetDirectoryName(_stagedPaths[0])!, beatmap.EventsSection.BackgroundImage);
                            }
                        }
                        catch
                        {
                            // Ignore decoding errors
                        }
                    }
                    if (!string.IsNullOrEmpty(_backgroundPath))
                    {
                        _previewDual.LoadBackgroundBrush(_backgroundPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load preview for {_stagedPaths[0]}: {ex.Message}");
                }
            }
        }

        public void ConvertFiles()
        {
            if (_stagedPaths is { Length: > 0 } && _fileDispatcher != null && _getActiveTabTag != null)
            {
                var activeTag = _getActiveTabTag();
                _fileDispatcher.ConvertFiles(_stagedPaths, activeTag);
            }
        }

        public List<string> CollectOsuFiles(string[] items)
        {
            var osuFiles = new List<string>();
            foreach (var item in items)
            {
                if (File.Exists(item) && Path.GetExtension(item).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    osuFiles.Add(item);
                }
                else if (Directory.Exists(item))
                {
                    try
                    {
                        var found = Directory.GetFiles(item, "*.osu", SearchOption.AllDirectories);
                        osuFiles.AddRange(found);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error accessing directory {item}: {ex.Message}");
                    }
                }
            }
            return osuFiles;
        }

        private void UpdateDisplayText()
        {
            if (_stagedPaths == null || _stagedPaths.Length == 0)
            {
                DisplayText = _dropHintLocalized.Value;
                _currentSource = FileSource.None;
            }
            else
            {
                string prefix = _currentSource switch
                {
                    FileSource.Dropped => "[拖入] ",
                    FileSource.Listened => "[监听] ",
                    _ => ""
                };
                DisplayText = prefix + string.Format(_dropFilesHintLocalized.Value, _stagedPaths.Length);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}