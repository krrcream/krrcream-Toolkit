using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Beatmaps.Objects.Mania;
using OsuParsers.Enums.Beatmaps;

namespace krrTools.Beatmaps
{
    public static class BeatmapExtensions
    {
        public static double GetBPM(this Beatmap? beatmap, bool asMs = false)
        {
            if (beatmap == null)
                return 180;

            List<TimingPoint> tp = beatmap.TimingPoints
                                          .Where(p => p.BeatLength > 0)
                                          .OrderBy(p => p.Offset)
                                          .ToList();
            if (tp.Count == 0)
                return 0;

            double maxDuration = -1;
            TimingPoint longestPoint = tp[0];

            for (int i = 0; i < tp.Count - 1; i++)
            {
                double duration = tp[i + 1].Offset - tp[i].Offset;

                if (duration > maxDuration)
                {
                    maxDuration = duration;
                    longestPoint = tp[i];
                }
            }

            double bpm = Math.Round(60000 / longestPoint.BeatLength, 2);
            return asMs ? 60000.0 / Math.Max(1.0, bpm) : bpm;
        }

        public static string GetBPMDisplay(this Beatmap beatmap)
        {
            double bpm = beatmap.MainBPM;
            double bpmMax = beatmap.MaxBPM;
            double bpmMin = beatmap.MinBPM;

            string BPMFormat = string.Format(CultureInfo.InvariantCulture, "{0:F0}({1:F0} - {2:F0})", bpm, bpmMin, bpmMax);

            return BPMFormat;
        }

        public static (NoteMatrix, List<int>) BuildMatrix(this Beatmap beatmap)
        {
            int cs = (int)beatmap.DifficultySection.CircleSize;
            var timePoints = new SortedSet<int>();

            foreach (HitObject? hitObject in beatmap.HitObjects)
            {
                timePoints.Add(hitObject.StartTime);
                if (hitObject.EndTime > 0) timePoints.Add(hitObject.EndTime);
            }

            List<int> timeAxis = timePoints.ToList();
            int h = timeAxis.Count;
            int a = cs;

            var matrix = new NoteMatrix(h, a);
            // NoteMatrix already initialized to Empty (-1)

            Dictionary<int, int> timeToRow = timeAxis
                                            .Select((time, index) => new { time, index })
                                            .ToDictionary(x => x.time, x => x.index);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                HitObject? hitObject = beatmap.HitObjects[i];
                int column = positionXToColumn(cs, (int)hitObject.Position.X);
                int startRow = timeToRow[hitObject.StartTime];

                matrix[startRow, column] = i;

                if (hitObject.EndTime > 0)
                {
                    int endRow = timeToRow[hitObject.EndTime];

                    for (int row = startRow + 1; row <= endRow; row++) matrix[row, column] = NoteMatrix.HoldBody;
                }
            }

            return (matrix, timeAxis);
        }

        private static int positionXToColumn(int CS, int X)
        {
            int column = (int)Math.Floor(X * (double)CS / 512);
            return column;
        }

        private static int columnToPositionX(int CS, int column)
        {
            int x = (int)Math.Floor((column + 0.5) * (512.0 / CS));
            return x;
        }

        public static double GetLNPercent(this Beatmap beatmap)
        {
            double noteCount = beatmap.HitObjects.Count;
            if (noteCount == 0) return 0;

            double LNCountCount = beatmap.HitObjects.Count(hitObject => hitObject.EndTime > hitObject.StartTime);
            return LNCountCount / noteCount * 100;
        }

        public static string GetOutputOsuFileName(this Beatmap beatmap, bool? isPreview = null)
        {
            // 清理文件名中的非法字符
            string artist = beatmap.MetadataSection.Artist ?? "";
            string title = beatmap.MetadataSection.Title ?? "";
            string creator = beatmap.MetadataSection.Creator ?? "";
            string version = beatmap.MetadataSection.Version ?? "";

            // 使用正则表达式移除所有非法字符
            string invalidCharsPattern = $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]";
            artist = Regex.Replace(artist, invalidCharsPattern, "");
            title = Regex.Replace(title, invalidCharsPattern, "");
            creator = Regex.Replace(creator, invalidCharsPattern, "");
            version = Regex.Replace(version, invalidCharsPattern, "");

            // 限制artist和title长度，官方要求81，但是为了给标签腾出安全位置 81 - 17 = 64
            if (artist.Length > 64)
            {
                artist = artist.Substring(0, 61) + "...";
                beatmap.MetadataSection.Artist = artist;
            }

            if (title.Length > 64)
            {
                title = title.Substring(0, 61) + "...";
                beatmap.MetadataSection.Title = title;
            }

            string FileName = isPreview == true
                                  ? $"{title} // {version}"
                                  : $"{artist} - {title} ({creator}) [{version}]";

            return FileName + ".osu";
        }

        public static Dictionary<double, double> GetBeatLengthList(this Beatmap beatmap)
        {
            List<TimingPoint> tp = beatmap.TimingPoints
                                          .Where(p => p.BeatLength > 0)
                                          .OrderBy(p => p.Offset)
                                          .ToList();
            if (tp.Count == 0)
                return new Dictionary<double, double>();

            var beatLengthDict = new Dictionary<double, double>();
            foreach (TimingPoint timingPoint in tp) beatLengthDict[timingPoint.Offset] = timingPoint.BeatLength;

            return beatLengthDict;
        }

        public static Beatmap? GetManiaBeatmap(this Beatmap beatmap)
        {
            if (beatmap.GeneralSection.ModeId != 3) return null;

            if (beatmap.HitObjects.Count == 0) return null;

            return beatmap;
        }

        public static HitObject CopyHitObjectByPositionX(HitObject hitObject, int positionX)
        {
            // 复制所有基本属性
            var newPosition = new Vector2(positionX, hitObject.Position.Y);
            int startTime = hitObject.StartTime;
            int endTime = hitObject.EndTime;
            HitSoundType hitSound = hitObject.HitSound;

            // 正确复制Extras，确保不为null
            Extras newExtras = hitObject.Extras != null
                                   ? new Extras(
                                       hitObject.Extras.SampleSet,
                                       hitObject.Extras.AdditionSet,
                                       hitObject.Extras.CustomIndex,
                                       hitObject.Extras.Volume,
                                       hitObject.Extras.SampleFileName
                                   )
                                   : new Extras();

            // 保持原始对象的其他属性
            bool isNewCombo = hitObject.IsNewCombo;
            int comboOffset = hitObject.ComboOffset;

            // 根据WriteHelper.TypeByte的逻辑来判断对象类型
            // 检查是否是长音符（mania模式下）
            bool isHoldNote = hitObject.EndTime > hitObject.StartTime;

            if (isHoldNote)
                // 创建ManiaHoldNote对象
            {
                return new ManiaHoldNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
            }
            else
                // 创建普通HitObject对象
            {
                return new ManiaNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
            }
        }

        public static void SortHitObjects(this Beatmap beatmap)
        {
            beatmap.HitObjects.Sort((a, b) =>
            {
                if (a.StartTime == b.StartTime)
                    return a.Position.X.CompareTo(b.Position.X);
                else
                    return a.StartTime.CompareTo(b.StartTime);
            });
        }
    }
}
