using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Diagnostics;

namespace krrTools.tools.Shared
{
    public static class OptionsManager
    {
        // base application folder under LocalAppData
        public const string BaseAppFolderName = "krrTools";

        // common filenames and folder names
        public const string PresetsFolderName = "presets";
        public const string OptionsFileName = "options.json";

        // tool identifiers
        public const string DPToolName = "DPTool";
        public const string ConverterToolName = "Converter";
        public const string LNToolName = "LNTransformer";

        // Add tool identifiers for simple tabs
        public const string LVToolName = "LV";
        public const string GetFilesToolName = "GetFiles";

        // DP specific constants
        public const string DPCreatorPrefix = "Krr DP. & ";
        public const string DPDefaultTag = "krrcream's converter DP";

        private static string BaseFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BaseAppFolderName);

        private static string GetToolFolder(string toolName)
        {
            var folder = Path.Combine(BaseFolder, toolName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static void SaveOptions<T>(string toolName, string filename, T options)
        {
            string folder = GetToolFolder(toolName);
            string path = Path.Combine(folder, filename);
            var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string json = JsonSerializer.Serialize(options, opts);
            File.WriteAllText(path, json);
        }

        public static T? LoadOptions<T>(string toolName, string filename)
        {
            string folder = GetToolFolder(toolName);
            string path = Path.Combine(folder, filename);
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            try
            {
                return JsonSerializer.Deserialize<T>(json, opts);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to deserialize options file '{path}': {ex.Message}");
                return default;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to read options file '{path}': {ex.Message}");
                return default;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Unauthorized accessing options file '{path}': {ex.Message}");
                return default;
            }
        }

        // Preset helpers: save preset by name (filename-safe) and list available presets
        public static void SavePreset<T>(string toolName, string presetName, T options)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string json = JsonSerializer.Serialize(options, opts);
            File.WriteAllText(path, json);
        }

        public static IEnumerable<(string Name, T? Options)> LoadPresets<T>(string toolName)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            if (!Directory.Exists(folder)) yield break;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name)) continue; // Skip presets with empty names
                T? opt = default;
                try
                {
                    string json = File.ReadAllText(file);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    opt = JsonSerializer.Deserialize<T>(json, opts);
                }
                catch (Exception ex)
                {
                    // Swallow malformed preset but log for diagnostics
                    Debug.WriteLine($"Failed to load preset '{file}': {ex.Message}");
                }
                yield return (name, opt);
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}