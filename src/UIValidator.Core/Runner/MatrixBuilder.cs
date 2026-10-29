using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Runner;

/// <summary>Expands the matrix configuration into an ordered list of scenarios.</summary>
public static class MatrixBuilder
{
    /// <summary>
    /// Order is resolution → DPI scale → window size so that the expensive display
    /// changes happen as rarely as possible.
    /// </summary>
    public static List<Scenario> Build(MatrixConfig matrix)
    {
        var resolutions = matrix.Resolutions.Count > 0 ? matrix.Resolutions.Select(r => (string?)Normalize(r)) : [null];
        var scales = matrix.DpiScales.Count > 0 ? matrix.DpiScales.Select(d => (int?)d) : [null];
        var sizes = matrix.WindowSizes.Count > 0 ? matrix.WindowSizes : ["current"];

        return (from r in resolutions
                from d in scales
                from s in sizes
                select new Scenario { Resolution = r, DpiScale = d, WindowSize = Normalize(s) }).ToList();
    }

    private static string Normalize(string s) =>
        Size2D.TryParse(s, out var size) ? size.ToString() : s.Trim().ToLowerInvariant();
}
