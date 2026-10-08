using PowerDocker.UI;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("PowerDocker — Docker containers, grouped by compose project\n");
    Console.WriteLine("Usage: powerdocker [--demo]\n");
    Console.WriteLine("↑/↓ or j/k  Select     Enter/Space  Collapse or expand project");
    Console.WriteLine("r  Start/restart       s  Stop       F5  Refresh       q/e/Esc  Quit");
    Console.WriteLine("--demo  Preview the interface without connecting to Docker.");
    return;
}

if (args.Any(arg => arg != "--demo"))
{
    Console.Error.WriteLine("Unknown option. Use --help for usage.");
    Environment.ExitCode = 1;
    return;
}

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.Error.WriteLine("PowerDocker needs an interactive terminal. Use --help for usage.");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var window = new MainWindow(args.Contains("--demo"));
    window.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"PowerDocker: {ex.Message}");
    Environment.ExitCode = 1;
}
