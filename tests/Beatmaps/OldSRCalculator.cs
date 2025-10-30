using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tests.Beatmaps
{
    public class OldSRCalculator
    {
        private readonly double[][] crossMatrix = new[]
        {
            new double[] { -1 },
            new[] { 0.075, 0.075 },
            new[] { 0.125, 0.05, 0.125 },
            new[] { 0.125, 0.125, 0.125, 0.125 },
            new[] { 0.175, 0.25, 0.05, 0.25, 0.175 },
            new[] { 0.175, 0.25, 0.175, 0.175, 0.25, 0.175 },
            new[] { 0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225 },
            new[] { 0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225 },
            new[] { 0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275 },
            new[] { 0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275 },
            new[] { 0.625, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.625 }
        };

        private readonly int granularity = 1;
        private readonly double lambda_1 = 0.11;
        private readonly double lambda_2 = 7.0;
        private readonly double lambda_3 = 24;
        private readonly double lambda_4 = 0.1;
        private readonly double lambda_n = 5;
        private readonly double p_0 = 1.0;
        private readonly double p_1 = 1.5;
        private readonly double w_0 = 0.4;
        private readonly double w_1 = 2.7;
        private readonly double w_2 = 0.27;
        private int K;
        private SRsNote[] LNSeq = Array.Empty<SRsNote>();
        private SRsNote[] noteSeq = Array.Empty<SRsNote>();
        private SRsNote[][] noteSeqByColumn = Array.Empty<SRsNote[]>();

        private int T;
        private SRsNote[] tailSeq = Array.Empty<SRsNote>();
        private double x = -1;

        public double Calculate(List<SRsNote> noteSequence, int keyCount, double od, out Dictionary<string, long> times)
        {
            // Initialize data structures
            K = keyCount;
            noteSeq = noteSequence.OrderBy(n => n.H).ThenBy(n => n.K).ToArray();
            x = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3)) / 500, 0.5);
            noteSeqByColumn = noteSeq.GroupBy(n => n.K).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();

            // Create LN sequences
            LNSeq = noteSeq.Where(n => n.T >= 0).ToArray();
            tailSeq = LNSeq.OrderBy(n => n.T).ToArray();

            var LNDict = new Dictionary<int, List<SRsNote>>();

            foreach (SRsNote note in LNSeq)
            {
                if (!LNDict.ContainsKey(note.K))
                    LNDict[note.K] = new List<SRsNote>();
                LNDict[note.K].Add(note);
            }

            List<List<SRsNote>> LNSeqByColumn = LNDict.Values.OrderBy(list => list[0].K).ToList();

            // Calculate T
            T = Math.Max(noteSeq.Max(n => n.H), noteSeq.Max(n => n.T)) + 1;

            try
            {
                times = new Dictionary<string, long>();
                var stopwatch = new Stopwatch();

                // Start all sections in parallel
                stopwatch.Start();

                // Define tasks for each section
                Task<(double[] Jbar, double[][] deltaKs)> task23 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    (double[] Jbar, double[][] deltaKs) = CalculateSection23();
                    sectionStopwatch.Stop();
                    return (Jbar, deltaKs);
                });

                Task<double[]> task24 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    double[] Xbar = CalculateSection24();
                    sectionStopwatch.Stop();
                    return Xbar;
                });

                Task<double[]> task25 = Task.Run(() =>
                {
                    var sectionStopwatch = Stopwatch.StartNew();
                    double[] Pbar = CalculateSection25();
                    sectionStopwatch.Stop();
                    return Pbar;
                });

                // Wait for all tasks to complete
                Task.WaitAll(task23, task24, task25);

                // Retrieve results
                (double[] Jbar, double[][] deltaKs) = task23.Result;
                double[] Xbar = task24.Result;
                double[] Pbar = task25.Result;

                stopwatch.Stop();
                times["Section232425"] = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                Task<(double[] Abar, int[] KS)> task26 = Task.Run(() => CalculateSection26(deltaKs));
                Task<(double[] Rbar, double[] Is)> task27 = Task.Run(() => CalculateSection27());

                // Wait for both tasks to complete
                Task.WaitAll(task26, task27);

                // Retrieve results
                (double[] Abar, int[] KS) = task26.Result;
                (double[] Rbar, double[] Is) = task27.Result;

                stopwatch.Stop();
                times["Section2627"] = stopwatch.ElapsedMilliseconds;

                // Final calculation
                stopwatch.Restart();
                double result = CalculateSection3(Jbar, Xbar, Pbar, Abar, Rbar, KS);
                stopwatch.Stop();
                times["Section3"] = stopwatch.ElapsedMilliseconds;

                times["Total"] = times["Section232425"] + times["Section2627"] + times["Section3"];

                return result;
            }
            catch (Exception)
            {
                times = new Dictionary<string, long>
                {
                    ["Section232425"] = 0,
                    ["Section2627"] = 0,
                    ["Section3"] = 0,
                    ["Total"] = 0
                };
                return -1;
            }
        }

        private double[] Smooth(double[] lst)
        {
            double[] lstbar = new double[T];
            double windowSum = 0.0;

            for (int i = 0; i < Math.Min(500, T); i += granularity)
                windowSum += lst[i];

            for (int s = 0; s < T; s += granularity)
            {
                lstbar[s] = 0.001 * windowSum * granularity;
                if (s + 500 < T)
                    windowSum += lst[s + 500];
                if (s - 500 >= 0)
                    windowSum -= lst[s - 500];
            }

            return lstbar;
        }

        private double[] Smooth2(double[] lst)
        {
            double[] lstbar = new double[T];
            double windowSum = 0.0;
            int windowLen = Math.Min(500, T);

            for (int i = 0; i < windowLen; i += granularity)
                windowSum += lst[i];

            for (int s = 0; s < T; s += granularity)
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
            return 1 - (7 * Math.Pow(10, -5) * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4));
        }

        private (double[] Jbar, double[][] deltaKs) CalculateSection23()
        {
            double[][] JKs = new double[K][];
            double[][] deltaKs = new double[K][];

            // Fill JKs and deltaKs with values
            for (int k = 0; k < K; k++)
            {
                JKs[k] = new double[T];
                deltaKs[k] = new double[T];
                Array.Fill(deltaKs[k], 1e9);

                for (int i = 0; i < noteSeqByColumn[k].Length - 1; i++)
                {
                    double delta = 0.001 * (noteSeqByColumn[k][i + 1].H - noteSeqByColumn[k][i].H);
                    double val = Math.Pow(delta * (delta + (lambda_1 * Math.Pow(x, 0.25))), -1) * JackNerfer(delta);

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

            // Smooth the JKs array
            double[][] JbarKs = new double[K][];
            for (int k = 0; k < K; k++)
                JbarKs[k] = Smooth(JKs[k]);

            // Calculate Jbar
            double[] Jbar = new double[T];

            for (int s = 0; s < T; s += granularity)
            {
                double weightedSum = 0;
                double weightSum = 0;

                // Replace list allocation with direct accumulation
                for (int i = 0; i < K; i++)
                {
                    double val = JbarKs[i][s];
                    double weight = 1.0 / deltaKs[i][s];

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
            double[][] XKs = new double[K + 1][];

            for (int k = 0; k <= K; k++)
            {
                XKs[k] = new double[T];
                SRsNote[] notesInPair = k == 0 ? noteSeqByColumn[0] :
                                        k == K ? noteSeqByColumn[K - 1] :
                                                 noteSeqByColumn[k - 1].Concat(noteSeqByColumn[k])
                                                                       .OrderBy(n => n.H).ToArray();

                for (int i = 1; i < notesInPair.Length; i++)
                {
                    double delta = 0.001 * (notesInPair[i].H - notesInPair[i - 1].H);
                    double val = 0.16 * Math.Pow(Math.Max(x, delta), -2);

                    for (int s = notesInPair[i - 1].H; s < notesInPair[i].H; s++) XKs[k][s] = val;
                }
            }

            double[] X = new double[T];

            for (int s = 0; s < T; s += granularity)
            {
                X[s] = Enumerable.Range(0, K + 1)
                                 .Sum(k => XKs[k][s] * crossMatrix[K][k]);
            }

            return Smooth(X);
        }

        private double[] CalculateSection25()
        {
            double[] P = new double[T];
            double[] LNBodies = new double[T];

            foreach (SRsNote note in LNSeq)
            {
                int t1 = Math.Min(note.H + 80, note.T);
                for (int t = note.H; t < t1; t++)
                    LNBodies[t] += 0.5;
                for (int t = t1; t < note.T; t++)
                    LNBodies[t] += 1;
            }

            double B(double delta)
            {
                double val = 7.5 / delta;
                if (160 < val && val < 360)
                    return 1 + (1.4 * Math.Pow(10, -7) * (val - 160) * Math.Pow(val - 360, 2));
                return 1;
            }

            for (int i = 0; i < noteSeq.Length - 1; i++)
            {
                double delta = 0.001 * (noteSeq[i + 1].H - noteSeq[i].H);

                if (delta < Math.Pow(10, -9))
                    P[noteSeq[i].H] += 1000 * Math.Pow(0.02 * ((4 / x) - lambda_3), 1.0 / 4);
                else
                {
                    int h_l = noteSeq[i].H;
                    int h_r = noteSeq[i + 1].H;
                    double v = 1 + (lambda_2 * 0.001 * LNBodies.Skip(h_l).Take(h_r - h_l).Sum());

                    if (delta < 2 * x / 3)
                    {
                        double baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                                  (1 - (lambda_3 * Math.Pow(x, -1) * Math.Pow(delta - (x / 2), 2))), 1.0 / 4) *
                                         B(delta) * v / delta;

                        for (int s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                    else
                    {
                        double baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                                  (1 - (lambda_3 * Math.Pow(x, -1) * Math.Pow(x / 6, 2))), 1.0 / 4) *
                                         B(delta) * v / delta;

                        for (int s = h_l; s < h_r; s++)
                            P[s] += baseVal;
                    }
                }
            }

            return Smooth(P);
        }

        private (double[] Abar, int[] KS) CalculateSection26(double[][] deltaKs)
        {
            bool[][] KUKs = new bool[K][];
            for (int k = 0; k < K; k++) KUKs[k] = new bool[T];

            foreach (SRsNote note in noteSeq)
            {
                int startTime = Math.Max(0, note.H - 500);
                int endTime = note.T < 0 ? Math.Min(note.H + 500, T - 1) : Math.Min(note.T + 500, T - 1);

                for (int s = startTime; s < endTime; s++) KUKs[note.K][s] = true;
            }

            int[] KS = new int[T];
            double[] A = new double[T];
            Array.Fill(A, 1);

            double[][] dks = new double[K - 1][];
            for (int k = 0; k < K - 1; k++) dks[k] = new double[T];

            for (int s = 0; s < T; s += granularity)
            {
                var cols = new List<int>(K);

                for (int k = 0; k < K; k++)
                {
                    if (KUKs[k][s])
                        cols.Add(k);
                }

                KS[s] = Math.Max(cols.Count, 1);

                for (int i = 0; i < cols.Count - 1; i++)
                {
                    int col1 = cols[i];
                    int col2 = cols[i + 1];

                    dks[col1][s] = Math.Abs(deltaKs[col1][s] - deltaKs[col2][s]) +
                                   Math.Max(0, Math.Max(deltaKs[col1][s], deltaKs[col2][s]) - 0.3);

                    double maxDelta = Math.Max(deltaKs[col1][s], deltaKs[col2][s]);
                    if (dks[col1][s] < 0.02)
                        A[s] *= Math.Min(0.75 + (0.5 * maxDelta), 1);
                    else if (dks[col1][s] < 0.07) A[s] *= Math.Min(0.65 + (5 * dks[col1][s]) + (0.5 * maxDelta), 1);
                }
            }

            return (Smooth2(A), KS);
        }

        private SRsNote FindNextNoteInColumn(SRsNote sRsNote, SRsNote[] columnNotes)
        {
            int index = Array.BinarySearch(columnNotes, sRsNote, Comparer<SRsNote>.Create((a, b) => a.H.CompareTo(b.H)));

            // If the exact element is not found, BinarySearch returns a bitwise complement of the index.
            // Convert it to the nearest index of an element >= note.H
            if (index < 0) index = ~index;

            return index + 1 < columnNotes.Length
                       ? columnNotes[index + 1]
                       : new SRsNote(0, (int)Math.Pow(10, 9), (int)Math.Pow(10, 9));
        }

        private (double[] Rbar, double[] Is) CalculateSection27()
        {
            double[] I = new double[LNSeq.Length];

            for (int i = 0; i < tailSeq.Length; i++)
            {
                (int k, int h_i, int t_i) = (tailSeq[i].K, tailSeq[i].H, tailSeq[i].T);
                SRsNote nextSRsNote = FindNextNoteInColumn(tailSeq[i], noteSeqByColumn[k]);
                (_, int h_j, int t_j) = (nextSRsNote.K, nextSRsNote.H, nextSRsNote.T);

                double I_h = 0.001 * Math.Abs(t_i - h_i - 80) / x;
                double I_t = 0.001 * Math.Abs(h_j - t_i - 80) / x;
                I[i] = 2 / (2 + Math.Exp(-5 * (I_h - 0.75)) + Math.Exp(-5 * (I_t - 0.75)));
            }

            double[] Is = new double[T];
            double[] R = new double[T];

            for (int i = 0; i < tailSeq.Length - 1; i++)
            {
                double delta_r = 0.001 * (tailSeq[i + 1].T - tailSeq[i].T);

                for (int s = tailSeq[i].T; s < tailSeq[i + 1].T; s++)
                {
                    Is[s] = 1 + I[i];
                    R[s] = 0.08 * Math.Pow(delta_r, -1.0 / 2) * Math.Pow(x, -1) * (1 + (lambda_4 * (I[i] + I[i + 1])));
                }
            }

            return (Smooth(R), Is);
        }

        public void ForwardFill(double[] array)
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

        private double CalculateSection3(double[] Jbar,
                                         double[] Xbar,
                                         double[] Pbar,
                                         double[] Abar,
                                         double[] Rbar,
                                         int[] KS)
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

            for (int t = 0; t < T; t++)
            {
                // Ensure all values are non-negative
                Jbar[t] = Math.Max(0, Jbar[t]);
                Xbar[t] = Math.Max(0, Xbar[t]);
                Pbar[t] = Math.Max(0, Pbar[t]);
                Abar[t] = Math.Max(0, Abar[t]);
                Rbar[t] = Math.Max(0, Rbar[t]);

                double term1 = Math.Pow(w_0 * Math.Pow(Math.Pow(Abar[t], 3.0 / KS[t]) * Jbar[t], 1.5), 1);
                double term2 = Math.Pow((1 - w_0) * Math.Pow(Math.Pow(Abar[t], 2.0 / 3) *
                                                             ((0.8 * Pbar[t]) + Rbar[t]), 1.5), 1);
                S[t] = Math.Pow(term1 + term2, 2.0 / 3);

                double T_t = Math.Pow(Abar[t], 3.0 / KS[t]) * Xbar[t] / (Xbar[t] + S[t] + 1);
                D[t] = (w_1 * Math.Pow(S[t], 1.0 / 2) * Math.Pow(T_t, p_1)) + (S[t] * w_2);
            }

            ForwardFill(D);
            ForwardFill(C);

            double weightedSum = 0.0;
            double weightSum = C.Sum();
            for (int t = 0; t < T; t++) weightedSum += Math.Pow(D[t], lambda_n) * C[t];

            double SR = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);

            SR = Math.Pow(SR, p_0) / Math.Pow(8, p_0) * 8;
            SR *= (noteSeq.Length + (0.5 * LNSeq.Length)) / (noteSeq.Length + (0.5 * LNSeq.Length) + 60);

            if (SR <= 2)
                SR = Math.Sqrt(SR * 2);
            SR *= 0.96 + (0.01 * K);
            return SR;
        }

        public List<SRsNote> getNotes(Beatmap beatmap)
        {
            var notes = new List<SRsNote>();
            string section = "";
            double cs = beatmap.DifficultySection.CircleSize;

            foreach (HitObject hitobject in beatmap.HitObjects)
            {
                int col = (int)Math.Floor(hitobject.Position.X * cs / 512.0);
                int time = hitobject.StartTime;
                int tail = hitobject.EndTime > hitobject.StartTime ? hitobject.EndTime : -1;
                notes.Add(new SRsNote(col, time, tail));
            }

            return notes;
        }
    }
}
