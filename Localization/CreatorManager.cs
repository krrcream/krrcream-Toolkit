using System;
using System.Collections.Generic;
using System.Linq;

namespace krrTools.Localization
{
    public abstract class CreatorManager
    {
        // 定义标签的固定顺序
        private static readonly List<string> TagOrder =
        [
            Strings.NToNCTag,
            Strings.KRRLNTag,
            Strings.DPTag
        ];

        public static string AddTagToCreator(string currentCreator, string newTag)
        {
            // 解析当前已有的标签和创作者
            var existingTags = new List<string>();
            string creator = "";

            if (!string.IsNullOrEmpty(currentCreator))
            {
                // 按空格分割字符串
                string[] parts = currentCreator.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // 检查第一个元素是否以"Krr"开头
                if (parts.Length > 0 && parts[0] == "Krr")
                {
                    // 从后向前查找"&"元素（最多检查到下标4）
                    int ampersandIndex = -1;
                    int searchLimit = Math.Min(parts.Length - 1, 4);

                    for (int i = parts.Length - 1; i >= Math.Max(1, parts.Length - 5); i--)
                    {
                        if (parts[i] == "&")
                        {
                            ampersandIndex = i;
                            break;
                        }
                    }

                    // 如果找到了"&"，并且位置合理
                    if (ampersandIndex > 0 && ampersandIndex < parts.Length - 1)
                    {
                        // 提取创作者（&之后的所有内容）
                        creator = string.Join(" ", parts, ampersandIndex + 1, parts.Length - ampersandIndex - 1);

                        // 验证中间部分是否都是有效的标签
                        bool isValidFormat = true;
                        var potentialTags = new List<string>();

                        for (int i = 1; i < ampersandIndex; i++)
                        {
                            string part = parts[i];

                            // 检查是否是有效的标签
                            if (TagOrder.Contains(part))
                                potentialTags.Add(part);
                            else
                            {
                                // 包含无效标签，说明整个字符串可能是原始字符串
                                isValidFormat = false;
                                break;
                            }
                        }

                        if (isValidFormat)
                            existingTags = potentialTags;
                        else
                            // 格式无效，将整个字符串视为创作者信息
                            creator = currentCreator;
                    }
                    else
                    {
                        // 没有找到合理的"&"分隔符，将整个字符串视为创作者信息
                        creator = currentCreator;
                    }
                }
                else
                {
                    // 不以"Krr"开头，将整个字符串视为创作者信息
                    creator = currentCreator;
                }
            }

            // 添加新标签（如果不在现有标签中）
            if (!existingTags.Contains(newTag)) existingTags.Add(newTag);

            // 按照预定义顺序排序标签
            List<string> orderedTags = existingTags.OrderBy(tag =>
            {
                int index = TagOrder.IndexOf(tag);
                return index == -1 ? int.MaxValue : index; // 未知标签放在最后
            }).ToList();

            // 重新构建标签字符串
            if (orderedTags.Count > 0)
            {
                string tagPart = "Krr " + string.Join(" ", orderedTags) + " & " + creator;
                return tagPart;
            }

            return "Krr & " + creator;
        }
    }
}
