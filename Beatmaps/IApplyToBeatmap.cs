namespace krrTools.Beatmaps
{
    /// <summary>
    /// 谱面应用接口 - 负责将转换应用到谱面
    /// </summary>
    public interface IApplyToBeatmap
    {
        /// <summary>
        /// 应用转换到谱面（实现类内部获取设置）
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        void ApplyToBeatmap(IBeatmap beatmap);
    }
}