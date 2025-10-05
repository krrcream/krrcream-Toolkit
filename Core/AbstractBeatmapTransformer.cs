using System.Collections.Generic;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Core
{
    // 谱面转换抽象类
    public abstract class AbstractBeatmapTransformer<TOptions> where TOptions : class
    {
        public Beatmap ProcessFile(string filePath, TOptions options)
        {
            var beatmap = LoadManiaBeatmap(filePath);
            ProcessBeatmap(beatmap, options);
            // ModifyMetadata(beatmap, options);
            return beatmap;
        }

        protected Beatmap ProcessBeatmapToData(Beatmap beatmap, TOptions options)
        {
            ProcessBeatmap(beatmap, options);
            // ModifyMetadata(beatmap, options);
            return beatmap;
        }

        protected void SaveBeatmap(Beatmap beatmap, TOptions options, string originalPath)
        {
            ModifyMetadata(beatmap, options); //我发现所有的保存都没经过ModifyMetadta，导致新的谱子metadeta和原始铺面一样。然后save不按照标准格式保存，而是+"_N2NC" + "_KRRLN"的形式?
        }
        
        protected virtual Beatmap LoadManiaBeatmap(string filePath)
        {
            var maniaBeatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap(filePath);
            
            return maniaBeatmap;
        }

        private void ProcessBeatmap(Beatmap beatmap, TOptions options)
        {
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options);
        }

        protected abstract int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, TOptions options);

        protected abstract void ApplyChangesToHitObjects(Beatmap beatmap, int[,] processedMatrix, TOptions options);

        protected abstract void ModifyMetadata(Beatmap beatmap, TOptions options);
    }
}