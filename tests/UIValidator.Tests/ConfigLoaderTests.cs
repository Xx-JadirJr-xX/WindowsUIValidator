using UIValidator.Core.Config;

namespace UIValidator.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Parses_matrix_screens_and_steps()
    {
        var config = ConfigLoader.Parse("""
            app: { path: app.exe }
            matrix:
              resolutions: [1920x1080]
              dpiScales: [100, 150]
              windowSizes: [maximized, 800x600]
            screens:
              - name: Settings
                steps:
                  - click: { automationId: btnSettings }
                  - type: { target: { automationId: txt }, text: hello }
                  - keys: ctrl+s
            rules:
              overlap: { enabled: false }
            """);

        Assert.Equal(["1920x1080"], config.Matrix.Resolutions);
        Assert.Equal([100, 150], config.Matrix.DpiScales);
        Assert.Equal("btnSettings", config.Screens[0].Steps[0].Click!.AutomationId);
        Assert.Equal("hello", config.Screens[0].Steps[1].Type!.Text);
        Assert.Equal("ctrl+s", config.Screens[0].Steps[2].Keys);
        Assert.False(config.Rules.Overlap.Enabled);
        Assert.True(config.Rules.Clipping.Enabled);
    }

    [Theory]
    [InlineData("1920x1080", true)]
    [InlineData("800 x 600", true)]
    [InlineData("abc", false)]
    [InlineData("0x100", false)]
    public void Parses_sizes(string text, bool ok) => Assert.Equal(ok, Size2D.TryParse(text, out _));

    [Fact]
    public void Reports_invalid_values()
    {
        var config = ConfigLoader.Parse("""
            app: { path: does-not-exist.exe }
            matrix: { resolutions: [bad], dpiScales: [50], windowSizes: [huge] }
            """);
        var errors = ConfigLoader.Validate(config);
        Assert.True(errors.Count >= 5);
    }
}
