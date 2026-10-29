using System.Drawing;
using UIValidator.Core.Model;
using UIValidator.Core.Visual;

namespace UIValidator.Tests;

public class ImageComparerTests
{
    private static Bitmap Solid(int w, int h, Color color)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    [Fact]
    public void Identical_images_are_fully_similar()
    {
        using var a = Solid(64, 64, Color.White);
        using var b = Solid(64, 64, Color.White);
        using var r = ImageComparer.Compare(a, b, 16);
        Assert.Equal(1.0, r.Similarity, 3);
        Assert.Null(r.ChangedRegion);
    }

    [Fact]
    public void Changed_area_is_located()
    {
        using var a = Solid(64, 64, Color.White);
        using var b = Solid(64, 64, Color.White);
        using (var g = Graphics.FromImage(a)) g.FillRectangle(Brushes.Black, 10, 20, 8, 4);
        using var r = ImageComparer.Compare(a, b, 16);
        Assert.True(r.Similarity < 1.0);
        Assert.Equal(new Rect(10, 20, 8, 4), r.ChangedRegion);
        Assert.NotNull(r.DiffImage);
    }

    [Fact]
    public void Masked_regions_are_ignored()
    {
        using var a = Solid(64, 64, Color.White);
        using var b = Solid(64, 64, Color.White);
        using (var g = Graphics.FromImage(a)) g.FillRectangle(Brushes.Black, 10, 20, 8, 4);
        using var r = ImageComparer.Compare(a, b, 16, [new Rect(8, 16, 16, 16)]);
        Assert.Null(r.ChangedRegion);
        Assert.Equal(1.0, r.Similarity, 3);
    }

    [Fact]
    public void Size_mismatch_is_reported()
    {
        using var a = Solid(64, 64, Color.White);
        using var b = Solid(32, 64, Color.White);
        using var r = ImageComparer.Compare(a, b, 16);
        Assert.True(r.SizeMismatch);
    }
}
