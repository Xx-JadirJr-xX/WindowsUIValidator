using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using UIValidator.Core.Model;

namespace UIValidator.Core.Visual;

/// <summary>Result of comparing a screenshot against its baseline.</summary>
public sealed class ComparisonResult : IDisposable
{
    /// <summary>Mean structural similarity (SSIM) in 0..1; 1 means identical.</summary>
    public double Similarity { get; init; }

    /// <summary>Fraction of pixels whose difference exceeds the pixel tolerance.</summary>
    public double ChangedPixelRatio { get; init; }

    /// <summary>Bounding box of all changed pixels, or null when nothing changed.</summary>
    public Rect? ChangedRegion { get; init; }

    public bool SizeMismatch { get; init; }

    /// <summary>Current image dimmed in grayscale with changed pixels in red.</summary>
    public Bitmap? DiffImage { get; init; }

    public void Dispose() => DiffImage?.Dispose();
}

/// <summary>Pixel and structural (SSIM) comparison of two screenshots.</summary>
public static class ImageComparer
{
    private const int Block = 8;
    private const double C1 = 6.5025;   // (0.01 * 255)^2
    private const double C2 = 58.5225;  // (0.03 * 255)^2

    /// <param name="masks">Regions (image coordinates) excluded from the comparison.</param>
    public static ComparisonResult Compare(Bitmap current, Bitmap baseline, int pixelTolerance, IReadOnlyList<Rect>? masks = null)
    {
        if (current.Width != baseline.Width || current.Height != baseline.Height)
            return new ComparisonResult { Similarity = 0, ChangedPixelRatio = 1, SizeMismatch = true };

        int w = current.Width, h = current.Height;
        var a = ReadPixels(current);
        var b = ReadPixels(baseline);
        var masked = BuildMask(w, h, masks);

        var diff = new int[a.Length];
        var grayA = new double[w * h];
        var grayB = new double[w * h];
        int changed = 0, minX = w, minY = h, maxX = -1, maxY = -1, compared = 0;

        for (var i = 0; i < w * h; i++)
        {
            int pa = a[i], pb = b[i];
            int ra = (pa >> 16) & 255, ga = (pa >> 8) & 255, ba = pa & 255;
            int rb = (pb >> 16) & 255, gb = (pb >> 8) & 255, bb = pb & 255;
            grayA[i] = 0.299 * ra + 0.587 * ga + 0.114 * ba;
            grayB[i] = masked[i] ? grayA[i] : 0.299 * rb + 0.587 * gb + 0.114 * bb;

            var dimmed = (int)(grayA[i] * 0.35 + 255 * 0.55);
            diff[i] = unchecked((int)0xFF000000) | (dimmed << 16) | (dimmed << 8) | dimmed;
            if (masked[i])
            {
                diff[i] = unchecked((int)0xFF9FB4D9); // Masked areas in light blue.
                continue;
            }

            compared++;
            var delta = Math.Max(Math.Abs(ra - rb), Math.Max(Math.Abs(ga - gb), Math.Abs(ba - bb)));
            if (delta <= pixelTolerance) continue;

            changed++;
            diff[i] = unchecked((int)0xFFE02020);
            int x = i % w, y = i / w;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        return new ComparisonResult
        {
            Similarity = Ssim(grayA, grayB, w, h),
            ChangedPixelRatio = compared == 0 ? 0 : (double)changed / compared,
            ChangedRegion = changed == 0 ? null : Rect.FromLTRB(minX, minY, maxX + 1, maxY + 1),
            DiffImage = WritePixels(diff, w, h),
        };
    }

    /// <summary>Mean SSIM over non-overlapping 8x8 blocks of the luminance channel.</summary>
    public static double Ssim(double[] a, double[] b, int w, int h)
    {
        double total = 0;
        var blocks = 0;
        for (var by = 0; by + Block <= h; by += Block)
            for (var bx = 0; bx + Block <= w; bx += Block)
            {
                double ma = 0, mb = 0;
                for (var y = by; y < by + Block; y++)
                    for (var x = bx; x < bx + Block; x++)
                    {
                        ma += a[y * w + x];
                        mb += b[y * w + x];
                    }
                const int n = Block * Block;
                ma /= n;
                mb /= n;

                double va = 0, vb = 0, cov = 0;
                for (var y = by; y < by + Block; y++)
                    for (var x = bx; x < bx + Block; x++)
                    {
                        double da = a[y * w + x] - ma, db = b[y * w + x] - mb;
                        va += da * da;
                        vb += db * db;
                        cov += da * db;
                    }
                va /= n - 1;
                vb /= n - 1;
                cov /= n - 1;

                total += (2 * ma * mb + C1) * (2 * cov + C2) / ((ma * ma + mb * mb + C1) * (va + vb + C2));
                blocks++;
            }
        return blocks == 0 ? 1.0 : total / blocks;
    }

    private static bool[] BuildMask(int w, int h, IReadOnlyList<Rect>? masks)
    {
        var mask = new bool[w * h];
        if (masks is null) return mask;
        foreach (var m in masks)
        {
            var r = m.Intersect(new Rect(0, 0, w, h));
            for (var y = r.Y; y < r.Bottom; y++)
                Array.Fill(mask, true, y * w + r.X, r.Width);
        }
        return mask;
    }

    private static int[] ReadPixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new int[bmp.Width * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bmp.Width, bmp.Width);
            return pixels;
        }
        finally { bmp.UnlockBits(data); }
    }

    private static Bitmap WritePixels(int[] pixels, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < h; y++)
                Marshal.Copy(pixels, y * w, data.Scan0 + y * data.Stride, w);
        }
        finally { bmp.UnlockBits(data); }
        return bmp;
    }
}
