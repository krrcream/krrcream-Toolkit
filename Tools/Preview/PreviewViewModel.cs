using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview;

public class PreviewViewModel : INotifyPropertyChanged
{
    private FrameworkElement? _originalVisual;
    private FrameworkElement? _convertedVisual;
    private string _title = string.Empty;
    private string? _beatmapPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public FrameworkElement? OriginalVisual
    {
        get => _originalVisual;
        private set
        {
            if (_originalVisual != value)
            {
                _originalVisual = value;
                OnPropertyChanged();
            }
        }
    }

    public FrameworkElement? ConvertedVisual
    {
        get => _convertedVisual;
        private set
        {
            if (_convertedVisual != value)
            {
                _convertedVisual = value;
                OnPropertyChanged();
            }
        }
    }

    public string Title
    {
        get => _title;
        private set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public IPreviewProcessor? Processor { get; private set; }

    public void LoadFromPath(string path)
    {
        _beatmapPath = path;
        ExecuteRefresh();
    }

    public void LoadBuiltInSample()
    {
        _beatmapPath = null;
        ExecuteRefresh();
    }

    public void SetProcessor(IPreviewProcessor? processor)
    {
        var oldProcessor = Processor;
        Processor = processor;

        if (oldProcessor != processor) 
        {
            ExecuteRefresh();
        }
    }

    public void TriggerRefresh()
    {
        ExecuteRefresh();
    }

    internal void ExecuteRefresh()
    {
        Beatmap? beatmap = null;
        if (!string.IsNullOrEmpty(_beatmapPath))
        {
            try
            {
                beatmap = BeatmapDecoder.Decode(_beatmapPath);
            }
            catch
            {
                // Ignore
            }
        }
        
        if (beatmap == null)
        {
            beatmap = PreviewManiaNote.BuiltInSampleStream();
        }

        var originalVisual = Processor?.BuildOriginalVisual(beatmap);
        OriginalVisual = originalVisual;

        if (Processor != null)
        {
            var convertedVisual = Processor.BuildConvertedVisual(beatmap);
            ConvertedVisual = convertedVisual;
        }
        else
        {
            ConvertedVisual = null;
        }

        UpdateTitle(beatmap);
    }

    public void Reset()
    {
        _beatmapPath = null;
        OriginalVisual = null;
        ConvertedVisual = null;
        Title = string.Empty;
    }

    private void UpdateTitle(Beatmap beatmap)
    {
        if (beatmap.MetadataSection.Title == "Built-in Sample")
        {
            Title = "Built-in Sample";
        }
        else
        {
            var name = beatmap.GetOutputOsuFileName(true);
            Title = $"DIFF: {name}";
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}