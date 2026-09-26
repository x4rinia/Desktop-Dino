param(
    [Parameter(Mandatory = $true)][string]$InputAtlas,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [ValidateSet('Core','Extra')][string]$Mode = 'Core'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class DinoSpriteProcessor
{
    private sealed class Cell
    {
        public int Column; public int Row; public string RelativePath;
        public Cell(int column, int row, string relativePath) { Column = column; Row = row; RelativePath = relativePath; }
    }

    public static void Process(string inputPath, string outputRoot)
    {
        using (var source = new Bitmap(inputPath))
        using (var transparent = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(transparent)) graphics.DrawImageUnscaled(source, 0, 0);
            RemoveMagenta(transparent);
            Directory.CreateDirectory(outputRoot);
            transparent.Save(Path.Combine(outputRoot, "dino-atlas.png"), ImageFormat.Png);

            var cells = new List<Cell> {
                new Cell(0, 0, @"Idle\idle-01.png"),
                new Cell(1, 0, @"Walk\walk-01.png"),
                new Cell(2, 0, @"Walk\walk-02.png"),
                new Cell(3, 0, @"Sit\sit-01.png"),
                new Cell(0, 1, @"Sleep\sleep-01.png"),
                new Cell(1, 1, @"Wake\wake-01.png"),
                new Cell(2, 1, @"Happy\happy-01.png"),
                new Cell(3, 1, @"Curious\curious-01.png")
            };

            foreach (var cell in cells)
            {
                int left = (int)Math.Round(cell.Column * transparent.Width / 4.0);
                int right = (int)Math.Round((cell.Column + 1) * transparent.Width / 4.0);
                int top = (int)Math.Round(cell.Row * transparent.Height / 2.0);
                int bottom = (int)Math.Round((cell.Row + 1) * transparent.Height / 2.0);
                using (var frame = transparent.Clone(new Rectangle(left, top, right - left, bottom - top), PixelFormat.Format32bppArgb))
                {
                    KeepLargestAlphaComponent(frame);
                    string output = Path.Combine(outputRoot, cell.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    frame.Save(output, ImageFormat.Png);
                }
            }
        }
    }

    public static void ProcessExtra(string inputPath, string outputRoot)
    {
        using (var source = new Bitmap(inputPath))
        using (var transparent = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(transparent)) graphics.DrawImageUnscaled(source, 0, 0);
            RemoveMagenta(transparent);
            var cells = new List<Cell> {
                new Cell(0, 0, @"Blink\blink-closed.png"),
                new Cell(1, 0, @"TailWag\tail-left.png"),
                new Cell(2, 0, @"TailWag\tail-right.png"),
                new Cell(3, 0, @"WeightShift\weight-shift.png")
            };
            foreach (var cell in cells)
            {
                int left = (int)Math.Round(cell.Column * transparent.Width / 4.0);
                int right = (int)Math.Round((cell.Column + 1) * transparent.Width / 4.0);
                using (var frame = transparent.Clone(new Rectangle(left, 0, right - left, transparent.Height), PixelFormat.Format32bppArgb))
                {
                    KeepLargestAlphaComponent(frame);
                    string output = Path.Combine(outputRoot, cell.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    frame.Save(output, ImageFormat.Png);
                }
            }
        }
    }

    private static void KeepLargestAlphaComponent(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(data.Stride) * data.Height;
        var pixels = new byte[bytes];
        Marshal.Copy(data.Scan0, pixels, 0, bytes);
        int width = bitmap.Width, height = bitmap.Height;
        var visited = new bool[width * height];
        List<int> largest = new List<int>();
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int start = y * width + x;
            if (visited[start] || pixels[y * data.Stride + x * 4 + 3] <= 15) continue;
            var component = new List<int>();
            var queue = new Queue<int>();
            visited[start] = true; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); component.Add(current);
                int cx = current % width, cy = current / width;
                for (int n = 0; n < 8; n++)
                {
                    int nx = cx + dx[n], ny = cy + dy[n];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (visited[next] || pixels[ny * data.Stride + nx * 4 + 3] <= 15) continue;
                    visited[next] = true; queue.Enqueue(next);
                }
            }
            if (component.Count > largest.Count) largest = component;
        }

        var keep = new bool[width * height];
        foreach (int pixel in largest) keep[pixel] = true;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (!keep[y * width + x]) pixels[y * data.Stride + x * 4 + 3] = 0;
        Marshal.Copy(pixels, 0, data.Scan0, bytes);
        bitmap.UnlockBits(data);
    }

    private static void RemoveMagenta(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(data.Stride) * data.Height;
        var pixels = new byte[bytes];
        Marshal.Copy(data.Scan0, pixels, 0, bytes);

        byte keyB = pixels[0], keyG = pixels[1], keyR = pixels[2];
        const double clearDistance = 14.0;
        const double opaqueDistance = 105.0;
        for (int y = 0; y < data.Height; y++)
        {
            int row = y * data.Stride;
            for (int x = 0; x < data.Width; x++)
            {
                int index = row + x * 4;
                double db = pixels[index] - keyB, dg = pixels[index + 1] - keyG, dr = pixels[index + 2] - keyR;
                double distance = Math.Sqrt(db * db + dg * dg + dr * dr);
                if (distance <= clearDistance) { pixels[index + 3] = 0; continue; }
                if (distance >= opaqueDistance) { pixels[index + 3] = 255; continue; }

                double alpha = (distance - clearDistance) / (opaqueDistance - clearDistance);
                alpha = alpha * alpha * (3.0 - 2.0 * alpha);
                if (alpha > 0.03)
                {
                    pixels[index] = Clamp((pixels[index] - (1.0 - alpha) * keyB) / alpha);
                    pixels[index + 1] = Clamp((pixels[index + 1] - (1.0 - alpha) * keyG) / alpha);
                    pixels[index + 2] = Clamp((pixels[index + 2] - (1.0 - alpha) * keyR) / alpha);
                }
                pixels[index + 3] = (byte)Math.Round(255 * alpha);
            }
        }
        Marshal.Copy(pixels, 0, data.Scan0, bytes);
        bitmap.UnlockBits(data);
    }

    private static byte Clamp(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value))); }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
if ($Mode -eq 'Extra') {
    [DinoSpriteProcessor]::ProcessExtra((Resolve-Path -LiteralPath $InputAtlas).Path, [System.IO.Path]::GetFullPath($OutputRoot))
} else {
    [DinoSpriteProcessor]::Process((Resolve-Path -LiteralPath $InputAtlas).Path, [System.IO.Path]::GetFullPath($OutputRoot))
}
