namespace UIValidator.Core.Model;

/// <summary>Immutable integer rectangle (physical pixels).</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public long Area => IsEmpty ? 0 : (long)Width * Height;

    public static Rect FromLTRB(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    public Rect Intersect(Rect other)
    {
        var l = Math.Max(X, other.X);
        var t = Math.Max(Y, other.Y);
        var r = Math.Min(Right, other.Right);
        var b = Math.Min(Bottom, other.Bottom);
        return r > l && b > t ? FromLTRB(l, t, r, b) : default;
    }

    public bool Intersects(Rect other) => !Intersect(other).IsEmpty;

    /// <summary>True when <paramref name="other"/> lies inside this rectangle, allowing <paramref name="tolerance"/> pixels of slack.</summary>
    public bool Contains(Rect other, int tolerance = 0) =>
        other.X >= X - tolerance && other.Y >= Y - tolerance &&
        other.Right <= Right + tolerance && other.Bottom <= Bottom + tolerance;

    public Rect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };

    public static Rect From(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
    public System.Drawing.Rectangle ToDrawing() => new(X, Y, Width, Height);

    public override string ToString() => $"({X},{Y} {Width}x{Height})";
}
