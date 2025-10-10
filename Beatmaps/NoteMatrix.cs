using System;
using System.Runtime.InteropServices;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// 谱面音符矩阵封装类，用于键位转换算法
    /// </summary>
    public class NoteMatrix
    {
        private readonly int[,] _data;

        /// <summary>
        /// 空位置常量
        /// </summary>
        public const int Empty = -1;

        /// <summary>
        /// 长音符身体常量
        /// </summary>
        public const int HoldBody = -7;

        /// <summary>
        /// 行数
        /// </summary>
        public int Rows => _data.GetLength(0);

        /// <summary>
        /// 列数
        /// </summary>
        public int Cols => _data.GetLength(1);

        /// <summary>
        /// 构造函数
        /// </summary>
        public NoteMatrix(int rows, int cols)
        {
            _data = new int[rows, cols];
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                _data[i, j] = Empty;
        }

        /// <summary>
        /// 从现有数组构造
        /// </summary>
        public NoteMatrix(int[,] data)
        {
            _data = (int[,])data.Clone();
        }

        /// <summary>
        /// 索引器
        /// </summary>
        public int this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new IndexOutOfRangeException($"Index out of range: row={row}, col={col}");
                return _data[row, col];
            }
            set
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new IndexOutOfRangeException($"Index out of range: row={row}, col={col}");
                _data[row, col] = value;
            }
        }

        /// <summary>
        /// 获取内部数组（用于兼容现有代码）
        /// </summary>
        public int[,] GetData()
        {
            return _data;
        }

        /// <summary>
        /// 复制数据到另一个矩阵
        /// </summary>
        public void CopyTo(NoteMatrix target)
        {
            if (Rows != target.Rows || Cols != target.Cols)
                throw new ArgumentException("Matrix dimensions must match");
            Array.Copy(_data, target._data, _data.Length);
        }

        /// <summary>
        /// 从另一个矩阵复制数据
        /// </summary>
        public void CopyFrom(NoteMatrix source)
        {
            source.CopyTo(this);
        }

        /// <summary>
        /// 获取行数据作为 Span（用于 MemoryMarshal）
        /// </summary>
        public Span<int> GetRowSpan(int row)
        {
            if (row < 0 || row >= Rows)
                throw new IndexOutOfRangeException($"Row index out of range: {row}");
            return MemoryMarshal.CreateSpan(ref _data[row, 0], Cols);
        }

        /// <summary>
        /// 克隆矩阵
        /// </summary>
        public NoteMatrix Clone()
        {
            var clone = new NoteMatrix(Rows, Cols);
            CopyTo(clone);
            return clone;
        }
    }

}