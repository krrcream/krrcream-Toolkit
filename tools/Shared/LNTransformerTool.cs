using System.Threading.Tasks;
using krrTools.tools.LNTransformer;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// LNTransformer工具包装器
    /// </summary>
    public class LNTransformerTool : ITool
    {
        public string Name => OptionsManager.LNToolName;

        public IToolOptions DefaultOptions => new LNTransformerOptions();

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not LNTransformerOptions lnOptions)
                return null;

            return TransformService.ProcessSingleFile(filePath, lnOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }
    }
}