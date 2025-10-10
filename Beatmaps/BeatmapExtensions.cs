using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Beatmaps
{
    public static class BeatmapExtensions
    {
        public static double GetBPM(this Beatmap? beatmap, bool asMs = false)
        {
            if (beatmap == null)
                return 180;

            var tp = beatmap.TimingPoints
                .Where(p => p.BeatLength > 0)
                .OrderBy(p => p.Offset)
                .ToList();
            if (tp.Count == 0)
                return 0;

            double maxDuration = -1;
            var longestPoint = tp[0];
            for (var i = 0; i < tp.Count - 1; i++)
            {
                double duration = tp[i + 1].Offset - tp[i].Offset;
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                    longestPoint = tp[i];
                }
            }

            var bpm = Math.Round(60000 / longestPoint.BeatLength, 2);
            return asMs ? 60000.0 / Math.Max(1.0, bpm) : bpm;
        }

        public static (NoteMatrix, List<int>) BuildMatrix(this Beatmap beatmap)
        {
            var cs = (int)beatmap.DifficultySection.CircleSize;
            var timePoints = new SortedSet<int>();
            foreach (var hitObject in beatmap.HitObjects)
            {
                timePoints.Add(hitObject.StartTime);
                if (hitObject.EndTime > 0) timePoints.Add(hitObject.EndTime);
            }

            var timeAxis = timePoints.ToList();
            var h = timeAxis.Count;
            var a = cs;

            var matrix = new NoteMatrix(h, a);
            // NoteMatrix already initialized to Empty (-1)

            var timeToRow = timeAxis
                .Select((time, index) => new { time, index })
                .ToDictionary(x => x.time, x => x.index);

            for (var i = 0; i < beatmap.HitObjects.Count; i++)
            {
                var hitObject = beatmap.HitObjects[i];
                var column = positionXToColumn(cs, (int)hitObject.Position.X);
                var startRow = timeToRow[hitObject.StartTime];

                matrix[startRow, column] = i;

                if (hitObject.EndTime > 0)
                {
                    var endRow = timeToRow[hitObject.EndTime];

                    for (var row = startRow + 1; row <= endRow; row++) matrix[row, column] = NoteMatrix.HoldBody;
                }
            }

            return (matrix, timeAxis);
        }

        private static int positionXToColumn(int CS, int X)
        {
            var column = (int)Math.Floor(X * (double)CS / 512);
            return column;
        }

        private static int columnToPositionX(int CS, int column)
        {
            // int set_x = ((column - 1) * 512 / CS) + (256 / CS); // 不要删
            var x = (int)Math.Floor((column + 0.5) * (512.0 / CS));
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
            var artist = beatmap.MetadataSection.Artist ?? "";
            var title = beatmap.MetadataSection.Title ?? "";
            var creator = beatmap.MetadataSection.Creator ?? "";
            var version = beatmap.MetadataSection.Version ?? "";

            // 使用正则表达式移除所有非法字符
            var invalidCharsPattern = $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]";
            artist = Regex.Replace(artist, invalidCharsPattern, "");
            title = Regex.Replace(title, invalidCharsPattern, "");
            creator = Regex.Replace(creator, invalidCharsPattern, "");
            version = Regex.Replace(version, invalidCharsPattern, "");
            var FileName = isPreview == true
                ? $"{title} // {version}"
                : $"{artist} - {title} ({creator}) [{version}]";

            var remainder = 250 - beatmap.OriginalFilePath.Length;

            if (FileName.Length > remainder) FileName = FileName.Substring(0, remainder) + "...";

            return FileName;
        }

        public static Dictionary<double, double> GetBeatLengthList(this Beatmap beatmap)
        {
            var tp = beatmap.TimingPoints
                .Where(p => p.BeatLength > 0)
                .OrderBy(p => p.Offset)
                .ToList();
            if (tp.Count == 0)
                return new Dictionary<double, double>();

            var beatLengthDict = new Dictionary<double, double>();
            foreach (var timingPoint in tp) beatLengthDict[timingPoint.Offset] = timingPoint.BeatLength;

            return beatLengthDict;
        }

        public static Beatmap GetManiaBeatmap(this Beatmap beatmap, string? path = null)
        {
            if (beatmap == null)
                throw new InvalidDataException("GetManiaBeatmap为空");

            if (path != null)
            {
                beatmap.OriginalFilePath = path;

                if (!File.Exists(path))
                    throw new FileNotFoundException($"文件未找到: {path}");

                if (Path.GetExtension(path).ToLower() != ".osu")
                    throw new ArgumentException("文件扩展名必须为.osu");
            }

            if (beatmap.GeneralSection.ModeId != 3)
                throw new InvalidDataException("谱面模式不是Mania");

            if (beatmap.HitObjects.Count == 0)
                Logger.WriteLine(LogLevel.Warning, "GetManiaBeatmap读取文件警告: 谱面为空");

            return beatmap;
        }

        public static HitObject CopyHitObjectByPositionX(HitObject hitObject, int positionX)
        {
            // 复制所有基本属性
            var newPosition = new Vector2(positionX, hitObject.Position.Y);
            var startTime = hitObject.StartTime;
            var endTime = hitObject.EndTime;
            var hitSound = hitObject.HitSound;

            // 正确复制Extras，确保不为null
            var newExtras = hitObject.Extras != null
                ? new Extras(
                    hitObject.Extras.SampleSet,
                    hitObject.Extras.AdditionSet,
                    hitObject.Extras.CustomIndex,
                    hitObject.Extras.Volume,
                    hitObject.Extras.SampleFileName
                )
                : new Extras();

            // 保持原始对象的其他属性
            var isNewCombo = hitObject.IsNewCombo;
            var comboOffset = hitObject.ComboOffset;

            // 根据WriteHelper.TypeByte的逻辑来判断对象类型
            // 检查是否是长音符（mania模式下）
            var isHoldNote = hitObject.EndTime > hitObject.StartTime;

            if (isHoldNote)
                // 创建ManiaHoldNote对象
                return new OsuParsers.Beatmaps.Objects.Mania.ManiaHoldNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
            else
                // 创建普通HitObject对象
                return new OsuParsers.Beatmaps.Objects.Mania.ManiaNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
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