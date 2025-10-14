using System;
using krrTools.Utilities;

namespace krrTools.Tests.Utilities;

public static class OsuMonitorServiceConsoleTest
{
    public static void RunTest()
    {
        var service = new OsuMonitorService();
        var result = service.ReadMemoryData();
        Console.WriteLine($"实际返回: {result}");
        Console.WriteLine($"是否绝对路径: {System.IO.Path.IsPathRooted(result)}");
    }
}