using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// Beatmap 文件缓存管理器，用于避免重复加载和处理
    /// </summary>
    public class BeatmapCacheManager
    {
        private readonly ConcurrentDictionary<string, (DateTime lastProcessTime, string contentHash)> _processedFiles =
            new ConcurrentDictionary<string, (DateTime lastProcessTime, string contentHash)>();

        /// <summary>
        /// 检查是否可以处理文件（避免重复处理）
        /// </summary>
        public bool CanProcessFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            DateTime now = DateTime.Now;
            // var fileInfo = new FileInfo(filePath);
            string contentHash = GetFileHash(filePath);

            if (_processedFiles.TryGetValue(filePath, out (DateTime lastProcessTime, string contentHash) cached))
            {
                // 检查时间窗口和内容哈希
                if (cached.contentHash == contentHash) return false; // 重复处理，跳过
            }

            // 更新缓存
            _processedFiles[filePath] = (now, contentHash);
            return true;
        }

        /// <summary>
        /// 获取文件的简单哈希（基于修改时间和大小）
        /// </summary>
        private string GetFileHash(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                return $"{fileInfo.LastWriteTimeUtc.Ticks}_{fileInfo.Length}";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 清理过期缓存
        /// </summary>
        public void CleanupExpiredEntries()
        {
            DateTime now = DateTime.Now;
            var expiredKeys = new List<string>();

            foreach (KeyValuePair<string, (DateTime lastProcessTime, string contentHash)> kvp in _processedFiles)
            {
                if (now - kvp.Value.lastProcessTime > TimeSpan.FromMinutes(5)) // 5分钟过期
                    expiredKeys.Add(kvp.Key);
            }

            foreach (string key in expiredKeys) _processedFiles.TryRemove(key, out _);
        }
    }
}
