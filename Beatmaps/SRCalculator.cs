using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Beatmaps
{
    public struct Note(int k, int h, int t)
    {
        public readonly int K = k;
        public readonly int H = h;
        public readonly int T = t;
    }

    public class SRCalculator
    {
        private readonly double lambda_n = 5;
        private readonly double lambda_1 = 0.11;
        private readonly double lambda_3 = 24;
        private readonly double lambda_2 = 7.0;
        private readonly double lambda_4 = 0.1;
        private readonly double w_0 = 0.4;
        private readonly double w_1 = 2.7;
        private readonly double p_1 = 1.5;
        private readonly double w_2 = 0.27;
        private readonly double p_0 = 1.0;
        private double x = -1;

        private readonly int granularity = 1; // 保持为1，确保精度不变


        private readonly double[][] crossMatrix =
        [
            [-1],
            [0.075, 0.075],
            [0.125, 0.05, 0.125],
            [0.125, 0.125, 0.125, 0.125],
            [0.175, 0.25, 0.05, 0.25, 0.175],
            [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
            [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
            [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
            [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
            [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
            [0.625, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.625]
        ];


        private int T;
        private int K;
        private Note[] noteSeq = [];
        private Note[][] noteSeqByColumn = [];
        private Note[] LNSeq = [];
        private Note[] tailSeq = [];

        public double Calculate(List<Note> noteSequence, int keyCount, double od)
        {
            var totalStopwatch = Stopwatch.StartNew(); // 总时间计时开始

            // Initialize data structures
            K = keyCount;
            // 优化：避免多次LINQ排序，使用Array.Sort
            noteSeq = noteSequence.ToArray();
            Array.Sort(noteSeq, (a, b) =>
            {
                var cmp = a.H.CompareTo(b.H);
                return cmp != 0 ? cmp : a.K.CompareTo(b.K);
            });

            x = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3)) / 500, 0.5);

            noteSeqByColumn = noteSeq.GroupBy(n => n.K).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();

            // 优化：预计算LN序列长度，避免Where().ToArray()
            var lnCount = 0;
            foreach (var note in noteSeq)
                if (note.T >= 0) lnCount++;

            LNSeq = new Note[lnCount];
            var lnIndex = 0;
            foreach (var note in noteSeq)
                if (note.T >= 0) LNSeq[lnIndex++] = note;

            // 优化：直接排序LNSeq而不是创建新数组
            Array.Sort(LNSeq, (a, b) => a.T.CompareTo(b.T));
            tailSeq = LNSeq;

            var LNDict = new Dictionary<int, List<Note>>();
            foreach (var note in LNSeq)
            {
                if (!LNDict.ContainsKey(note.K))
                    LNDict[note.K] = new List<Note>();
                LNDict[note.K].Add(note);
            }

            // var LNSeqByColumn = LNDict.Values.OrderBy(list => list[0].K).ToList(); // 未使用，移除

            // Calculate T
            T = Math.Max(noteSeq.Max(n => n.H), noteSeq.Max(n => n.T)) + 1;

            try
            {
                var stopwatch = new Stopwatch();

                // Start all sections in parallel
                stopwatch.Start();

                // Define tasks for each section
                // 优化：使用并行任务加速Section 23/24/25的计算
                var task23 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    var (jbar, deltaKsResult) = CalculateSection23();
                    sectionStopwatch.Stop();
                    return (jbar, deltaKsResult);
                });

                var task24 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    var Xbar = CalculateSection24();
                    sectionStopwatch.Stop();
                    return Xbar;
                });

                var task25 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    var Pbar = CalculateSection25();
                    sectionStopwatch.Stop();
                    return Pbar;
                });

                // Wait for all tasks to complete
                Task.WaitAll(task23, task24, task25);

                // Retrieve results
                var (Jbar, deltaKs) = task23.Result;
                var Xbar = task24.Result;
                var Pbar = task25.Result;

                stopwatch.Stop();
                Logger.WriteLine(LogLevel.Debug,
                    $"[SRCalculator]Section 23/24/25 Time: {stopwatch.ElapsedMilliseconds}ms");

                stopwatch.Restart();
                var task26 = Task.Run(() => CalculateSection26(deltaKs));
                var task27 = Task.Run(CalculateSection27);

                // Wait for both tasks to complete
                Task.WaitAll(task26, task27);

                // Retrieve results
                var (Abar, KS) = task26.Result;
                var (Rbar, _) = task27.Result;

                stopwatch.Stop();
                Logger.WriteLine(LogLevel.Debug,
                    $"[SRCalculator]Section 26/27 Time: {stopwatch.ElapsedMilliseconds}ms");

                // Final calculation
                stopwatch.Restart();
                var result = CalculateSection3(Jbar, Xbar, Pbar, Abar, Rbar, KS);
                stopwatch.Stop();
                Logger.WriteLine(LogLevel.Debug, $"[SRCalculator]Section 3 Time: {stopwatch.ElapsedMilliseconds}ms");

                totalStopwatch.Stop(); // 总时间计时结束
                Logger.WriteLine(LogLevel.Debug,
                    $"[SRCalculator]Total Calculate Time: {totalStopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Exception: {ex.Message}");
                Logger.WriteLine(LogLevel.Error, $"[SRCalculator] StackTrace: {ex.StackTrace}");
                return -1;
            }
        }

        // private double[] Smooth(double[] lst)
        // {
        //     var lstbar = new double[T];
        //     var windowSum = 0.0;

        //     for (int i = 0; i < Math.Min(500, T); i += granularity)
        //         windowSum += lst[i];

        //     for (int s = 0; s < T; s += granularity)
        //     {
        //         lstbar[s] = 0.001 * windowSum * granularity;
        //         if (s + 500 < T)
        //             windowSum += lst[s + 500];
        //         if (s - 500 >= 0)
        //             windowSum -= lst[s - 500];
        //     }
        //     return lstbar;
        // }
        private double[] Smooth(double[] lst) // 优化方法：使用前缀和加速滑动窗口计算
        {
            var prefixSum = new double[T + 1];
            for (var i = 1; i <= T; i++)
                prefixSum[i] = prefixSum[i - 1] + lst[i - 1];

            var lstbar = new double[T];
            for (var s = 0; s < T; s += granularity)
            {
                var left = Math.Max(0, s - 500);
                var right = Math.Min(T, s + 500);
                var sum = prefixSum[right] - prefixSum[left];
                lstbar[s] = 0.001 * sum * granularity; // 匹配原滑动窗口逻辑
            }

            return lstbar;
        }

        private double[] Smooth2(double[] lst)
        {
            var lstbar = new double[T];
            var windowSum = 0.0;
            var windowLen = Math.Min(500, T);

            for (var i = 0; i < windowLen; i += granularity)
                windowSum += lst[i];

            for (var s = 0; s < T; s += granularity)
            {
                lstbar[s] = windowSum / windowLen * granularity;

                if (s + 500 < T)
                {
                    windowSum += lst[s + 500];
                    windowLen += granularity;
                }

                if (s - 500 >= 0)
                {
                    windowSum -= lst[s - 500];
                    windowLen -= granularity;
                }
            }

            return lstbar;
        }

        private double JackNerfer(double delta)
        {
            return 1 - 7 * Math.Pow(10, -5) * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);
        }

        private (double[] Jbar, double[][] deltaKs) CalculateSection23()
        {
            var JKs = new double[K][];
            var deltaKs = new double[K][];

            // 并行化：每个k独立计算，优化性能
            Parallel.For(0, K, k =>
            {
                JKs[k] = new double[T];
                deltaKs[k] = new double[T];
                Array.Fill(deltaKs[k], 1e9);

                // 只有当该列有音符时才处理
                if (k < noteSeqByColumn.Length && noteSeqByColumn[k].Length > 1)
                {
                    for (var i = 0; i < noteSeqByColumn[k].Length - 1; i++)
                    {
                        var delta = 0.001 * (noteSeqByColumn[k][i + 1].H - noteSeqByColumn[k][i].H);
                        var val = Math.Pow(delta * (delta + lambda_1 * Math.Pow(x, 0.25)), -1) * JackNerfer(delta);

                        // Define start and end for filling the range in deltaKs and JKs
                        var start = noteSeqByColumn[k][i].H;
                        var end = noteSeqByColumn[k][i + 1].H;
                        var length = end - start;

                        // Use Span to fill subarrays
                        var deltaSpan = new Span<double>(deltaKs[k], start, length);
                        deltaSpan.Fill(delta);

                        var JKsSpan = new Span<double>(JKs[k], start, length);
                        JKsSpan.Fill(val);
                    }
                }
            });

            // Smooth the JKs array，优化：并行化Smooth调用
            var JbarKs = new double[K][];
            Parallel.For(0, K, k => JbarKs[k] = Smooth(JKs[k]));

            // Calculate Jbar
            var Jbar = new double[T];
            for (var s = 0; s < T; s += granularity)
            {
                double weightedSum = 0;
                double weightSum = 0;

                // Replace list allocation with direct accumulation
                for (var i = 0; i < K; i++)
                {
                    var val = JbarKs[i][s];
                    var weight = 1.0 / deltaKs[i][s];

                    weightSum += weight;
                    weightedSum += Math.Pow(Math.Max(val, 0), lambda_n) * weight;
                }

                weightSum = Math.Max(1e-9, weightSum);
                Jbar[s] = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);
            }

            return (Jbar, deltaKs);
        }

        private double[] CalculateSection24()
        {
            var XKs = new double[K + 1][];

            // 并行化：每个k独立计算，优化性能
            Parallel.For(0, K + 1, k =>
            {
                XKs[k] = new double[T];
                Note[] notesInPair;
                if (k == 0)
                {
                    notesInPair = noteSeqByColumn.Length > 0 ? noteSeqByColumn[0] : [];
                }
                else if (k == K)
                {
                    notesInPair = noteSeqByColumn.Length > 0 ? noteSeqByColumn[noteSeqByColumn.Length - 1] : [];
                }
                else
                {
                    var leftCol = k - 1;
                    var rightCol = k;
                    var leftNotes = leftCol < noteSeqByColumn.Length ? noteSeqByColumn[leftCol] : [];
                    var rightNotes = rightCol < noteSeqByColumn.Length ? noteSeqByColumn[rightCol] : [];
                    notesInPair = leftNotes.Concat(rightNotes).OrderBy(n => n.H).ToArray();
                }
                for (var i = 1; i < notesInPair.Length; i++)
                {
                    var delta = 0.001 * (notesInPair[i].H - notesInPair[i - 1].H);
                    var val = 0.16 * Math.Pow(Math.Max(x, delta), -2);

                    for (var s = notesInPair[i - 1].H; s < notesInPair[i].H; s++) XKs[k][s] = val;
                }
            });

            var X = new double[T];
            for (var s = 0; s < T; s += granularity)
            {
                X[s] = 0;
                for (var k = 0; k <= K; k++) X[s] += XKs[k][s] * crossMatrix[K][k];
            }

            return Smooth(X);
        }

        private double[] CalculateSection25()
        {
            var P = new double[T];
            var LNBodies = new double[T];

            // 优化：使用分块并行化，避免lock瓶颈
            var numThreads = Environment.ProcessorCount;
            var partialLNBodies = new double[numThreads][];
            for (var i = 0; i < numThreads; i++)
                partialLNBodies[i] = new double[T];

            Parallel.For(0, LNSeq.Length, i =>
            {
                var threadId = i % numThreads;
                var note = LNSeq[i];
                var t1 = Math.Min(note.H + 80, note.T);
                for (var t = note.H; t < t1; t++)
                    partialLNBodies[threadId][t] += 0.5;
                for (var t = t1; t < note.T; t++)
                    partialLNBodies[threadId][t] += 1;
            });

            // 合并结果
            for (var t = 0; t < T; t++)
            for (var i = 0; i < numThreads; i++)
                LNBodies[t] += partialLNBodies[i][t];

            // 优化：计算 LNBodies 前缀和，用于快速求和
            var prefixSumLNBodies = new double[T + 1];
            for (var t = 1; t <= T; t++)
                prefixSumLNBodies[t] = prefixSumLNBodies[t - 1] + LNBodies[t - 1];

            double B(double delta)
            {
                var val = 7.5 / delta;
                if (val is > 160 and < 360)
                    return 1 + 1.4 * Math.Pow(10, -7) * (val - 160) * Math.Pow(val - 360, 2);
                return 1;
            }

            for (var i = 0; i < noteSeq.Length - 1; i++)
            {
                var delta = 0.001 * (noteSeq[i + 1].H - noteSeq[i].H);
                if (delta < Math.Pow(10, -9))
                {
                    P[noteSeq[i].H] += 1000 * Math.Pow(0.02 * (4 / x - lambda_3), 1.0 / 4);
                }
                else
                {
                    var h_l = noteSeq[i].H;
                    var h_r = noteSeq[i + 1].H;
                    var v = 1 + lambda_2 * 0.001 * (prefixSumLNBodies[h_r] - prefixSumLNBodies[h_l]);

                    if (delta < 2 * x / 3)
                    {
                        var baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                               (1 - lambda_3 * Math.Pow(x, -1) * Math.Pow(delta - x / 2, 2)), 1.0 / 4) *
                            B(delta) * v / delta;

                        for (var s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                    else
                    {
                        var baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                               (1 - lambda_3 * Math.Pow(x, -1) * Math.Pow(x / 6, 2)), 1.0 / 4) *
                            B(delta) * v / delta;

                        for (var s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                }
            }

            return Smooth(P);
        }

        private (double[] Abar, int[] KS) CalculateSection26(double[][] deltaKs)
        {
            var KUKs = new bool[K][];
            for (var k = 0; k < K; k++) KUKs[k] = new bool[T];

            // 并行化：每个note独立填充KUKs，优化性能
            Parallel.ForEach(noteSeq, note =>
            {
                var startTime = Math.Max(0, note.H - 500);
                var endTime = note.T < 0 ? Math.Min(note.H + 500, T - 1) : Math.Min(note.T + 500, T - 1);

                for (var s = startTime; s < endTime; s++) KUKs[note.K][s] = true;
            });

            var KS = new int[T];
            var A = new double[T];
            Array.Fill(A, 1);

            var dks = new double[K - 1][];
            for (var k = 0; k < K - 1; k++) dks[k] = new double[T];

            // 并行化：每个s独立计算，优化性能
            Parallel.For(0, T / granularity, sIndex =>
            {
                var s = sIndex * granularity;
                var cols = new int[K]; // 使用数组而不是List
                var colCount = 0;
                for (var k = 0; k < K; k++)
                    if (KUKs[k][s])
                        cols[colCount++] = k;

                KS[s] = Math.Max(colCount, 1);

                for (var i = 0; i < colCount - 1; i++)
                {
                    var col1 = cols[i];
                    var col2 = cols[i + 1];

                    dks[col1][s] = Math.Abs(deltaKs[col1][s] - deltaKs[col2][s]) +
                                   Math.Max(0, Math.Max(deltaKs[col1][s], deltaKs[col2][s]) - 0.3);

                    var maxDelta = Math.Max(deltaKs[col1][s], deltaKs[col2][s]);
                    if (dks[col1][s] < 0.02)
                        A[s] *= Math.Min(0.75 + 0.5 * maxDelta, 1);
                    else if (dks[col1][s] < 0.07) A[s] *= Math.Min(0.65 + 5 * dks[col1][s] + 0.5 * maxDelta, 1);
                }
            });

            return (Smooth2(A), KS);
        }

        private Note FindNextNoteInColumn(Note note, Note[] columnNotes)
        {
            var index = Array.BinarySearch(columnNotes, note, Comparer<Note>.Create((a, b) => a.H.CompareTo(b.H)));

            // If the exact element is not found, BinarySearch returns a bitwise complement of the index.
            // Convert it to the nearest index of an element >= note.H
            if (index < 0) index = ~index;

            return index + 1 < columnNotes.Length
                ? columnNotes[index + 1]
                : new Note(0, (int)Math.Pow(10, 9), (int)Math.Pow(10, 9));
        }

        private (double[] Rbar, double[] Is) CalculateSection27()
        {
            var I = new double[LNSeq.Length];
            Parallel.For(0, tailSeq.Length, i =>
            {
                var (k, h_i, t_i) = (tailSeq[i].K, tailSeq[i].H, tailSeq[i].T);
                var columnNotes = k < noteSeqByColumn.Length ? noteSeqByColumn[k] : [];
                var nextNote = FindNextNoteInColumn(tailSeq[i], columnNotes);
                var (_, h_j, _) = (nextNote.K, nextNote.H, nextNote.T);

                var I_h = 0.001 * Math.Abs(t_i - h_i - 80) / x;
                var I_t = 0.001 * Math.Abs(h_j - t_i - 80) / x;
                I[i] = 2 / (2 + Math.Exp(-5 * (I_h - 0.75)) + Math.Exp(-5 * (I_t - 0.75)));
            });

            var Is = new double[T];
            var R = new double[T];

            Parallel.For(0, tailSeq.Length - 1, i =>
            {
                var delta_r = 0.001 * (tailSeq[i + 1].T - tailSeq[i].T);
                var isVal = 1 + I[i];
                var rVal = 0.08 * Math.Pow(delta_r, -1.0 / 2) * Math.Pow(x, -1) * (1 + lambda_4 * (I[i] + I[i + 1]));
                for (var s = tailSeq[i].T; s < tailSeq[i + 1].T; s++)
                {
                    Is[s] = isVal;
                    R[s] = rVal;
                }
            });

            return (Smooth(R), Is);
        }

        private void ForwardFill(double[] array)
        {
            double lastValidValue = 0; // Use initialValue for leading NaNs and 0s

            for (var i = 0; i < array.Length; i++)
                if (!double.IsNaN(array[i]) && array[i] != 0) // Check if the current value is valid (not NaN or 0)
                    lastValidValue = array[i];
                else
                    array[i] = lastValidValue; // Replace NaN or 0 with last valid value or initial value
        }

        private double CalculateSection3(double[] Jbar, double[] Xbar, double[] Pbar,
            double[] Abar, double[] Rbar, int[] KS)
        {
            var C = new double[T];
            int start = 0, end = 0;

            for (var t = 0; t < T; t++)
            {
                while (start < noteSeq.Length && noteSeq[start].H < t - 500)
                    start++;
                while (end < noteSeq.Length && noteSeq[end].H < t + 500)
                    end++;
                C[t] = end - start;
            }

            var S = new double[T];
            var D = new double[T];

            // 并行化计算S和D
            Parallel.For(0, T, t =>
            {
                // Ensure all values are non-negative
                Jbar[t] = Math.Max(0, Jbar[t]);
                Xbar[t] = Math.Max(0, Xbar[t]);
                Pbar[t] = Math.Max(0, Pbar[t]);
                Abar[t] = Math.Max(0, Abar[t]);
                Rbar[t] = Math.Max(0, Rbar[t]);

                var term1 = Math.Pow(w_0 * Math.Pow(Math.Pow(Abar[t], 3.0 / KS[t]) * Jbar[t], 1.5), 1);
                var term2 = Math.Pow((1 - w_0) * Math.Pow(Math.Pow(Abar[t], 2.0 / 3) *
                                                          (0.8 * Pbar[t] + Rbar[t]), 1.5), 1);
                S[t] = Math.Pow(term1 + term2, 2.0 / 3);

                var T_t = Math.Pow(Abar[t], 3.0 / KS[t]) * Xbar[t] / (Xbar[t] + S[t] + 1);
                D[t] = w_1 * Math.Pow(S[t], 1.0 / 2) * Math.Pow(T_t, p_1) + S[t] * w_2;
            });

            ForwardFill(D);
            ForwardFill(C);

            var weightedSum = 0.0;
            var weightSum = C.Sum();
            for (var t = 0; t < T; t++) weightedSum += Math.Pow(D[t], lambda_n) * C[t];

            var SR = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);

            SR = Math.Pow(SR, p_0) / Math.Pow(8, p_0) * 8;
            SR *= (noteSeq.Length + 0.5 * LNSeq.Length) / (noteSeq.Length + 0.5 * LNSeq.Length + 60);

            if (SR <= 2)
                SR = Math.Sqrt(SR * 2);
            SR *= 0.96 + 0.01 * K;
            return SR;
        }

        public List<Note> getNotes(Beatmap beatmap)
        {
            var notes = new List<Note>();
            double cs = beatmap.DifficultySection.CircleSize;
            foreach (var hitobject in beatmap.HitObjects)
            {
                var col = (int)Math.Floor(hitobject.Position.X * cs / 512.0);
                var time = hitobject.StartTime;
                var tail = hitobject.EndTime > hitobject.StartTime ? hitobject.EndTime : -1;
                notes.Add(new Note(col, time, tail));
            }

            return notes;
        }
    }
}