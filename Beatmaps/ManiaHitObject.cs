using System.Numerics;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Beatmaps.Objects.Mania;
using OsuParsers.Enums.Beatmaps;

#nullable disable
namespace krrTools.Beatmaps
{
    public class ManiaHitObject
    {
        public Vector2 Position { get; set; }
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public int Index { get; set; }

        public bool IsHold { get; set; }
        public Extras Extras { get; set; } = new Extras();
        public HitSoundType HitSound { get; set; }
    }
}
