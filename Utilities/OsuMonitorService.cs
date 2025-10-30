using System;
using System.Diagnostics;
using System.IO;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Tools.Listener;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;

#pragma warning disable CS0618 // 禁止提示

namespace krrTools.Utilities
{
    /// <summary>
    /// OSU监听服务 - 负责进程检测、内存读取和文件监控
    /// </summary>
    public class OsuMonitorService
    {
        private string _lastBeatmapFile = string.Empty;
        private int _lastProcessId = -1;
        private int _lastProcessCount = -1;
        private bool _lastIsOsuRunning;
        private bool _lastIsPlaying;
        private bool _hasLoggedNotChanged;

        [Inject]
        private StateBarManager stateBarManager { get; set; } = null!;

        // [Inject]
        private IOsuMemoryReader? reader { get; set; }

        public OsuMonitorService()
        {
            this.InjectServices();
        }

        public OsuMonitorService(IOsuMemoryReader? reader2)
        {
            this.InjectServices();
            reader ??= reader2;
        }

        /// <summary>
        /// 检测OSU进程并返回选中的进程
        /// </summary>
        public void DetectOsuProcess()
        {
            // TODO: 初始化有延迟，如果是监听开启伴随启动就会延迟好几秒。
            reader ??= OsuMemoryReader.Instance;

            Process[] osuProcesses = Process.GetProcessesByName("osu!");
            bool isOsuRunning = osuProcesses.Length > 0;

            // 只有当状态变化时才输出日志
            if (_lastProcessCount != osuProcesses.Length || _lastIsOsuRunning != isOsuRunning)
            {
                Logger.WriteLine(LogLevel.Information, "[OsuMonitorService] osu! process: {0}, running: {1}", osuProcesses.Length,
                                 isOsuRunning);
                _lastProcessCount = osuProcesses.Length;
                _lastIsOsuRunning = isOsuRunning;
            }

            // 更新全局状态
            stateBarManager.IsOsuRunning.Value = isOsuRunning;

            bool currentIsPlaying = isOsuRunning && IsPlaying();

            if (currentIsPlaying != _lastIsPlaying)
            {
                stateBarManager.IsPlaying.Value = currentIsPlaying;
                _lastIsPlaying = currentIsPlaying;
            }

            if (!isOsuRunning)
                _lastProcessId = -1;
            else
            {
                Process? selectedProcess = null;

                if (osuProcesses.Length == 1)
                    selectedProcess = osuProcesses[0];
                else
                {
                    var selectionWindow = new ProcessSelectionWindow(osuProcesses);
                    if (selectionWindow.ShowDialog() == true)
                        selectedProcess = selectionWindow.SelectedProcess;
                }

                if (selectedProcess != null) AutoSetSongsPath(selectedProcess);
            }
        }

        /// <summary>
        /// 设置Songs路径
        /// </summary>
        private void AutoSetSongsPath(Process selectedProcess)
        {
            if (selectedProcess.MainModule?.FileName is { } exePath)
            {
                string? osuDir = Path.GetDirectoryName(exePath);

                if (osuDir != null && !osuDir.ToUpper().Contains("SYSTEM32"))
                {
                    string songsPath = Path.Combine(osuDir, "Songs");

                    if (Directory.Exists(songsPath))
                    {
                        // 只有在进程变化时才重新设置songs路径
                        if (_lastProcessId != selectedProcess.Id &&
                            songsPath != BaseOptionsManager.GetGlobalSettings().SongsPath.Value)
                        {
                            BaseOptionsManager.GetGlobalSettings().SongsPath.Value = songsPath;
                            Logger.WriteLine(LogLevel.Information,
                                             "[OsuMonitorService] Client: osu!, Process ID: {0}, Loaded Songs Path: {1}",
                                             selectedProcess.Id, songsPath);
                        }

                        _lastProcessId = selectedProcess.Id;
                    }
                    else
                    {
                        Logger.WriteLine(LogLevel.Warning,
                                         "[OsuMonitorService] Songs path not found: {0}", songsPath);
                        // 路径设置失败的处理逻辑可以在这里添加
                    }
                }
            }
        }

        /// <summary>
        /// 读取内存数据
        /// </summary>
        public string ReadMemoryData()
        {
            if (reader == null) return string.Empty;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // if (_reader.ReadSongSelectGameMode() != 3) return string.Empty; // 这是监听F1选歌模式，不是选谱，所以使用的话体验差一点

                string? beatmapFile = reader.GetOsuFileName();
                string? mapFolderName = reader.GetMapFolderName();

                string path = Path.Combine(BaseOptionsManager.GetGlobalSettings().SongsPath.Value, mapFolderName,
                                           beatmapFile);

                // 只有当beatmap文件变化时才输出日志
                if (_lastBeatmapFile != path)
                {
                    Logger.WriteLine(LogLevel.Information, "[OsuMonitorService] ReadMemoryData , {0}ms: beatmapFile={1}",
                                     stopwatch.ElapsedMilliseconds, path);
                    _lastBeatmapFile = path;
                }

                _hasLoggedNotChanged = false;
                return path.Trim();
            }
            catch (Exception ex)
            {
                if (!_hasLoggedNotChanged)
                    Logger.WriteLine(LogLevel.Critical, "[OsuMonitorService] Beatmap has not changed : {0}", ex.Message);

                _hasLoggedNotChanged = true;

                return string.Empty;
            }
        }

        /// <summary>
        /// 检测是否在playing状态
        /// </summary>
        private bool IsPlaying()
        {
            if (reader == null) return false;

            try
            {
                // 假设playing模式是0，选歌是其他，体验目前是最佳的
                int playingMode = reader.ReadPlayedGameMode();
                // double p = _reader.ReadPlayerHp();

                Logger.WriteLine(LogLevel.Debug, "[OsuMonitorService] IsPlaying mode: {0}", playingMode);
                return playingMode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
