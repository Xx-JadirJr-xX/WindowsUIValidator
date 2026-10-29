using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using UIValidator.Core.Model;
using UIValidator.Core.Windowing;
using static UIValidator.Core.Native.NativeMethods;

namespace UIValidator.Core.Capture;

/// <summary>Captures screenshots of a window's visible bounds.</summary>
public static class WindowCapture
{
    /// <summary>
    /// Captures the window using <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, which works even when the
    /// window is partially covered, and crops the result to the visible (DWM) bounds.
    /// Falls back to a screen copy when PrintWindow produces a blank image.
    /// </summary>
    public static Bitmap Capture(IntPtr hwnd, out Rect visibleBounds)
    {
        var raw = WindowManager.GetWindowRect(hwnd);
        visibleBounds = WindowManager.GetVisibleBounds(hwnd);

        using var full = new Bitmap(Math.Max(1, raw.Width), Math.Max(1, raw.Height), PixelFormat.Format32bppArgb);
        var ok = false;
        using (var g = Graphics.FromImage(full))
        {
            var hdc = g.GetHdc();
            try { ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT); }
            finally { g.ReleaseHdc(hdc); }
        }

        var crop = visibleBounds.Offset(-raw.X, -raw.Y).Intersect(new Rect(0, 0, full.Width, full.Height));
        if (ok && !crop.IsEmpty && !IsBlank(full, crop))
            return full.Clone(crop.ToDrawing(), PixelFormat.Format32bppArgb);

        return CaptureScreen(visibleBounds);
    }

    /// <summary>Copies a region of the screen.</summary>
    public static Bitmap CaptureScreen(Rect region)
    {
        var bmp = new Bitmap(Math.Max(1, region.Width), Math.Max(1, region.Height), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.X, region.Y, 0, 0, new Size(region.Width, region.Height));
        return bmp;
    }

    /// <summary>
    /// Waits until two consecutive captures are identical (animations finished) or the timeout expires.
    /// Returns the last capture.
    /// </summary>
    public static Bitmap CaptureStable(IntPtr hwnd, out Rect visibleBounds, int timeoutMs = 3000, int intervalMs = 200)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        var current = Capture(hwnd, out visibleBounds);
        var hash = Hash(current);
        while (Environment.TickCount64 < deadline)
        {
            Thread.Sleep(intervalMs);
            var next = Capture(hwnd, out var nextBounds);
            var nextHash = Hash(next);
            current.Dispose();
            current = next;
            visibleBounds = nextBounds;
            if (nextHash == hash) break;
            hash = nextHash;
        }
        return current;
    }

    public static string Hash(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA1.HashData(bytes)) + $":{bmp.Width}x{bmp.Height}";
        }
        finally { bmp.UnlockBits(data); }
    }

    private static bool IsBlank(Bitmap bmp, Rect region)
    {
        // Sample a grid; a fully black or fully transparent capture means PrintWindow failed.
        var first = bmp.GetPixel(region.X, region.Y).ToArgb();
        for (var y = region.Y; y < region.Bottom; y += Math.Max(1, region.Height / 16))
            for (var x = region.X; x < region.Right; x += Math.Max(1, region.Width / 16))
                if (bmp.GetPixel(x, y).ToArgb() != first) return false;
        return true;
    }
}
