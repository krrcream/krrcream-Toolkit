using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// xxySR的私有结构体，表示单个音符的信息
    /// </summary>
    /// <param name="k">列号</param>
    /// <param name="h">开始时间</param>
    /// <param name="t">结束事件，非面条为-1</param>
    public struct SRsNote(int k, int h, int t)
    {
        public readonly int K = k;
        public readonly int H = h;
        public readonly int T = t;
    }

    public class NoteComparer : IComparer<SRsNote>
    {
        public int Compare(SRsNote a, SRsNote b)
        {
            int cmp = a.H.CompareTo(b.H);
            return cmp != 0 ? cmp : a.K.CompareTo(b.K);
        }
    }

    public class NoteComparerByT : IComparer<SRsNote>
    {
        public int Compare(SRsNote a, SRsNote b)
        {
            return a.T.CompareTo(b.T);
        }
    }

    [Obsolete("此版计算已过时，使用LAsOsuBeatmapParser的SR计算器替代，同步了最新算法，高性能、内存友好", false)]
    public class SRCalculator
    {
        // 单例模式：无状态类，线程安全，暂时不要用，高并行优化不好可能阻塞
        public static SRCalculator Instance { get; } = new SRCalculator();

        private const double lambda_n = 5;
        private const double lambda_1 = 0.11;
        private const double lambda_3 = 24;
        private const double lambda_2 = 7.0;
        private const double lambda_4 = 0.1;
        private const double w_0 = 0.4;
        private const double w_1 = 2.7;
        private const double p_1 = 1.5;
        private const double w_2 = 0.27;
        private const double p_0 = 1.0;

        private const int granularity = 1; // 只能保持为1，确保精度不变，不可修改

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
            [0.625, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.625],
            // Inferred matrices for K=11 to 18 based on user-specified patterns
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=11 (odd, unsupported)
            [0.8, 0.8, 0.8, 0.6, 0.4, 0.2, 0.05, 0.2, 0.4, 0.6, 0.8, 0.8, 0.8], // K=12 (even, sides 3 columns higher)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=13 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.2, 0.3, 0.3, 0.1, 0.1, 0.3, 0.3, 0.2, 0.2, 0.4, 0.4, 0.4], // K=14 (wave: low-low-high-high-low-low-high-high)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=15 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.2, 0.1, 0.1, 0.2, 0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.4], // K=16 (wave: low-low-high-high-low-low-high-high-low-low-high-high)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=17 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.4, 0.2, 0.4, 0.2, 0.3, 0.1, 0.1, 0.3, 0.2, 0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4] // K=18 (wave: low-low-high-low-high-low-high-low-low-high-low-high-low-high)
        ];

        // 同步计算SR，兼容旧接口
        public double CalculateSR(Beatmap beatmap, out Dictionary<string, long> times)
        {
            Task<(double sr, Dictionary<string, long> times)> task = CalculateSRAsync(beatmap);
            (double sr, Dictionary<string, long> t) = task.Result; // 同步等待（用于兼容旧接口）
            times = t;
            return sr;
        }

        // 极致性能优化：异步SR计算核心，已并行化所有section
        // 返回 (sr, times) 元组，times记录各部分耗时
        // 异步接口，推荐使用，可后台计算
        public async Task<(double sr, Dictionary<string, long> times)> CalculateSRAsync(Beatmap beatmap)
        {
            double od = beatmap.DifficultySection.OverallDifficulty;
            int cs = (int)beatmap.DifficultySection.CircleSize;
            var times = new Dictionary<string, long>();

            // Check if key count is supported (max 18 keys, even numbers only for K>10)
            // note数太低的没必要算
            if (cs > 18 || cs < 1 || (cs > 10 && cs % 2 == 1) || beatmap.HitObjects.Count < 10) return (-1, times); // Return invalid SR

            try
            {
                var totalStopwatch = Stopwatch.StartNew(); // 总时间计时开始
                var noteSequence = new List<SRsNote>();

                foreach (HitObject? hitObject in beatmap.HitObjects)
                {
                    int col = (int)Math.Floor(hitObject.Position.X * cs / 512.0);
                    int time = hitObject.StartTime;
                    int tail = hitObject.EndTime > hitObject.StartTime ? hitObject.EndTime : -1;
                    noteSequence.Add(new SRsNote(col, time, tail));
                }

                // 优化：避免多次LINQ排序，使用Array.Sort，并使用Span优化
                SRsNote[] noteSeq = noteSequence.ToArray();

                // Handle empty note sequence
                if (noteSeq.Length == 0)
                {
                    totalStopwatch.Stop();
                    return (0, times);
                }

                Array.Sort(noteSeq, new NoteComparer());

                double x = 0.3 * Math.Sqrt((64.5 - Math.Ceiling(od * 3)) / 500);

                SRsNote[][] noteSeqByColumn = noteSeq.GroupBy(n => n.K).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();

                // 优化：预计算LN序列长度，避免Where().ToArray()
                int lnCount = 0;

                foreach (SRsNote note in noteSeq)
                {
                    if (note.T >= 0)
                        lnCount++;
                }

                var LNSeq = new SRsNote[lnCount];
                int lnIndex = 0;

                foreach (SRsNote note in noteSeq)
                {
                    if (note.T >= 0)
                        LNSeq[lnIndex++] = note;
                }

                // 优化：直接排序LNSeq而不是创建新数组
                Array.Sort(LNSeq, new NoteComparerByT());
                SRsNote[] tailSeq = LNSeq;

                var LNDict = new Dictionary<int, List<SRsNote>>();

                foreach (SRsNote note in LNSeq)
                {
                    if (!LNDict.ContainsKey(note.K))
                        LNDict[note.K] = new List<SRsNote>();
                    LNDict[note.K].Add(note);
                }

                // Calculate T
                int T = Math.Max(noteSeq.Max(n => n.H), noteSeq.Max(n => n.T)) + 1;

                var stopwatch = new Stopwatch();

                // Start all sections in parallel
                stopwatch.Start();

                // Define tasks for each section
                // 优化：使用并行任务加速Section 23/24/25的计算
                Task<(double[] jBar, double[][] deltaKsResult)> task23 = Task.Run(() =>
                {
                    try
                    {
                        var sectionStopwatch = Stopwatch.StartNew();
                        (double[] jBar, double[][] deltaKsResult) = CalculateSection23(cs, noteSeqByColumn, T, x);
                        sectionStopwatch.Stop();
                        return (jBar, deltaKsResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Section23 Exception: {ex.Message}");
                        throw;
                    }
                });

                Task<double[]> task24 = Task.Run(() =>
                {
                    try
                    {
                        var sectionStopwatch = Stopwatch.StartNew();
                        double[] XBar = CalculateSection24(cs, T, noteSeqByColumn, x);
                        sectionStopwatch.Stop();
                        return XBar;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Section24 Exception: {ex.Message}");
                        throw;
                    }
                });

                Task<double[]> task25 = Task.Run(() =>
                {
                    try
                    {
                        var sectionStopwatch = Stopwatch.StartNew();
                        double[] PBar = CalculateSection25(T, LNSeq, noteSeq, x);
                        sectionStopwatch.Stop();
                        return PBar;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Section25 Exception: {ex.Message}");
                        throw;
                    }
                });

                // Wait for all tasks to complete
                await Task.WhenAll(task23, task24, task25).ConfigureAwait(false);

                // Retrieve results
                (double[] JBar, double[][] deltaKs) = task23.Result;
                double[] XBar = task24.Result;
                double[] PBar = task25.Result;

                stopwatch.Stop();
                // Logger.WriteLine(LogLevel.Debug, $"[SRCalculator]Section 23/24/25 Time: {stopwatch.ElapsedMilliseconds}ms");
                times["Section232425"] = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                Task<(double[] ABar, int[] KS)> task26 = Task.Run(() =>
                {
                    try
                    {
                        return CalculateSection26(deltaKs, cs, T, noteSeq);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Section26 Exception: {ex.Message}");
                        throw;
                    }
                });
                Task<(double[] RBar, double[] Is)> task27 = Task.Run(() =>
                {
                    try
                    {
                        return CalculateSection27(LNSeq, tailSeq, T, noteSeqByColumn, x);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Section27 Exception: {ex.Message}");
                        throw;
                    }
                });

                // Wait for both tasks to complete
                await Task.WhenAll(task26, task27).ConfigureAwait(false);

                // Retrieve results
                (double[] ABar, int[] KS) = task26.Result;
                (double[] RBar, _) = task27.Result;

                stopwatch.Stop();
                // Logger.WriteLine(LogLevel.Debug, $"[SRCalculator]Section 26/27 Time: {stopwatch.ElapsedMilliseconds}ms");
                times["Section2627"] = stopwatch.ElapsedMilliseconds;

                // Final calculation
                stopwatch.Restart();
                double result = CalculateSection3(JBar, XBar, PBar, ABar, RBar, KS, T, noteSeq, LNSeq, cs);
                stopwatch.Stop();
                // Logger.WriteLine(LogLevel.Debug, $"[SRCalculator]Section 3 Time: {stopwatch.ElapsedMilliseconds}ms");
                times["Section3"] = stopwatch.ElapsedMilliseconds;

                totalStopwatch.Stop(); // 总时间计时结束
                // Logger.WriteLine(LogLevel.Debug, $"[SRCalculator]Total Calculate Time: {totalStopwatch.ElapsedMilliseconds}ms");
                times["Total"] = totalStopwatch.ElapsedMilliseconds;

                return (result, times);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, $"[SRCalculator] Exception: {ex.Message}");
                Logger.WriteLine(LogLevel.Error, $"[SRCalculator] StackTrace: {ex.StackTrace}");
                times["Error"] = -1;
                return (-1, times);
            }
        }

        private double[] Smooth(double[] lst, int T)
        {
            ReadOnlySpan<double> lstSpan = lst;
            double[] prefixSum = new double[T + 1];
            Span<double> prefixSpan = prefixSum;
            prefixSpan[0] = 0;
            for (int i = 1; i <= T; i++)
                prefixSpan[i] = prefixSpan[i - 1] + lstSpan[i - 1];

            double[] lstBar = new double[T];
            Span<double> lstBarSpan = lstBar;

            for (int s = 0; s < T; s += granularity)
            {
                int left = Math.Max(0, s - 500);
                int right = Math.Min(T, s + 500);
                double sum = prefixSpan[right] - prefixSpan[left];
                lstBarSpan[s] = 0.001 * sum; // 因为步长是1ms，不允许修改
            }

            return lstBar;
        }

        private double[] Smooth2(double[] lst, int T)
        {
            ReadOnlySpan<double> lstSpan = lst;
            double[] lstBar = new double[T];
            Span<double> lstBarSpan = lstBar;
            double windowSum = 0.0;
            int windowLen = Math.Min(500, T);

            for (int i = 0; i < windowLen; i += granularity)
                windowSum += lstSpan[i];

            for (int s = 0; s < T; s += granularity)
            {
                lstBarSpan[s] = windowSum / windowLen * granularity;

                if (s + 500 < T)
                {
                    windowSum += lstSpan[s + 500];
                    windowLen += granularity;
                }

                if (s - 500 >= 0)
                {
                    windowSum -= lstSpan[s - 500];
                    windowLen -= granularity;
                }
            }

            return lstBar;
        }

        private (double[] JBar, double[][] deltaKs) CalculateSection23(int K, SRsNote[][] noteSeqByColumn, int T, double x)
        {
            double[][] JKs = new double[K][];
            double[][] deltaKs = new double[K][];

            if (K > 7)
            {
                // 局部变量，避免lambda捕获大对象
                SRsNote[][] localNoteSeqByColumn = noteSeqByColumn;
                int localT = T;
                double localX = x;
                Parallel.For(0, K, k =>
                {
                    JKs[k] = new double[localT];
                    deltaKs[k] = new double[localT];
                    Array.Fill(deltaKs[k], 1e9);

                    // 预计算复杂表达式，减少重复计算
                    double xPow025 = Math.Sqrt(Math.Sqrt(localX));
                    double lambda1X = lambda_1 * xPow025; // 只有当该列有音符时才处理

                    if (k < localNoteSeqByColumn.Length && localNoteSeqByColumn[k].Length > 1)
                    {
                        for (int i = 0; i < localNoteSeqByColumn[k].Length - 1; i++)
                        {
                            double delta = 0.001 * (localNoteSeqByColumn[k][i + 1].H - localNoteSeqByColumn[k][i].H);
                            double absDelta = Math.Abs(delta - 0.08);
                            double temp = 0.15 + absDelta;
                            double temp4 = temp * temp * temp * temp;
                            double jack = 1 - 7e-5 * (1 / temp4);
                            double val = 1 / (delta * (delta + lambda1X)) * jack;

                            // Define start and end for filling the range in deltaKs and JKs
                            int start = localNoteSeqByColumn[k][i].H;
                            int end = localNoteSeqByColumn[k][i + 1].H;
                            int length = end - start;

                            // Use Span to fill subarrays
                            var deltaSpan = new Span<double>(deltaKs[k], start, length);
                            deltaSpan.Fill(delta);

                            var JKsSpan = new Span<double>(JKs[k], start, length);
                            JKsSpan.Fill(val);
                        }
                    }
                });
            }
            else
            {
                for (int k = 0; k < K; k++)
                {
                    JKs[k] = new double[T];
                    deltaKs[k] = new double[T];
                    Array.Fill(deltaKs[k], 1e9);

                    // 预计算复杂表达式，减少重复计算
                    double xPow025 = Math.Sqrt(Math.Sqrt(x));
                    double lambda1X = lambda_1 * xPow025; // 只有当该列有音符时才处理

                    if (k < noteSeqByColumn.Length && noteSeqByColumn[k].Length > 1)
                    {
                        for (int i = 0; i < noteSeqByColumn[k].Length - 1; i++)
                        {
                            double delta = 0.001 * (noteSeqByColumn[k][i + 1].H - noteSeqByColumn[k][i].H);
                            double absDelta = Math.Abs(delta - 0.08);
                            double temp = 0.15 + absDelta;
                            double temp4 = temp * temp * temp * temp;
                            double jack = 1 - 7e-5 * (1 / temp4);
                            double val = 1 / (delta * (delta + lambda1X)) * jack;

                            // Define start and end for filling the range in deltaKs and JKs
                            int start = noteSeqByColumn[k][i].H;
                            int end = noteSeqByColumn[k][i + 1].H;
                            int length = end - start;

                            // Use Span to fill subarrays
                            var deltaSpan = new Span<double>(deltaKs[k], start, length);
                            deltaSpan.Fill(delta);

                            var JKsSpan = new Span<double>(JKs[k], start, length);
                            JKsSpan.Fill(val);
                        }
                    }
                }
            }

            // Smooth the JKs array，让系统自动调度
            double[][] JBarKs = new double[K][];
            Parallel.For(0, K, k => JBarKs[k] = Smooth(JKs[k], T));

            // Calculate JBar
            double[] JBar = new double[T];

            for (int s = 0; s < T; s += granularity)
            {
                double weightedSum = 0;
                double weightSum = 0;

                // Replace list allocation with direct accumulation
                for (int i = 0; i < K; i++)
                {
                    double val = JBarKs[i][s];
                    double weight = 1.0 / deltaKs[i][s];

                    weightSum += weight;
                    weightedSum += Math.Pow(Math.Max(val, 0), lambda_n) * weight;
                }

                weightSum = Math.Max(1e-9, weightSum);
                JBar[s] = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);
            }

            return (JBar, deltaKs);
        }

        private double[] CalculateSection24(int K, int T, SRsNote[][] noteSeqByColumn, double x)
        {
            double[][] XKs = new double[K + 1][];

            Parallel.For(0, K + 1, k =>
            {
                XKs[k] = new double[T];
                SRsNote[] notesInPair;

                if (k == 0)
                    notesInPair = noteSeqByColumn.Length > 0 ? noteSeqByColumn[0] : [];
                else if (k == K)
                    notesInPair = noteSeqByColumn.Length > 0 ? noteSeqByColumn[^1] : [];
                else
                {
                    int leftCol = k - 1;
                    int rightCol = k;
                    SRsNote[] leftNotes = leftCol < noteSeqByColumn.Length ? noteSeqByColumn[leftCol] : [];
                    SRsNote[] rightNotes = rightCol < noteSeqByColumn.Length ? noteSeqByColumn[rightCol] : [];
                    notesInPair = leftNotes.Concat(rightNotes).OrderBy(n => n.H).ToArray();
                }

                Span<double> XKsSpan = XKs[k];

                for (int i = 1; i < notesInPair.Length; i++)
                {
                    double delta = 0.001 * (notesInPair[i].H - notesInPair[i - 1].H);
                    double maxXd = Math.Max(x, delta);
                    double val = 0.16 / (maxXd * maxXd);

                    int start = notesInPair[i - 1].H;
                    int end = notesInPair[i].H;
                    int length = end - start;
                    XKsSpan.Slice(start, length).Fill(val);
                }
            });

            double[] X = new double[T];

            for (int s = 0; s < T; s += granularity)
            {
                X[s] = 0;
                for (int k = 0; k <= K; k++) X[s] += XKs[k][s] * crossMatrix[K][k];
            }

            return Smooth(X, T);
        }

        private double[] CalculateSection25(int T, SRsNote[] LNSeq, SRsNote[] noteSeq, double x)
        {
            double[] P = new double[T];
            double[] LNBodies = new double[T];

            // 简化：使用系统默认的线程数，避免过度限制
            int numThreads = Environment.ProcessorCount;
            double[] partialLNBodies = new double[numThreads * T];

            Parallel.For(0, LNSeq.Length, i =>
            {
                int threadId = i % numThreads;
                int offset = threadId * T;
                SRsNote sRsNote = LNSeq[i];
                int t1 = Math.Min(sRsNote.H + 80, sRsNote.T);
                for (int t = sRsNote.H; t < t1; t++)
                    partialLNBodies[offset + t] += 0.5;
                for (int t = t1; t < sRsNote.T; t++)
                    partialLNBodies[offset + t] += 1;
            });

            // 合并结果
            for (int t = 0; t < T; t++)
            {
                for (int i = 0; i < numThreads; i++)
                    LNBodies[t] += partialLNBodies[i * T + t];
            }

            // 优化：计算 LNBodies 前缀和，用于快速求和
            double[] prefixSumLNBodies = new double[T + 1];
            for (int t = 1; t <= T; t++)
                prefixSumLNBodies[t] = prefixSumLNBodies[t - 1] + LNBodies[t - 1];

            double B(double delta)
            {
                double val = 7.5 / delta;

                if (val is > 160 and < 360)
                {
                    double diff = val - 160;
                    double diff2 = val - 360;
                    return 1 + 1.4e-7 * diff * (diff2 * diff2);
                }

                return 1;
            }

            // 预计算常量，减少重复计算
            const double lambda2Scaled = lambda_2 * 0.001;

            for (int i = 0; i < noteSeq.Length - 1; i++)
            {
                double delta = 0.001 * (noteSeq[i + 1].H - noteSeq[i].H);

                if (delta < 1e-9)
                    P[noteSeq[i].H] += 1000 * Math.Sqrt(Math.Sqrt(0.02 * (4 / x - lambda_3)));
                else
                {
                    int h_l = noteSeq[i].H;
                    int h_r = noteSeq[i + 1].H;
                    double v = 1 + lambda2Scaled * (prefixSumLNBodies[h_r] - prefixSumLNBodies[h_l]);

                    if (delta < 2 * x / 3)
                    {
                        double baseVal = Math.Sqrt(Math.Sqrt(0.08 / x *
                                                             (1 - lambda_3 / x * (delta - x / 2) * (delta - x / 2)))) *
                                         B(delta) * v / delta;

                        for (int s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                    else
                    {
                        double baseVal = Math.Sqrt(Math.Sqrt(0.08 / x *
                                                             (1 - lambda_3 / x * (x / 6) * (x / 6)))) *
                                         B(delta) * v / delta;

                        for (int s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                }
            }

            return Smooth(P, T);
        }

        private (double[] ABar, int[] KS) CalculateSection26(double[][] deltaKs, int K, int T, SRsNote[] noteSeq)
        {
            bool[][] KUKs = new bool[K][];
            for (int k = 0; k < K; k++) KUKs[k] = new bool[T];

            // 并行化：每个note独立填充KUKs，优化性能
            Parallel.ForEach(noteSeq, note =>
            {
                int startTime = Math.Max(0, note.H - 500);
                int endTime = note.T < 0 ? Math.Min(note.H + 500, T - 1) : Math.Min(note.T + 500, T - 1);

                for (int s = startTime; s < endTime; s++) KUKs[note.K][s] = true;
            });

            int[] KS = new int[T];
            double[] A = new double[T];
            Array.Fill(A, 1);

            double[][] dks = new double[K - 1][];
            for (int k = 0; k < K - 1; k++) dks[k] = new double[T];

            // 并行化：每个s独立计算，优化性能
            Parallel.For(0, T / granularity, sIndex =>
            {
                int s = sIndex * granularity;
                int[] cols = new int[K]; // 使用数组而不是List
                int colCount = 0;

                for (int k = 0; k < K; k++)
                {
                    if (KUKs[k][s])
                        cols[colCount++] = k;
                }

                KS[s] = Math.Max(colCount, 1);

                for (int i = 0; i < colCount - 1; i++)
                {
                    int col1 = cols[i];
                    int col2 = cols[i + 1];

                    dks[col1][s] = Math.Abs(deltaKs[col1][s] - deltaKs[col2][s]) +
                                   Math.Max(0, Math.Max(deltaKs[col1][s], deltaKs[col2][s]) - 0.3);

                    double maxDelta = Math.Max(deltaKs[col1][s], deltaKs[col2][s]);
                    if (dks[col1][s] < 0.02)
                        A[s] *= Math.Min(0.75 + 0.5 * maxDelta, 1);
                    else if (dks[col1][s] < 0.07) A[s] *= Math.Min(0.65 + 5 * dks[col1][s] + 0.5 * maxDelta, 1);
                }
            });

            return (Smooth2(A, T), KS);
        }

        private SRsNote FindNextNoteInColumn(SRsNote sRsNote, SRsNote[] columnNotes)
        {
            int index = Array.BinarySearch(columnNotes, sRsNote, Comparer<SRsNote>.Create((a, b) => a.H.CompareTo(b.H)));

            // If the exact element is not found, BinarySearch returns a bitwise complement of the index.
            // Convert it to the nearest index of an element >= note.H
            if (index < 0) index = ~index;

            return index + 1 < columnNotes.Length
                       ? columnNotes[index + 1]
                       : new SRsNote(0, (int)1e9, (int)1e9);
        }

        private (double[] RBar, double[] Is) CalculateSection27(SRsNote[] LNSeq, SRsNote[] tailSeq, int T, SRsNote[][] noteSeqByColumn, double x)
        {
            double[] I = new double[LNSeq.Length];
            Parallel.For(0, tailSeq.Length, i =>
            {
                (int k, int h_i, int t_i) = (tailSeq[i].K, tailSeq[i].H, tailSeq[i].T);
                SRsNote[] columnNotes = k < noteSeqByColumn.Length ? noteSeqByColumn[k] : [];
                SRsNote nextSRsNote = FindNextNoteInColumn(tailSeq[i], columnNotes);
                (_, int h_j, _) = (nextSRsNote.K, nextSRsNote.H, nextSRsNote.T);

                double I_h = 0.001 * Math.Abs(t_i - h_i - 80) / x;
                double I_t = 0.001 * Math.Abs(h_j - t_i - 80) / x;
                I[i] = 2 / (2 + Math.Exp(-5 * (I_h - 0.75)) + Math.Exp(-5 * (I_t - 0.75)));
            });

            double[] Is = new double[T];
            double[] R = new double[T];

            Parallel.For(0, tailSeq.Length - 1, i =>
            {
                double delta_r = 0.001 * (tailSeq[i + 1].T - tailSeq[i].T);
                double isVal = 1 + I[i];
                double rVal = 0.08 * Math.Pow(delta_r, -1.0 / 2) * Math.Pow(x, -1) * (1 + lambda_4 * (I[i] + I[i + 1]));

                for (int s = tailSeq[i].T; s < tailSeq[i + 1].T; s++)
                {
                    Is[s] = isVal;
                    R[s] = rVal;
                }
            });

            return (Smooth(R, T), Is);
        }

        private void ForwardFill(double[] array)
        {
            double lastValidValue = 0; // Use initialValue for leading NaNs and 0s

            for (int i = 0; i < array.Length; i++)
            {
                if (!double.IsNaN(array[i]) && array[i] != 0) // Check if the current value is valid (not NaN or 0)
                    lastValidValue = array[i];
                else
                    array[i] = lastValidValue; // Replace NaN or 0 with last valid value or initial value
            }
        }

        private double CalculateSection3(double[] JBar,
                                         double[] XBar,
                                         double[] PBar,
                                         double[] ABar,
                                         double[] RBar,
                                         int[] KS,
                                         int T,
                                         SRsNote[] noteSeq,
                                         SRsNote[] LNSeq,
                                         int K)
        {
            double[] C = new double[T];
            int start = 0, end = 0;

            for (int t = 0; t < T; t++)
            {
                while (start < noteSeq.Length && noteSeq[start].H < t - 500)
                    start++;
                while (end < noteSeq.Length && noteSeq[end].H < t + 500)
                    end++;
                C[t] = end - start;
            }

            double[] S = new double[T];
            double[] D = new double[T];

            // 并行化计算S和D
            Parallel.For(0, T, t =>
            {
                // Ensure all values are non-negative
                JBar[t] = Math.Max(0, JBar[t]);
                XBar[t] = Math.Max(0, XBar[t]);
                PBar[t] = Math.Max(0, PBar[t]);
                ABar[t] = Math.Max(0, ABar[t]);
                RBar[t] = Math.Max(0, RBar[t]);

                double term1 = Math.Pow(w_0 * Math.Pow(Math.Pow(ABar[t], 3.0 / KS[t]) * JBar[t], 1.5), 1);
                double term2 = Math.Pow((1 - w_0) * Math.Pow(Math.Pow(ABar[t], 2.0 / 3) *
                                                             (0.8 * PBar[t] + RBar[t]), 1.5), 1);
                S[t] = Math.Pow(term1 + term2, 2.0 / 3);

                double T_t = Math.Pow(ABar[t], 3.0 / KS[t]) * XBar[t] / (XBar[t] + S[t] + 1);
                D[t] = w_1 * Math.Pow(S[t], 1.0 / 2) * Math.Pow(T_t, p_1) + S[t] * w_2;
            });

            ForwardFill(D);
            ForwardFill(C);

            double weightedSum = 0.0;
            double weightSum = C.Sum();
            for (int t = 0; t < T; t++) weightedSum += Math.Pow(D[t], lambda_n) * C[t];

            double SR = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);

            SR = Math.Pow(SR, p_0) / Math.Pow(8, p_0) * 8;
            SR *= (noteSeq.Length + 0.5 * LNSeq.Length) / (noteSeq.Length + 0.5 * LNSeq.Length + 60);

            if (SR <= 2)
                SR = Math.Sqrt(SR * 2);
            SR *= 0.96 + 0.01 * K;
            return SR;
        }

        public List<SRsNote> getNotes(Beatmap beatmap)
        {
            var notes = new List<SRsNote>();
            double cs = beatmap.DifficultySection.CircleSize;

            foreach (HitObject? hitObject in beatmap.HitObjects)
            {
                int col = (int)Math.Floor(hitObject.Position.X * cs / 512.0);
                int time = hitObject.StartTime;
                int tail = hitObject.EndTime > hitObject.StartTime ? hitObject.EndTime : -1;
                notes.Add(new SRsNote(col, time, tail));
            }

            return notes;
        }
    }
}
