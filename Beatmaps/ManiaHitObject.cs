using System.Numerics;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Beatmaps.Objects.Mania;
using OsuParsers.Enums.Beatmaps;

#nullable disable
namespace krrTools.Beatmaps
{
    public class ManiaHitObject : IHitObject
    {
        public Vector2 Position { get; set; }
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public int Index { get; set; }

        public bool IsHold { get; set; }
        public Extras Extras { get; set; } = new Extras();
        public HitSoundType HitSound { get; set; }

        public void InitFrom(HitObject ho)
        {
            Position = ho.Position;
            StartTime = ho.StartTime;
            EndTime = ho.EndTime;
            Index = (int)ho.Position.X;
            IsHold = ho is ManiaHoldNote && ho.EndTime > ho.StartTime;
            Extras = ho.Extras;
            HitSound = ho.HitSound;
        }
    }
}
