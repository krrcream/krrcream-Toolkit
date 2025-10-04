namespace krrTools.Beatmaps;

public class ManiaHitObject : IHitObject
{
    public float StartTimeInMs { get; set; }
    public float EndTimeInMs { get; set; }
    public int StartTime { get; set; }
    public int EndTime { get; set; }
    public int Index { get; set; }
    public int Column { get; set; }
    public bool IsHold { get; set; }
}