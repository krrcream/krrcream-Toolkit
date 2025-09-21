using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace krrTools.tools.LNTransformer
{
    public static class Helper
    {
        public static readonly int[] DIVIDE_NUMBER = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];
        public const double ERROR = 2.0;

        public delegate void FileFunc(string fileName);

        public static void Show(object obj)
        {
            MessageBox.Show(obj?.ToString()?.Trim() ?? string.Empty);
        }

        public static void DragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        public static List<string> GetDroppedFiles(DragEventArgs e, Func<string, bool>? filter = null, Action<string, int>? onFileFound = null)
        {
            List<string> fileList = new List<string>();
            string[]? droppedItems = e.Data.GetData(DataFormats.FileDrop) as string[];

            if (droppedItems == null)
            {
                return fileList;
            }

            foreach (string item in droppedItems)
            {
                if (File.Exists(item))
                {
                    if (filter == null || filter(item))
                    {
                        fileList.Add(item);
                        onFileFound?.Invoke(item, 1);
                    }
                }
                else if (IsDirectory(item))
                {
                    AddFilesInDirectory(item, fileList, filter, onFileFound);
                }
            }
            return fileList;
        }

        public static void DragDrop(DragEventArgs e, params FileFunc[] Func)
        {
            Array fileNames = (Array)e.Data.GetData(DataFormats.FileDrop);

            foreach (object obj in fileNames)
            {
                string? str = obj.ToString();
                if (string.IsNullOrEmpty(str))
                {
                    continue;
                }
                if (File.Exists(str))
                {
                    foreach (var func in Func)
                    {
                        func(str);
                    }
                }
                else if (IsDirectory(str))
                {
                    foreach (var func in Func)
                    {
                        DirectionFunc(str, func);
                    }
                }
            }
        }

        public static void DirectionFunc(string str, FileFunc FileFunc)
        {
            if (Directory.Exists(str))
            {
                string[] files = Directory.GetFiles(str);
                string[] dirs = Directory.GetDirectories(str);
                foreach (var file in files)
                {
                    FileFunc(file);
                }
                foreach (var dir in dirs)
                {
                    DirectionFunc(dir, FileFunc);
                }
            }
        }

        private static void AddFilesInDirectory(string directoryPath, List<string> fileList, Func<string, bool>? filter = null, Action<string, int>? onFileFound = null)
        {
            foreach (string file in Directory.GetFiles(directoryPath))
            {
                if (filter == null || filter(file))
                {
                    fileList.Add(file);
                    onFileFound?.Invoke(file, 1);
                }
            }

            foreach (string directory in Directory.GetDirectories(directoryPath))
            {
                AddFilesInDirectory(directory, fileList, filter, onFileFound);
            }
        }

        public static bool IsDirectory(string path)
        {
            FileInfo info = new FileInfo(path);
            return (info.Attributes & FileAttributes.Directory) != 0;
        }

        public static List<ManiaHitObject> SelectColumn(this List<ManiaHitObject> obj, int Column)
        {
            return obj.Where(o => o.Column == Column).ToList();
        }

        public static List<ManiaHitObject> SelectManyColumn(this List<ManiaHitObject> obj, int[] Columns)
        {
            return Columns.SelectMany(column => SelectColumn(obj, column)).ToList();
        }

        /// <summary>
        /// You need use OrderBy(o => o.StartTime) by yourself after use this method.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="minTime"></param>
        /// <param name="maxTime"></param>
        /// <param name="keys"></param>
        /// <param name="LNProbability">0 means no LN, 100 means always LN</param>
        /// <param name="durationMaxLimit"></param>
        /// <returns></returns>
        public static List<ManiaHitObject> GenerateRandomHitObjects(int count, int minTime, int maxTime, int keys, int LNProbability = 0, int durationMinLimit = 0, int durationMaxLimit = 0)
        {
            Random random = new Random();
            List<ManiaHitObject> list = new List<ManiaHitObject>();
            for (int i = 0; i < count; i++)
            {
                int time = random.Next(minTime, maxTime);
                int column = random.Next(0, keys);
                int endTime;
                endTime = random.Next(time + 1 + durationMinLimit, time + 1 + durationMaxLimit);
                if (LNProbability > 0 && random.Next(100) < LNProbability)
                {
                    var ln = new ManiaHitObject(OsuFileV14.KeyX[keys][column], 192, keys, time, 128, 0, "0:0:0:0:", endTime: endTime);
                    list.Add(ln);
                }
                else
                {
                    var note = new ManiaHitObject(OsuFileV14.KeyX[keys][column], keys, 192, keys, time, 1);
                    list.Add(note);
                }
            }

            list = list.CleanObject();

            if (list.Count < count)
            {
                list.AddRange(GenerateRandomHitObjects(count - list.Count, minTime, maxTime, keys, LNProbability, durationMinLimit, durationMaxLimit));
            }

            return list;
        }

        public static List<ManiaHitObject> CleanObject(this List<ManiaHitObject> obj, double error = 0)
        {
            var list = obj.Where(o => !CheckOverlapWithoutLN(obj, o, error)).OrderBy(o => o.StartTime).ToList();
            return list;
        }

        public static bool CheckOverlapWithoutLN(this List<ManiaHitObject> obj, ManiaHitObject note, double error = 0)
        {
            var list = obj.Where(o => o.Column == note.Column).ToList();
            list.Remove(note);
            return list.Any(o => Math.Abs(o.StartTime - note.StartTime) <= error);
        }

        public static bool CheckOverlap(this List<ManiaHitObject> obj, ManiaHitObject note)
        {
            return CheckOverlapWithoutLN(obj, note, 0);
        }

        // DurationEveryDivide =  60 / bpm / divide * 10000
        // 125ms is equivalent to duration time between adjacent every two 120BPM 1/4 timing line.
        // 125ms 相当于 120BPM 1/4 叠键每两行的时间间隔
        // 以下个人用方便消除乱键子弹使用
        //
        // Level 1: 125.00ms
        // 120BPM - 125.00ms    130BPM - 115.38ms    140BPM - 107.14ms
        // 150BPM - 100.00ms
        //
        // Level 2: 100.00ms
        // 160BPM - 93.75ms    170BPM - 88.23ms    180BPM - 83.33ms
        // 190BPM - 78.94ms
        //
        // Level 3: 75.00ms
        // 200BPM - 75.00ms    210BPM - 71.42ms    220BPM - 68.18ms
        // 230BPM - 65.21ms    240BPM - 62.50ms
        //
        // Level4: 60.00ms
        // 250BPM - 60.00ms    260BPM - 57.69ms    270BPM - 55.55ms
        // 280BPM - 53.57ms    290BPM - 51.72ms    300BPM - 50.00ms
        //

        public static TimeSpan GetAudioDuration(string filePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{filePath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();

                // 从输出中提取时长信息
                string durationString = ExtractDuration(output);
                if (TimeSpan.TryParse(durationString, out TimeSpan duration))
                {
                    return duration;
                }
            }

            return TimeSpan.Zero;
        }

        public static string ExtractDuration(string ffmpegOutput)
        {
            const string durationKey = "Duration:";
            int durationIndex = ffmpegOutput.IndexOf(durationKey);
            if (durationIndex != -1)
            {
                int startIndex = durationIndex + durationKey.Length;
                int endIndex = ffmpegOutput.IndexOf(",", startIndex);
                if (endIndex != -1)
                {
                    string duration = ffmpegOutput.Substring(startIndex, endIndex - startIndex).Trim();
                    return duration;
                }
            }
            return string.Empty;
        }

        public static double PreciseTime(double time, double bpm, double offset)
        {
            foreach (int t in DIVIDE_NUMBER)
            {
                double tem = time;
                time = offset + Math.Round((time - offset) / (bpm / t)) * bpm / t;
                if (Math.Abs(time - tem) < ERROR)
                    return time;
                else
                    time = tem;
            }
            return time;
        }

        /// <summary>
        /// 随机选择一个或多个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="Rng"></param>
        /// <param name="times"></param>
        /// <param name="duplicate"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static IEnumerable<T> SelectRandom<T>(this IEnumerable<T> enumerable, Random Rng, int times = 1, bool duplicate = false)
        {
            if (times <= 0)
            {
                return [];
            }

            var result = new List<T>();
            var list = enumerable.ToList();

            if (duplicate)
            {
                while (times > 0)
                {
                    int index = Rng.Next(list.Count);
                    result.Add(list[index]);
                    times--;
                }
            }
            else
            {
                if (times > enumerable.Count())
                {
                    throw new InvalidOperationException("Cannot select more items than the count of the enumerable.");
                }
                while (times > 0)
                {
                    int index = Rng.Next(list.Count);
                    result.Add(list[index]);
                    list.RemoveAt(index);
                    times--;
                }
            }
            return result.AsEnumerable();
        }

        public static int[] SelectRandomNumber(int count, bool duplicate = false, params int[] numbers)
        {
            if (numbers == null || numbers.Length == 0)
            {
                return Array.Empty<int>();
            }

            // 如果要求不重复且请求数量大于可用数字数量，则返回所有数字的随机排列
            if (!duplicate && count > numbers.Length)
            {
                count = numbers.Length;
            }

            int[] result = new int[count];
            // 使用静态Random实例避免短时间内创建多个实例导致的重复序列
            Random random = new Random(Guid.NewGuid().GetHashCode());

            if (duplicate)
            {
                for (int i = 0; i < count; i++)
                {
                    result[i] = numbers[random.Next(numbers.Length)];
                }
            }
            else
            {
                // 对于不重复的情况，使用Fisher-Yates洗牌算法更高效
                // 创建一个临时数组来存储numbers的副本
                int[] tempArray = new int[numbers.Length];
                Array.Copy(numbers, tempArray, numbers.Length);

                // 洗牌算法
                for (int i = 0; i < tempArray.Length; i++)
                {
                    int j = random.Next(i, tempArray.Length);
                    // 交换元素
                    int temp = tempArray[i];
                    tempArray[i] = tempArray[j];
                    tempArray[j] = temp;
                }

                // 取前count个元素
                Array.Copy(tempArray, result, count);
            }

            return result;
        }

        private static void AddFilesInDirectory(string path, List<string> fileList)
        {
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    fileList.Add(file);
                }
                foreach (string dir in Directory.GetDirectories(path))
                {
                    AddFilesInDirectory(dir, fileList);
                }
            }
            catch (Exception ex)
            {
                // It's good practice to handle potential exceptions, e.g., access denied.
                Debug.WriteLine($"Could not access {path}. Reason: {ex.Message}");
            }
        }
    }
}
