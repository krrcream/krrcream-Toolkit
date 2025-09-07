using System;
using System.Collections.Generic;

namespace krrTools.Tools.Converter
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class ConversionOptions
    {
        /// <summary>
        /// 目标键数
        /// </summary>
        public double TargetKeys { get; set; }

        /// <summary>
        /// 最大键数
        /// </summary>
        public double MaxKeys { get; set; }

        /// <summary>
        /// 最小键数
        /// </summary>
        public double MinKeys { get; set; }

        /// <summary>
        /// 变换速度
        /// </summary>
        public double TransformSpeed { get; set; }

        /// <summary>
        /// 种子值
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// 键数筛选器 - 选中的键数类型
        /// </summary>
        public List<int> SelectedKeyTypes { get; set; } = new List<int>();

        /// <summary>
        /// 构造函数
        /// </summary>
        public ConversionOptions()
        {
            SelectedKeyTypes = new List<int>();
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        /// <param name="targetKeys">目标键数</param>
        /// <param name="maxKeys">最大键数</param>
        /// <param name="minKeys">最小键数</param>
        /// <param name="transformSpeed">变换速度</param>
        public ConversionOptions(double targetKeys, double maxKeys, double minKeys, double transformSpeed)
        {
            TargetKeys = targetKeys;
            MaxKeys = maxKeys;
            MinKeys = minKeys;
            TransformSpeed = transformSpeed;
            SelectedKeyTypes = new List<int>();
        }
    }
}