using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Win32;

namespace krrTools.tools.LNTransformer;

// 未来弃用的文件
internal class OsuFileProcessor
{
    /// <summary>
    /// Return an OsuFileV14 parsed from a FileInfo.
    /// </summary>
    public static OsuFileV14 ReadFile(FileInfo file, Func<string, bool>? exitFunc = null)
    {
        return ReadSingleOsuFileInternal(file, exitFunc);
    }

    public static OsuFileV14 ReadFile(string fileName, Func<string, bool>? exitFunc = null)
    {
        return ReadSingleOsuFileInternal(new FileInfo(fileName), exitFunc);
    }

    /// <summary>
    /// Reads multiple osu files in parallel.
    /// </summary>
    public static List<OsuFileV14> ReadMultipleFiles(IEnumerable<string> filePaths, Func<string, bool>? exitFunc = null)
    {
        var results = new ConcurrentBag<OsuFileV14>();

        Parallel.ForEach(filePaths, filePath =>
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var osuFile = ReadSingleOsuFileInternal(fileInfo, exitFunc);
                if (!osuFile.IsEmpty) results.Add(osuFile);
            }
            catch (Exception ex)
            {
                // Library code should not show UI. Log and continue.
                Debug.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }
        });

        return results.ToList();
    }

    // Internal: parse from a sequence of lines (allows stream/text parsing)
    private static OsuFileV14 ParseFromLines(IEnumerable<string> lines, FileInfo? sourceFile = null,
        Func<string, bool>? exitFunc = null)
    {
        var obj = new List<ManiaHitObject>();
        var points = new List<TimingPoint>();
        var colours = new List<Colour>();
        var meta = new Metadata();
        var general = new General();
        var events = new Event();

        var Events = false;
        var TimingPoints = false;
        var Colours = false;
        var HitObjects = false;

        foreach (var raw in lines)
        {
            var lineContent = raw;
            var line = lineContent.Trim();

            if (exitFunc != null && exitFunc(line)) return OsuFileV14.Empty;

            if (line == string.Empty) continue;

            if (line.StartsWith("osu file format"))
            {
                var version = line.Split(' ').Last().Trim();
                if (version != "v14") return OsuFileV14.Empty;
                continue;
            }

            var colonIndex = line.IndexOf(':');
            var key = string.Empty;
            var value = string.Empty;
            if (colonIndex != -1)
            {
                key = line[..colonIndex].Trim();
                value = line[(colonIndex + 1)..].Trim();
            }

            switch (key)
            {
                case "AudioFilename":
                    general.AudioFilename = value;
                    continue;
                case "AudioLeadIn":
                    general.AudioLeadIn = value;
                    continue;
                case "PreviewTime":
                    general.PreviewTime = value;
                    continue;
                case "Countdown":
                    general.Countdown = value;
                    continue;
                case "SampleSet":
                    general.SampleSet = value;
                    continue;
                case "StackLeniency":
                    general.StackLeniency = value;
                    continue;
                case "Mode":
                    general.Mode = value;
                    continue;
                case "LetterboxInBreaks":
                    general.LetterboxInBreaks = value;
                    continue;
                case "SpecialStyle":
                    general.SpecialStyle = value;
                    continue;
                case "WidescreenStoryboard":
                    general.WidescreenStoryboard = value;
                    continue;
            }

            if (line.StartsWith("TitleUnicode"))
            {
                meta.TitleUnicode = value;
                continue;
            }

            if (line.StartsWith("ArtistUnicode"))
            {
                meta.ArtistUnicode = value;
                continue;
            }

            switch (key)
            {
                case "Title":
                    meta.Title = value;
                    continue;
                case "Artist":
                    meta.Artist = value;
                    continue;
                case "Creator":
                    meta.Creator = value;
                    continue;
                case "Version":
                    meta.Difficulty = value;
                    continue;
                case "Source":
                    meta.Source = value;
                    continue;
                case "Tags":
                    meta.Tags = value;
                    continue;
                case "BeatmapID":
                    meta.BeatmapID = value;
                    continue;
                case "BeatmapSetID":
                    meta.BeatmapSetID = value;
                    continue;
            }

            switch (key)
            {
                case "HPDrainRate":
                    general.HPDrainRate = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                case "CircleSize":
                    general.CircleSize = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                case "OverallDifficulty":
                    general.OverallDifficulty = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                case "ApproachRate":
                    general.ApproachRate = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                case "SliderMultiplier":
                    general.SliderMultiplier = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                case "SliderTickRate":
                    general.SliderTickRate = double.Parse(value, CultureInfo.InvariantCulture);
                    continue;
            }

            if (line.StartsWith("[Events]"))
            {
                Events = true;
                continue;
            }

            if (line.StartsWith("[TimingPoints]"))
            {
                Events = false;
                TimingPoints = true;
                continue;
            }

            if (line.StartsWith("[Colours]"))
            {
                Events = false;
                TimingPoints = false;
                Colours = true;
                continue;
            }

            if (line.StartsWith("[HitObjects]"))
            {
                Events = false;
                TimingPoints = false;
                Colours = false;
                HitObjects = true;
                continue;
            }

            if (Events)
            {
                events.eventString += line + Environment.NewLine;
                continue;
            }

            if (TimingPoints)
            {
                var pointElements = line.Split(',');
                if (pointElements.Length >= 8)
                    points.Add(new TimingPoint(
                        int.Parse(pointElements[0], CultureInfo.InvariantCulture),
                        double.Parse(pointElements[1], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[2], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[3], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[4], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[5], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[6], CultureInfo.InvariantCulture),
                        int.Parse(pointElements[7], CultureInfo.InvariantCulture)));
                continue;
            }

            if (Colours)
            {
                var colourElements = line.Split(':');
                if (colourElements.Length >= 2)
                {
                    var name = colourElements[0].Trim();
                    var colour = colourElements[1].Trim().Split(',');
                    if (colour.Length >= 3)
                        colours.Add(new Colour(name, byte.Parse(colour[0], CultureInfo.InvariantCulture),
                            byte.Parse(colour[1], CultureInfo.InvariantCulture),
                            byte.Parse(colour[2], CultureInfo.InvariantCulture)));
                }

                continue;
            }

            if (!HitObjects) continue;

            var elements = line.Split(',');
            if (elements.Length < 6) continue;
            var x = (int)double.Parse(elements[0], CultureInfo.InvariantCulture);
            var y = (int)double.Parse(elements[1], CultureInfo.InvariantCulture);
            var time = (int)double.Parse(elements[2], CultureInfo.InvariantCulture);
            var type = (int)double.Parse(elements[3], CultureInfo.InvariantCulture);
            var hitSound = (int)double.Parse(elements[4], CultureInfo.InvariantCulture);
            var hitSample = elements[5];
            obj.Add(new ManiaHitObject(x, y, (int)general.CircleSize, time, type, hitSound, hitSample));
        }

        return new OsuFileV14(obj, points, colours, meta, general, events, sourceFile);
    }

    private static OsuFileV14 ReadSingleOsuFileInternal(FileInfo file, Func<string, bool>? exitFunc = null)
    {
        // Use File.ReadLines for memory efficiency and pass into the common parser
        return ParseFromLines(File.ReadLines(file.FullName), file, exitFunc);
    }

    /// <summary>
    /// Parse from a Stream (no UI, no filesystem required).
    /// </summary>
    public static OsuFileV14 ReadFromStream(Stream stream, Func<string, bool>? exitFunc = null)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return ParseFromLines(lines, null, exitFunc);
    }

    public static void WriteOsuFile(OsuFileV14 osu, string path = "")
    {
        if (!string.IsNullOrEmpty(path))
        {
            osu.WriteFile(path);
            return;
        }

        osu.WriteFile(osu.path);
    }

    /// <summary>
    /// Write to provided stream instead of a file.
    /// </summary>
    public static void WriteToStream(OsuFileV14 osu, Stream stream)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);
        osu.SerializeTo(writer);
        writer.Flush();
    }

    public string GetOsuPath()
    {
        try
        {
            var path = "\\osu!\\shell\\open\\command";
            var keyname = "";
            var regkey = Registry.ClassesRoot;
            var regsubkey = regkey.OpenSubKey(path, false);
            if (regsubkey == null) return "Failed";
            try
            {
                var regvaluekind = regsubkey.GetValueKind(keyname);
                var result = regsubkey.GetValue(keyname);
                if (regvaluekind == RegistryValueKind.String && result != null)
                {
                    var destination = result.ToString();
                    if (string.IsNullOrEmpty(destination)) return "Failed";
                    return destination.Substring(1, destination.Length - 16);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetOsuPath registry read error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GetOsuPath error: " + ex.Message);
            return "Failed";
        }

        return "Failed";
    }
}

public class OsuFileV14
{
    public List<ManiaHitObject> HitObjects;
    public List<TimingPoint> TimingPoints;
    public List<Colour> Colours = [];
    public Metadata Metadata;
    public General General;
    public Event Events = new();
    public FileInfo? OriginalFile;
    public string path = string.Empty;
    public const string FileExtension = ".osu";
    public readonly char[] ForbiddenChar = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
    public double StarRating;
    public bool IsEmpty;

    public static readonly Dictionary<int, List<int>> KeyX = new()
    {
        { 3, new List<int> { 85, 256, 426 } },
        { 4, new List<int> { 64, 192, 320, 448 } },
        { 5, new List<int> { 51, 153, 256, 358, 460 } },
        { 6, new List<int> { 42, 128, 213, 298, 384, 469 } },
        { 7, new List<int> { 36, 109, 182, 256, 329, 402, 475 } },
        { 8, new List<int> { 32, 96, 160, 224, 288, 352, 416, 480 } },
        { 9, new List<int> { 28, 85, 142, 199, 256, 312, 369, 426, 483 } },
        { 10, new List<int> { 25, 76, 128, 179, 230, 281, 332, 384, 435, 486 } },
        { 12, new List<int>() },
        { 14, new List<int>() },
        { 16, new List<int>() },
        { 18, new List<int>() }
    };

    public string FileName
    {
        get
        {
            try
            {
                return string.Concat(OriginalName.Where(c => !ForbiddenChar.Contains(c)));
            }
            catch
            {
                return "";
            }
        }
    }

    public string OriginalName => (Metadata.Artist ?? string.Empty) + " - " + (Metadata.Title ?? string.Empty) + " (" +
                                  (Metadata.Creator ?? string.Empty) + ") " + "[" +
                                  (Metadata.Difficulty ?? string.Empty) + "]";

    public static OsuFileV14 Empty
    {
        get
        {
            var osu = new OsuFileV14(new List<ManiaHitObject>(), new List<TimingPoint>(), new List<Colour>(),
                new Metadata(), new General(), new Event(), null)
            {
                IsEmpty = true
            };
            return osu;
        }
    }

    public OsuFileV14(List<ManiaHitObject> hitObjects, List<TimingPoint> timingPoints, List<Colour> colours,
        Metadata metadata, General general, Event events, FileInfo? file)
    {
            HitObjects = hitObjects;
            TimingPoints = timingPoints;
            Colours = colours;
            Metadata = metadata;
            General = general;
            Events = events;
            OriginalFile = file;
            for (int i = 0; i < hitObjects.Count; i++)
            {
                var obj = HitObjects[i];
                obj.CircleSize = (int)general.CircleSize;
                obj.actualColumn = (int)Math.Floor(obj.X * obj.CircleSize / 512.0);
                HitObjects[i] = obj;
            }

            if (file is not null)
            {
                path = file.DirectoryName ?? string.Empty;
                try
                {
                    StarRating = 0;
                }
                catch
                {
                    StarRating = 0;
                }
            }
            else
            {
                StarRating = 0;
            }
    }

    public OsuFileV14(string FileName)
    {
        var osu = OsuFileProcessor.ReadFile(FileName);
            HitObjects = osu.HitObjects;
            TimingPoints = osu.TimingPoints;
            Metadata = osu.Metadata;
            General = osu.General;
            path = Path.GetDirectoryName(FileName) ?? string.Empty;
            OriginalFile = new FileInfo(FileName);
            for (int i = 0; i < HitObjects.Count; i++)
        {
            var obj = HitObjects[i];
            obj.CircleSize = (int)osu.General.CircleSize;
            obj.actualColumn = (int)Math.Floor(obj.X * obj.CircleSize / 512.0);
            HitObjects[i] = obj;
        }

        StarRating = 0;
    }

    public TimingPoint TimingPointAt(double time)
    {
        if (TimingPoints.Count == 0) throw new InvalidOperationException("TimingPoints is empty.");
        var RedPoints = TimingPoints.Where(tp => tp.BeatLength >= 0).OrderBy(tp => tp.Time).ToList();
        if (RedPoints.Count == 1) return RedPoints[0];
        for (var i = 0; i < RedPoints.Count - 1; i++)
            if (RedPoints[i].Time <= time && RedPoints[i + 1].Time > time)
                return RedPoints[i];
        return RedPoints[^1];
    }

    public void ManiaToKeys(int Keys)
    {
        General.CircleSize = Keys;
        for (var i = 0; i < HitObjects.Count; i++)
        {
            var obj = HitObjects[i];
            obj.X = KeyX[Keys][obj.Column];
            obj.CircleSize = Keys;
            HitObjects[i] = obj;
        }
    }

    public List<ManiaHitObject> SelectColumn(int Column)
    {
        return HitObjects.Where(obj => obj.Column == Column).ToList();
    }

    public List<ManiaHitObject> SelectManyColumn(int[] Columns)
    {
        var list = new List<ManiaHitObject>();
        var i = 0;
        var ToCircleSize = Columns.Length;
        foreach (var column in Columns)
        {
            var range = SelectColumn(column);
            range.ForEach(o => o.X = KeyX[ToCircleSize][i]);
            range.ForEach(o => o.CircleSize = ToCircleSize);
            list.AddRange(range);
            i++;
        }

        return list;
    }

    public void RemoveManyColumn(int[] Columns)
    {
        var RemainColumns = Enumerable.Range(0, (int)General.CircleSize).Where(c => !Columns.Contains(c)).ToArray();
        HitObjects = SelectManyColumn(RemainColumns);
        General.CircleSize -= Columns.Length;
    }

    public OsuFileV14 Copy()
    {
        return new OsuFileV14([..HitObjects.Select(obj => obj)],
            [..TimingPoints.Select(pt => pt)], [..Colours.Select(col => col)],
            Metadata, General, Events, OriginalFile);
    }

    // Serialize to a TextWriter. Used by both file and stream writers.
    internal void SerializeTo(TextWriter writer)
    {
        // [General]
        writer.WriteLine("osu file format " + General.Version + Environment.NewLine);
        writer.WriteLine("[General]");
        writer.WriteLine("AudioFilename:" + General.AudioFilename);
        writer.WriteLine("AudioLeadIn:" + General.AudioLeadIn);
        writer.WriteLine("PreviewTime:" + General.PreviewTime);
        writer.WriteLine("Countdown:" + General.Countdown);
        writer.WriteLine("SampleSet:" + General.SampleSet);
        writer.WriteLine("StackLeniency:" + General.StackLeniency);
        writer.WriteLine("Mode:" + General.Mode);
        writer.WriteLine("LetterboxInBreaks:" + General.LetterboxInBreaks);
        writer.WriteLine("SpecialStyle:" + General.SpecialStyle);
        writer.WriteLine("WidescreenStoryboard:" + General.WidescreenStoryboard);
        writer.WriteLine();

        // [Metadata]
        writer.WriteLine("[Metadata]");
        writer.WriteLine("Title:" + Metadata.Title);
        writer.WriteLine("TitleUnicode:" + Metadata.TitleUnicode);
        writer.WriteLine("Artist:" + Metadata.Artist);
        writer.WriteLine("ArtistUnicode:" + Metadata.ArtistUnicode);
        writer.WriteLine("Creator:" + Metadata.Creator);
        writer.WriteLine("Version:" + Metadata.Difficulty);
        writer.WriteLine("Source:" + Metadata.Source);
        writer.WriteLine("Tags:" + Metadata.Tags);
        writer.WriteLine("BeatmapID:" + Metadata.BeatmapID);
        writer.WriteLine("BeatmapSetID:" + Metadata.BeatmapSetID);
        writer.WriteLine();

        // [Difficulty]
        writer.WriteLine("[Difficulty]");
        writer.WriteLine("HPDrainRate:" + General.HPDrainRate.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("CircleSize:" + General.CircleSize.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("OverallDifficulty:" + General.OverallDifficulty.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("ApproachRate:" + General.ApproachRate.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("SliderMultiplier:" + General.SliderMultiplier.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("SliderTickRate:" + General.SliderTickRate.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine();

        // [Events]
        writer.WriteLine("[Events]");
        writer.WriteLine(Events.eventString);

        // [TimingPoints]
        writer.WriteLine("[TimingPoints]");
        foreach (var point in TimingPoints)
            writer.WriteLine(string.Join(",",
                point.Time.ToString(CultureInfo.InvariantCulture),
                point.BeatLength.ToString(CultureInfo.InvariantCulture),
                point.Meter.ToString(CultureInfo.InvariantCulture),
                point.SampleSet.ToString(CultureInfo.InvariantCulture),
                point.SampleIndex.ToString(CultureInfo.InvariantCulture),
                point.Volume.ToString(CultureInfo.InvariantCulture),
                point.Uninherited.ToString(CultureInfo.InvariantCulture),
                point.Effects.ToString(CultureInfo.InvariantCulture)));
        writer.WriteLine();

        // [Colours]
        if (Colours.Count > 0)
        {
            writer.WriteLine("[Colours]");
            foreach (var colour in Colours)
                writer.WriteLine(colour.Name + ": " + colour.R + "," + colour.G + "," + colour.B + "," + colour.A);
        }

        writer.WriteLine();

        // [HitObjects]
        writer.WriteLine("[HitObjects]");
        foreach (var obj in HitObjects)
            writer.WriteLine(string.Join(",", obj.X, obj.Y, obj.StartTime, obj.Type, obj.HitSound, obj.HitSample));
    }

    public void WriteFile(string filePath = "")
    {
        StreamWriter writer;
        if (!string.IsNullOrEmpty(filePath))
        {
            writer = string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.InvariantCultureIgnoreCase) ? 
                new StreamWriter(filePath) 
                : 
                new StreamWriter(filePath + Path.DirectorySeparatorChar + FileName + FileExtension);
        }
        else
        {
            writer = new StreamWriter(path + Path.DirectorySeparatorChar + FileName + FileExtension);
        }

        SerializeTo(writer);
        writer.Close();
    }

    public void Recalculate()
    {
        // Ensure timing points are ordered
        TimingPoints = TimingPoints.OrderBy(tp => tp.Time).ToList();

        // Recompute CircleSize and actualColumn for hitobjects
        for (int i = 0; i < HitObjects.Count; i++)
        {
            var obj = HitObjects[i];
            obj.CircleSize = (int)General.CircleSize;
            obj.actualColumn = (int)Math.Floor(obj.X * obj.CircleSize / 512.0);
            HitObjects[i] = obj;
        }
    }
}

public struct TimingPoint(
    int time,
    double beatLength,
    int meter,
    int sampleSet,
    int sampleIndex,
    int volume,
    int uninherited,
    int effects)
{
    public int Time = time;
    public double BeatLength = beatLength;
    public int Meter = meter;
    public int SampleSet = sampleSet;
    public int SampleIndex = sampleIndex;
    public int Volume = volume;
    public int Uninherited = uninherited;
    public int Effects = effects;
}

public record struct ManiaHitObject
{
    public int X;
    public int Y;
    public int StartTime;
    public int Type;
    public int HitSound;
    public int CircleSize;
    public string HitSample;

    public int actualColumn;

    public int EndTime
    {
        get
        {
            try
            {
                if (Type != 128) return StartTime;
                var time = int.Parse(HitSample.Split(':')[0], CultureInfo.InvariantCulture);
                return time;
            }
            catch
            {
                return StartTime;
            }
        }
        set
        {
            if (value != StartTime)
            {
                if (Type != 128) Type = 128;
                if (HitSample.Split(':').Length == 5)
                {
                    HitSample = value + ":" + HitSample;
                }
                else
                {
                    var letter = string.Join(":", HitSample.Split(':').Skip(1));
                    HitSample = value + ":" + letter;
                }
            }
            else
            {
                if (Type == 128)
                {
                    Type = 1;
                    HitSample = string.Join(":", HitSample.Split(':').Skip(1));
                }
            }
        }
    }

    public bool IsLN => StartTime != EndTime || Type == 128;

    public int Column
    {
        get => actualColumn;
        set
        {
            actualColumn = value;
            X = OsuFileV14.KeyX[CircleSize][value];
        }
    }

    public ManiaHitObject(int x, int y, int circleSize, int startTime, int type, int hitSound = 0,
        string hitSample = "0:0:0:0:", int endTime = int.MinValue)
    {
        X = x;
        Y = y;
        StartTime = startTime;
        CircleSize = circleSize;
        actualColumn = (int)Math.Floor(x * circleSize / 512.0);
        Type = type;
        HitSound = hitSound;
        HitSample = hitSample;
        if (endTime != int.MinValue) EndTime = endTime;
    }

    public ManiaHitObject(int column, int circleSize, int startTime, int type, int hitSound = 0,
        string hitSample = "0:0:0:0:", int endTime = int.MinValue)
    {
        actualColumn = column;
        X = (int)((column + 0.5) * 512 / circleSize);
        Y = 192;
        StartTime = startTime;
        CircleSize = circleSize;
        Type = type;
        HitSound = hitSound;
        HitSample = hitSample;
        if (endTime != int.MinValue) EndTime = endTime;
    }

    public ManiaHitObject(int column, int circleSize, int startTime, int hitSound = 0, string hitSample = "0:0:0:0:",
        int endTime = int.MinValue)
    {
        actualColumn = column;
        X = (int)((column + 0.5) * 512 / circleSize);
        Y = 192;
        StartTime = startTime;
        CircleSize = circleSize;
        HitSound = hitSound;
        HitSample = hitSample;
        if (endTime != int.MinValue)
        {
            EndTime = endTime;
        }
        else
        {
            Type = 1;
            return;
        }

        Type = endTime != startTime ? 128 : 1;
    }

    public ManiaHitObject ToNote()
    {
        var note = this;
        note.EndTime = note.StartTime;
        return note;
    }

    public ManiaHitObject ToLongNote(double endTimeDouble)
    {
        var note = this;
        try
        {
            int endTime = (int)Math.Round(endTimeDouble);
            note.EndTime = endTime;
        }
        catch
        {
            note.EndTime = note.StartTime;
        }
        return note;
    }
}

public struct Event()
{
    public string eventString = string.Empty;
}

public struct Colour
{
    public string Name;
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Colour(string name, byte r, byte g, byte b)
    {
        Name = name;
        R = r;
        G = g;
        B = b;
        A = 255;
    }

    public Colour(string name, byte r, byte g, byte b, byte a)
    {
        Name = name;
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

public struct Metadata
{
    public string? Title;
    public string? TitleUnicode;
    public string? Artist;
    public string? ArtistUnicode;
    public string? Creator;
    public string? Difficulty;
    public string? Source;
    public string? Tags;
    public string? BeatmapID;
    public string? BeatmapSetID;

    public Metadata()
    {
        Title = string.Empty;
        TitleUnicode = string.Empty;
        Artist = string.Empty;
        ArtistUnicode = string.Empty;
        Creator = string.Empty;
        Difficulty = string.Empty;
        Source = string.Empty;
        Tags = string.Empty;
        BeatmapID = "0";
        BeatmapSetID = "-1";
    }

        public Metadata(string title, string titleUnicode, string artist, string artistUnicode, string creator, string version, string source, string tags, string beatmapID, string beatmapSetID)
        {
            Title = title;
            TitleUnicode = titleUnicode;
            Artist = artist;
            ArtistUnicode = artistUnicode;
            Creator = creator;
            Difficulty = version;
            Source = source;
            Tags = tags;
            BeatmapID = beatmapID;
            BeatmapSetID = beatmapSetID;
        }
}

public struct General
{
    public readonly string Version = "v14";
    public string PreviewTime;
    public string AudioFilename;
    public string AudioLeadIn;
    public string Countdown;
    public string SampleSet;
    public string StackLeniency;
    public string Mode;
    public string LetterboxInBreaks;
    public string SpecialStyle;
    public string WidescreenStoryboard;
    public double HPDrainRate;
    public double CircleSize;
    public double OverallDifficulty;
    public double ApproachRate;
    public double SliderMultiplier;
    public double SliderTickRate;
    public string? ImageEvent;
    public string? VideoEvent;

    public General()
    {
        PreviewTime = "-1";
        AudioFilename = string.Empty;
        AudioLeadIn = "0";
        Countdown = "0";
        SampleSet = "Soft";
        StackLeniency = "0.7";
        Mode = string.Empty;
        LetterboxInBreaks = "0";
        SpecialStyle = "1";
        WidescreenStoryboard = "0";
        HPDrainRate = 0;
        CircleSize = 8;
        OverallDifficulty = 0;
        ApproachRate = 5;
    }

    public General(string version, string previewTime, string audioFilename, string audioLeadIn, string countdown, string sampleSet, string stackLeniency, string mode, string letterboxInBreaks, string specialStyle, string widescreenStoryboard, double hPDrainRate, double circleSize, double overallDifficulty, double approachRate, double sliderMultiplier, double sliderTickRate, string imageEvent, string videoEvent)
    {
        Version = version;
        PreviewTime = previewTime;
        AudioFilename = audioFilename;
        AudioLeadIn = audioLeadIn;
        Countdown = countdown;
        SampleSet = sampleSet;
        StackLeniency = stackLeniency;
        Mode = mode;
        LetterboxInBreaks = letterboxInBreaks;
        SpecialStyle = specialStyle;
        WidescreenStoryboard = widescreenStoryboard;
        HPDrainRate = hPDrainRate;
        CircleSize = circleSize;
        OverallDifficulty = overallDifficulty;
        ApproachRate = approachRate;
        SliderMultiplier = sliderMultiplier;
        SliderTickRate = sliderTickRate;
        ImageEvent = imageEvent;
        VideoEvent = videoEvent;
    }
}