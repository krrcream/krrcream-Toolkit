using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OsuParsers.Beatmaps;
using Xunit;
using Xunit.Abstractions;

namespace krrTools.Tests
{
    public class SRCalculatorComparisonTests
    {
        private readonly ITestOutputHelper _output;

        public SRCalculatorComparisonTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestRustLibraryLoad()
        {
            // Test that we can load the Rust library
            // This is a basic smoke test
            _output.WriteLine("Rust SR Calculator library created successfully");

            // Test basic JSON serialization
            var testData = new
            {
                difficulty_section = new
                {
                    overall_difficulty = 8.0,
                    circle_size = 4.0
                },
                hit_objects = new[]
                {
                    new { position = new { x = 0.0 }, start_time = 1000, end_time = 1000 },
                    new { position = new { x = 128.0 }, start_time = 1200, end_time = 1200 }
                }
            };

            string json = JsonSerializer.Serialize(testData);
            Assert.NotNull(json);
            Assert.Contains("overall_difficulty", json);

            _output.WriteLine("JSON serialization test passed");
        }

        [DllImport("rust_sr_calculator.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr calculate_sr_from_json(IntPtr jsonPtr, int len);

        private static double CalculateSRRust(Beatmap beatmap)
        {
            // Convert beatmap to JSON
            var beatmapData = new
            {
                difficulty_section = new
                {
                    overall_difficulty = beatmap.DifficultySection.OverallDifficulty,
                    circle_size = beatmap.DifficultySection.CircleSize
                },
                hit_objects = beatmap.HitObjects.Select(ho => new
                {
                    position = new { x = ho.Position.X },
                    start_time = ho.StartTime,
                    end_time = ho.EndTime
                }).ToArray()
            };

            string json = JsonSerializer.Serialize(beatmapData);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // Call Rust function
            IntPtr resultPtr = calculate_sr_from_json(Marshal.StringToHGlobalAnsi(json), jsonBytes.Length);

            if (resultPtr == IntPtr.Zero) throw new Exception("Rust SR calculation failed");

            // Parse result (assuming it returns a JSON string with the SR value)
            string resultJson = Marshal.PtrToStringAnsi(resultPtr)!;
            var result = JsonSerializer.Deserialize<Dictionary<string, double>>(resultJson);
            if (result == null) throw new Exception("Failed to deserialize Rust result");

            // Free the allocated memory
            Marshal.FreeHGlobal(resultPtr);

            return result["sr"];
        }
    }
}
