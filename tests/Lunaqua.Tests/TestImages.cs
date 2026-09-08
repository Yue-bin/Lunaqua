using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace Lunaqua.Tests;

/// <summary>渲染帧的像素级检查（用来代替人眼判断「有没有真的画出东西」）。</summary>
internal static class TestImages
{
    /// <summary>统计帧里的不同颜色数；空白帧只有 1 种颜色。</summary>
    public static int CountDistinctColors(WriteableBitmap bitmap)
    {
        using var buffer = bitmap.Lock();
        var stride = buffer.RowBytes;
        var height = buffer.Size.Height;
        var width = buffer.Size.Width;

        var pixels = new byte[stride * height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

        var colors = new HashSet<uint>();
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            for (var x = 0; x < width; x++)
            {
                colors.Add(BitConverter.ToUInt32(pixels, rowStart + (x * 4)));
            }
        }

        return colors.Count;
    }
}
