param(
    [Parameter(Mandatory = $true)][string]$InputImage,
    [Parameter(Mandatory = $true)][string]$OutputPng,
    [Parameter(Mandatory = $true)][string]$OutputIco
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class DinoIconProcessor
{
    public static void Process(string inputPath, string outputPng, string outputIco)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPng));
        using (var source = new Bitmap(inputPath))
        using (var transparent = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(transparent)) graphics.DrawImageUnscaled(source, 0, 0);
            RemoveMagenta(transparent);
            transparent.Save(outputPng, ImageFormat.Png);
            WriteIco(transparent, outputIco, new[] { 16, 24, 32, 48, 64, 128, 256 });
        }
    }

    private static void RemoveMagenta(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(data.Stride) * data.Height;
        var pixels = new byte[bytes]; Marshal.Copy(data.Scan0, pixels, 0, bytes);
        byte keyB = pixels[0], keyG = pixels[1], keyR = pixels[2];
        const double clearDistance = 14.0, opaqueDistance = 105.0;
        for (int y = 0; y < data.Height; y++)
        for (int x = 0; x < data.Width; x++)
        {
            int i = y * data.Stride + x * 4;
            double db = pixels[i] - keyB, dg = pixels[i + 1] - keyG, dr = pixels[i + 2] - keyR;
            double distance = Math.Sqrt(db * db + dg * dg + dr * dr);
            if (distance <= clearDistance) { pixels[i + 3] = 0; continue; }
            if (distance >= opaqueDistance) { pixels[i + 3] = 255; continue; }
            double alpha = (distance - clearDistance) / (opaqueDistance - clearDistance);
            alpha = alpha * alpha * (3.0 - 2.0 * alpha);
            if (alpha > 0.03)
            {
                pixels[i] = Clamp((pixels[i] - (1.0 - alpha) * keyB) / alpha);
                pixels[i + 1] = Clamp((pixels[i + 1] - (1.0 - alpha) * keyG) / alpha);
                pixels[i + 2] = Clamp((pixels[i + 2] - (1.0 - alpha) * keyR) / alpha);
            }
            pixels[i + 3] = (byte)Math.Round(255 * alpha);
        }
        Marshal.Copy(pixels, 0, data.Scan0, bytes); bitmap.UnlockBits(data);
    }

    private static void WriteIco(Bitmap source, string path, int[] sizes)
    {
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (var resized = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(resized))
            using (var stream = new MemoryStream())
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, size, size));
                resized.Save(stream, ImageFormat.Png); images.Add(stream.ToArray());
            }
        }

        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)images.Count);
            int offset = 6 + images.Count * 16;
            for (int i = 0; i < images.Count; i++)
            {
                int size = sizes[i]; writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (var image in images) writer.Write(image);
        }
    }

    private static byte Clamp(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value))); }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
[DinoIconProcessor]::Process(
    (Resolve-Path -LiteralPath $InputImage).Path,
    [System.IO.Path]::GetFullPath($OutputPng),
    [System.IO.Path]::GetFullPath($OutputIco))
