namespace krrTools.Localization;

public static class LocalizationExtensions
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> _cache = new();

    /// <summary>
    /// 拓展方法，xxx.Localize()
    /// </summary>
    public static string Localize(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        if (!_cache.TryGetValue(s, out var parts))
        {
            parts = s.Split(['|'], 2);
            _cache[s] = parts;
        }

        // Use centralized Strings management; no try-catch for localization
        return LocalizationManager.IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
    }
}