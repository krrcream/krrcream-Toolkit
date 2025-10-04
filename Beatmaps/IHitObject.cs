namespace krrTools.Beatmaps;

public interface IHitObject
{
    float StartTimeInMs { get; set; }
    float EndTimeInMs { get; set; }

    int StartTime { get; set; }
    int EndTime { get; set; }
    
    int Index { get; set; }
    int Column { get; set; }

    bool IsHold { get; }
}