using UIValidator.Core.Config;

namespace UIValidator.Cli;

/// <summary>Command-line entry point.</summary>
internal static class Program
{
    private const string Usage = """
        Windows UI Validator - automated UI testing across resolutions and DPI scales.

        Usage:
          uivalidator validate <config.yaml>     Check a configuration file
          uivalidator --help                     Show this help
        """;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "validate" => Validate(args),
                _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int Validate(string[] args)
    {
        if (args.Length < 2) return Fail("Missing config path.");
        var config = ConfigLoader.Load(args[1]);
        var errors = ConfigLoader.Validate(config);
        foreach (var e in errors) Console.Error.WriteLine($"error: {e}");
        if (errors.Count == 0) Console.WriteLine("Configuration is valid.");
        return errors.Count == 0 ? 0 : 2;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
