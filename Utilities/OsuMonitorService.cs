using System;
using System.Diagnostics;
using System.IO;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Tools.Listener;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;

#pragma warning disable CS0618 // 禁止提示

namespace krrTools.Utilities;

/// <summary>
/// OSU监听服务 - 负责进程检测、内存读取和文件监控
/// </summary>
public class OsuMonitorService
{
    private int _lastProcessId = -1;
    private readonly IOsuMemoryReader _reader;
    private int _lastProcessCount = -1;
    private bool _lastIsOsuRunning = false;
    private string _lastBeatmapFile = string.Empty;

    [Inject]
    private StateBarManager StateBarManager { get; set; } = null!;

    public OsuMonitorService()
    {
#pragma warning disable CS0618 // 必须构建初始化，否则有高延迟问题
        _reader = OsuMemoryReader.Instance;
#pragma warning restore CS0618 // Type or member is obsolete

        // 注入服务
        this.InjectServices();
    }

    /// <summary>
    /// 检测OSU进程并返回选中的进程
    /// </summary>
    public void DetectOsuProcess()
    {
        var osuProcesses = Process.GetProcessesByName("osu!");
        var isOsuRunning = osuProcesses.Length > 0;

        // 只有当状态变化时才输出日志
        if (_lastProcessCount != osuProcesses.Length || _lastIsOsuRunning != isOsuRunning)
        {
            Console.WriteLine($"[OsuMonitorService] osu!进程数: {osuProcesses.Length}, 运行: {isOsuRunning}");
            _lastProcessCount = osuProcesses.Length;
            _lastIsOsuRunning = isOsuRunning;
        }

        // 更新全局状态
        StateBarManager.IsOsuRunning.Value = isOsuRunning;

        if (!isOsuRunning)
        {
            _lastProcessId = -1;
        }
        else
        {
            Process? selectedProcess = null;

            if (osuProcesses.Length == 1)
            {
                selectedProcess = osuProcesses[0];
            }
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
            var osuDir = Path.GetDirectoryName(exePath);
            if (osuDir != null && !osuDir.ToUpper().Contains("SYSTEM32"))
            {
                var songsPath = Path.Combine(osuDir, "Songs");
                if (Directory.Exists(songsPath))
                {
                    // 只有在进程变化时才重新设置songs路径
                    if (_lastProcessId != selectedProcess.Id &&
                        songsPath != BaseOptionsManager.GetGlobalSettings().SongsPath.Value)
                    {
                        BaseOptionsManager.GetGlobalSettings().SongsPath.Value = songsPath;
                        Console.WriteLine("[OsuMonitorService] 客户端: osu!, 进程ID: {0}, 加载Songs路径: {1}",
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
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (_reader.ReadSongSelectGameMode() != 3) return string.Empty; // 3 = 观看录像模式，跳过

            var beatmapFile = _reader.GetOsuFileName();
            var mapFolderName = _reader.GetMapFolderName();

            var path = Path.Combine(BaseOptionsManager.GetGlobalSettings().SongsPath.Value, mapFolderName, beatmapFile);

            // 只有当beatmap文件变化时才输出日志
            if (_lastBeatmapFile != path)
            {
                Console.WriteLine($"[OsuMonitorService] 内存读取成功,用时{stopwatch.ElapsedMilliseconds}ms: beatmapFile={path}");
                _lastBeatmapFile = path;
            }

            return path.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OsuMonitorService] 未能读取内存数据: {ex.Message}");
            return string.Empty;
        }
    }
}