using System.Threading.Tasks;
using krrTools.tools.N2NC;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// N2NC工具包装器
    /// </summary>
    public class N2NCTool : ITool
    {
        public string Name => OptionsManager.ConverterToolName;

        public IToolOptions DefaultOptions => new N2NCOptions();

        public string? ProcessFile(string filePath, IToolOptions options)
        {
            if (options is not N2NCOptions n2ncOptions)
                return null;

            return N2NCService.ProcessSingleFile(filePath, n2ncOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath, IToolOptions options)
        {
            return await Task.Run(() => ProcessFile(filePath, options));
        }
    }
}