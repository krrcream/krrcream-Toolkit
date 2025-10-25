using System.Numerics;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Enums.Beatmaps;

namespace krrTools.Beatmaps
{
    public interface IHitObject
    {
        Vector2 Position { get; set; }

        int StartTime { get; set; }

        int EndTime { get; set; }

        int Index { get; set; }

        bool IsHold { get; }

        Extras Extras { get; set; }

        HitSoundType HitSound { get; set; }
    }
}
