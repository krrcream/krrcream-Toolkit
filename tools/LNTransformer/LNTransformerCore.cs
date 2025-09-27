using System;
using System.Collections.Generic;
using System.Linq;

namespace krrTools.tools.LNTransformer;

// Core LN transformation logic extracted from LNTransformer.xaml.cs
public static class LNTransformerCore
{
    private const double ERROR = 2.0;

    // Random-distribution utilities
    private static double RandDistribution(Random Rng, double u, double d)
    {
        if (d <= 0) return u;

        var u1 = Rng.NextDouble();
        var u2 = Rng.NextDouble();
        var z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
        var x = u + d * z;
        return x;
    }

    private static double TimeRound(double timedivide, double num)
    {
        var remainder = num % timedivide;
        if (remainder < timedivide / 2)
            return num - remainder;
        return num + timedivide - remainder;
    }

    private static double GetDurationByDistribution(Random Rng, OsuFileV14 osu, int startTime, double limitDuration,
        double mu, double sigmaDivisor, double divide, double divide2 = -1, double mu2 = -2, double mu1Dmu2 = -1)
    {
        var beatLength = osu.TimingPointAt(startTime).BeatLength;
        var timeDivide = beatLength / divide;
        var flag = true;
        var sigma = timeDivide / sigmaDivisor;
        var timenum = (int)Math.Round(limitDuration / timeDivide, 0);
        var duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu / 100, sigma));

        if (mu1Dmu2 >= 0.0)
            if (Rng.Next(100) >= mu1Dmu2)
            {
                timeDivide = beatLength / divide2;
                sigma = timeDivide / sigmaDivisor;
                timenum = (int)Math.Round(limitDuration / timeDivide, 0);
                duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu2 / 100, sigma));
            }

        if (mu < 0.0)
        {
            if (timenum < 1)
            {
                duration = timeDivide;
            }
            else
            {
                var rdtime = Rng.Next(1, timenum);
                duration = rdtime * timeDivide;
                duration = TimeRound(timeDivide, duration);
            }
        }

        if (duration > limitDuration - timeDivide)
        {
            duration = limitDuration - timeDivide;
            duration = TimeRound(timeDivide, duration);
        }

        if (duration <= timeDivide) duration = timeDivide;

        if (duration >= limitDuration - ERROR) flag = false;

        return flag ? duration : double.NaN;
    }

    // Transform a single column (returns original LN objects found in this column)
    private static List<ManiaHitObject> TransformColumn(Random Rng, double mu, double sigmaDivisor, double divide,
        OsuFileV14 osu, List<ManiaHitObject> newObjects,
        IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue,
        bool fixErrorIsChecked, int divide2 = -1, double mu2 = -2, double mu1Dmu2 = -1)
    {
        var originalLNObjects = new List<ManiaHitObject>();
        var newColumnObjects = new List<ManiaHitObject>();
        var locations = column.OrderBy(h => h.StartTime).ToList();
        var locCount = locations.Count;
        for (var i = 0; i < locCount - 1; i++)
        {
            double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
            var duration = GetDurationByDistribution(Rng, osu, locations[i].StartTime, fullDuration, mu,
                sigmaDivisor, divide, divide2, mu2, mu1Dmu2);

            var obj = locations[i];
            obj.Column = column.Key;
            if (originalLNIsChecked && locations[i].StartTime != locations[i].EndTime)
            {
                // keep original LN object
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else if ((Rng.NextDouble() * 100.0) < percentageValue && !double.IsNaN(duration))
            {
                var endTime = obj.StartTime + duration;
                if (fixErrorIsChecked)
                {
                    var point = osu.TimingPointAt((int)endTime);
                    endTime = TimeRound(point.BeatLength, endTime);
                }

                obj.EndTime = (int)endTime;
                newColumnObjects.Add(obj);
            }
            else
            {
                newColumnObjects.Add(obj.ToNote());
            }
        }

        // Handle last note
        var last = locations[locCount - 1];
        if (Math.Abs(last.StartTime - last.EndTime) <= ERROR || (Rng.NextDouble() * 100.0) >= percentageValue)
            newColumnObjects.Add(last.ToNote());
        else
            newColumnObjects.Add(last);

        newObjects.AddRange(newColumnObjects);
        return originalLNObjects;
    }

    private static List<ManiaHitObject> InvertColumn(OsuFileV14 osu, List<ManiaHitObject> newObjects, Random Rng,
        IGrouping<int, ManiaHitObject> column, double divideValue, bool originalLNIsChecked, double percentageValue,
        bool fixErrorIsChecked)
    {
        var locations = column.OrderBy(h => h.StartTime).ToList();

        var newColumnObjects = new List<ManiaHitObject>();
        var originalLNObjects = new List<ManiaHitObject>();
        var locCount = locations.Count;

        for (var i = 0; i < locCount - 1; i++)
        {
            double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
            var beatLength = osu.TimingPointAt(locations[i + 1].StartTime).BeatLength;
            var flag = true;
            var duration = fullDuration - beatLength / divideValue;

            if (duration < beatLength / divideValue) duration = beatLength / divideValue;

            if (duration > fullDuration - 3) flag = false;

            var obj = locations[i];
            obj.Column = column.Key;

            if (originalLNIsChecked && locations[i].StartTime != locations[i].EndTime)
            {
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else if ((Rng.NextDouble() * 100.0) < percentageValue && flag)
            {
                var endTime = locations[i].StartTime + duration;
                if (fixErrorIsChecked) endTime = TimeRound(osu.TimingPointAt((int)endTime).BeatLength, endTime);

                obj.EndTime = (int)endTime;
                newColumnObjects.Add(obj);
            }
            else
            {
                newColumnObjects.Add(obj.ToNote());
            }
        }

        var lastObj = locations[locCount - 1];
        double lastStartTime = lastObj.StartTime;
        double lastEndTime = lastObj.EndTime;
        if (originalLNIsChecked && Math.Abs(lastStartTime - lastEndTime) > ERROR)
        {
            var obj = lastObj;
            obj.Column = column.Key;
            newColumnObjects.Add(obj);
            originalLNObjects.Add(obj);
        }
        else
        {
            var obj = lastObj;
            obj.Column = column.Key;
            newColumnObjects.Add(obj.ToNote());
        }

        newObjects.AddRange(newColumnObjects);

        return originalLNObjects;
    }

    private static List<ManiaHitObject> TrueRandomColumn(List<ManiaHitObject> newObjects, Random Rng,
        IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue)
    {
        var locations = column.OrderBy(h => h.StartTime).ToList();

        var newColumnObjects = new List<ManiaHitObject>();
        var originalLNObjects = new List<ManiaHitObject>();
        var locCount = locations.Count;

        for (var i = 0; i < locCount - 1; i++)
        {
            double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
            // produce a random duration within [0, fullDuration)
            var duration = Rng.NextDouble() * fullDuration;

            var obj = locations[i];
            obj.Column = column.Key;

            if (originalLNIsChecked && locations[i].StartTime != locations[i].EndTime)
            {
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else if ((Rng.NextDouble() * 100.0) < percentageValue)
            {
                obj.EndTime = obj.StartTime + (int)duration;
                newColumnObjects.Add(obj);
            }
            else
            {
                newColumnObjects.Add(obj.ToNote());
            }
        }

        // Handle last note
        var last = locations[locCount - 1];
        if (originalLNIsChecked && last.StartTime != last.EndTime)
        {
            newColumnObjects.Add(last);
            originalLNObjects.Add(last);
        }
        else if (Math.Abs(last.StartTime - last.EndTime) <= ERROR || (Rng.NextDouble() * 100.0) >= percentageValue)
        {
            newColumnObjects.Add(last.ToNote());
        }
        else
        {
            newColumnObjects.Add(last);
        }

        newObjects.AddRange(newColumnObjects);

        return originalLNObjects;
    }

    private static void AfterTransform(List<ManiaHitObject> afterObjects, List<ManiaHitObject> originalLNObjects,
        OsuFileV14 osu, Random Rng, int transformColumnNum, bool originalLNIsChecked, int gapValue)
    {
        var resultObjects = new List<ManiaHitObject>();
        var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
        var keys = (int)osu.General.CircleSize;
        var maxGap = gapValue;
        var gap = maxGap;

        if (transformColumnNum > keys) transformColumnNum = keys;

        var randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng,
            transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

        foreach (var timeGroup in afterObjects.OrderBy(h => h.StartTime).GroupBy(h => h.StartTime))
        {
            foreach (var note in timeGroup)
                if (originalLNSet.Contains(note) && originalLNIsChecked)
                    resultObjects.Add(note);
                else if (randomColumnSet.Contains(note.Column) && note.StartTime != note.EndTime)
                    resultObjects.Add(note);
                else
                    resultObjects.Add(note.ToNote());

            gap--;
            if (gap == 0)
            {
                randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum)
                    .ToHashSet();
                gap = maxGap;
            }
        }

        osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
    }

    // High level transform that applies LN transformation to a copy of OsuFileV14 using parameters and returns the transformed copy.
    public static OsuFileV14 TransformFull(OsuFileV14 source, Preview.PreviewTransformation.LNPreviewParameters p)
    {
        // Create a deep copy to avoid mutating original
        var osu = source.Copy();
        var rng = new Random(114514 + (int)(p.LevelValue * 37));
        var newObjects = new List<ManiaHitObject>();
        var originalLNObjects = new List<ManiaHitObject>();

        // Handle special mode -3 (simple behavior from original)
        if ((int)p.LevelValue == -3)
        {
            for (var i = 0; i < osu.HitObjects.Count; i++)
                if (osu.HitObjects[i].IsLN)
                {
                    var obj = osu.HitObjects[i];
                    obj.EndTime = osu.HitObjects[i].StartTime;
                    osu.HitObjects[i] = obj;
                }

            // No further changes
            return osu;
        }

        foreach (var column in osu.HitObjects.GroupBy(h => h.Column))
            switch ((int)p.LevelValue)
            {
                case -2:
                {
                    var orig = TrueRandomColumn(newObjects, rng, column, p.OriginalLN, p.PercentageValue);
                    originalLNObjects.AddRange(orig);
                }
                    break;
                case 10:
                {
                    var orig = InvertColumn(osu, newObjects, rng, column, p.DivideValue, p.OriginalLN,
                        p.PercentageValue, p.FixError);
                    originalLNObjects.AddRange(orig);
                }
                    break;
                default:
                {
                    double mu;
                    double sigma;

                    if ((int)p.LevelValue == -1)
                    {
                        mu = -1;
                        sigma = 1;
                    }
                    else if ((int)p.LevelValue == 0)
                    {
                        mu = 1;
                        sigma = 100;
                    }
                    else
                    {
                        mu = p.LevelValue * 11;
                        sigma = 0.85;
                        if ((int)p.LevelValue == 8) sigma = 0.9;
                        else if ((int)p.LevelValue == 9) sigma = 1;
                    }

                    var orig = TransformColumn(rng, mu, sigma, p.DivideValue, osu, newObjects, column, p.OriginalLN,
                        p.PercentageValue, p.FixError);
                    originalLNObjects.AddRange(orig);
                }
                    break;
            }

        newObjects = newObjects.OrderBy(h => h.StartTime).ToList();
        originalLNObjects = originalLNObjects.OrderBy(h => h.StartTime).ToList();
        AfterTransform(newObjects, originalLNObjects, osu, rng, (int)p.ColumnValue, p.OriginalLN, (int)p.GapValue);
        osu.General.OverallDifficulty = p.OverallDifficulty;
        return osu;
    }
}