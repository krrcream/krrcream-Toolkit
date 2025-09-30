
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace krrTools.Tools.KrrLV;
public class KRRLVAnalysisItem : ObservableObject
{
        
    private readonly ProcessingWindow _processingWindow = new ProcessingWindow();
    private int _processedCount;
    private int _totalCount;
        
    public string? FileName { get; set; }

    public string? FilePath { get; init; }

    public string? Status { get; set; }

    public string? Diff { get; set; }
        
    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? Creator { get; set; }

    public double Keys { get; set; }

    public string? BPM { get; set; }
        
    public double OD { get; set; }

    public double HP { get; set; }

    public double LNPercent { get; set; }

    public double BeatmapID { get; set; }

    public double BeatmapSetID { get; set; }
        
    public double XxySR { get; set; }
    
    public double KrrLV { get; set; }
        
    public double YlsLV { get; set; }
        
    public int ProcessedCount
    {
        get => _processedCount;
        set
        {
            _processedCount = value;
            OnPropertyChanged();
            // TODO: 改成WPF UI的进度条
            Application.Current.Dispatcher.Invoke(() =>
            {
                _processingWindow.UpdateProgress(value, _totalCount);
            });
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        set
        {
            _totalCount = value;
            OnPropertyChanged();
        }
    }
}