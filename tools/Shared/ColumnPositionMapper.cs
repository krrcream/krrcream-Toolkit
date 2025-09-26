﻿using System;
using System.Collections.Generic;

namespace krrTools.tools.Shared;

public static class ColumnPositionMapper
{
    private static readonly Dictionary<int, int[]> KeyValueMap = new Dictionary<int, int[]>
    {
        [1] = [256],
        [2] = [128, 384],
        [3] = [85, 256, 426],
        [4] = [64, 192, 320, 448],
        [5] = [51, 153, 256, 358, 460],
        [6] = [42, 128, 213, 298, 384, 469],
        [7] = [36, 109, 182, 256, 329, 402, 475],
        [8] = [32, 96, 160, 224, 288, 352, 416, 480],
        [9] = [28, 85, 142, 199, 256, 312, 369, 426, 483],
        [10] = [25, 76, 128, 179, 230, 281, 332, 384, 435, 486],
        [11] = [23, 69, 116, 162, 209, 256, 302, 349, 395, 442, 488],
        [12] = [21, 64, 106, 149, 192, 234, 277, 320, 362, 405, 448, 490],
        [13] = [19, 59, 98, 137, 177, 256, 216, 295, 334, 374, 413, 452, 492],
        [14] = [18, 54, 91, 128, 164, 201, 237, 274, 310, 347, 384, 420, 457, 493],
        [15] = [17, 51, 85, 119, 153, 187, 221, 256, 290, 324, 358, 392, 426, 460, 494],
        [16] = [16, 48, 80, 112, 144, 176, 240, 208, 272, 304, 336, 368, 400, 432, 464, 496],
        [17] = [15, 45, 75, 135, 165, 105, 195, 225, 316, 286, 256, 376, 346, 406, 436, 466, 496],
        [18] = [16, 48, 80, 112, 128, 144, 176, 208, 240, 272, 304, 336, 368, 384, 400, 432, 464, 496],
        [19] = [13, 39, 66, 93, 120, 147, 174, 201, 228, 255, 282, 309, 336, 363, 390, 417, 444, 471, 498],
        [20] = [12, 37, 63, 88, 114, 140, 165, 191, 216, 242, 268, 293, 319, 344, 370, 396, 421, 447, 472, 498],
        [21] = [12, 36, 60, 85, 109, 133, 158, 182, 207, 231, 255, 280, 304, 328, 353, 377, 402, 426, 450, 475, 499],
        [22] = [11, 34, 57, 80, 104, 127, 150, 173, 197, 220, 243, 267, 290, 313, 336, 360, 383, 406, 429, 453, 476, 499],
        [23] = [11, 33, 55, 77, 100, 122, 144, 166, 189, 211, 233, 255, 278, 300, 322, 344, 367, 389, 411, 433, 456, 478, 500],
        [24] = [10, 31, 52, 74, 95, 116, 138, 159, 180, 202, 223, 244, 266, 287, 308, 330, 351, 372, 394, 415, 436, 458, 479, 500]
    };

    // Expose a read-only view in case callers want the whole map
    public static IReadOnlyDictionary<int, int[]> Map => KeyValueMap;

    public static int ColumnToPositionX(int keyCount, int column)
    {
        if (!KeyValueMap.TryGetValue(keyCount, out var row))
        {
            throw new ArgumentOutOfRangeException(nameof(keyCount), $"Unsupported key count: {keyCount}");
        }
        if (column < 0 || column >= row.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(column), $"Column index out of range for keyCount {keyCount}: {column}");
        }
        return row[column];
    }
    
    public static int GetPositionX(int base_cs, int set_x)
    {
        set_x = ((set_x - 1) * 512 / base_cs) + (256 / base_cs);
        return set_x;
    }
}