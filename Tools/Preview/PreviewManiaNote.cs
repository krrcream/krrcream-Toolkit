using System.IO;
using System.Text;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview
{
    public abstract class PreviewManiaNote
    {
        public static Beatmap BuiltInSampleStream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ManiaData));
            return BeatmapDecoder.Decode(stream);
        }
    
        private const string ManiaData = @"
osu file format v14

[General]
SampleSet: Normal
StackLeniency: 0.7
Mode: 3

[Metadata]
Title:Built-in Sample
TitleUnicode:Built-in Sample
Artist:Preview
ArtistUnicode:Preview
Version:5k

[Events]
//Background and Video events
0,0,""bg.jpg"",0,0

[Difficulty]
HPDrainRate:3
CircleSize:5
OverallDifficulty:8
ApproachRate:8
SliderMultiplier:3.59999990463257
SliderTickRate:2

[TimingPoints]
24,352.941176470588,4,1,1,100,1,0
6376,-50,4,1,1,100,0,0

[HitObjects]
51,192,24,1,0,0:0:0:0:
153,192,200,1,0,0:0:0:0:
358,192,376,1,0,0:0:0:0:
460,192,553,1,0,0:0:0:0:
460,192,729,128,0,1435:0:0:0:0:
358,192,906,128,0,1612:0:0:0:0:
256,192,1082,128,0,1788:0:0:0:0:
153,192,1259,128,0,1965:0:0:0:0:
51,192,1435,128,0,2141:0:0:0:0:
51,192,2318,1,12,0:0:0:0:
153,192,2318,1,4,0:0:0:0:
256,192,2318,1,6,0:0:0:0:
358,192,2318,1,14,0:0:0:0:
460,192,2318,1,0,0:0:0:0:
";
    }
}