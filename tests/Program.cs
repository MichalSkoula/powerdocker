using System.Text.RegularExpressions;
using PowerDocker.Models;
using PowerDocker.UI;
using Spectre.Console;
using Docker.DotNet;
using Docker.DotNet.Models;
using PowerDocker.Services;
using System.Reflection;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

ComposeProject Project(string name, params string[] ids) => new()
{
    Name = name,
    Containers = ids.Select(id => new DockerContainer
    {
        Id = id, Name = id, State = "running", Image = "postgres:16", Status = "Up 2 hours"
    }).ToList()
};

var state = new BrowserState();
state.Update([Project("app", "api", "db")]);
state.Move(2);
state.Update([Project("new", "worker"), Project("app", "api", "db")]);
Check(state.Selected?.Container?.Id == "db", "Refresh must keep the selected container when row positions change.");
state.Update([Project("app", "api")]);
Check(state.Selected is { Container: null, Project.Name: "app" }, "A removed container must fall back to its own project.");
state.ToggleProject();
Check(state.Items.Count == 1, "Collapse must hide only child containers.");
state.Update([Project("app", "api", "db")]);
Check(state.Items.Count == 1, "Auto-refresh must preserve collapsed groups.");
state.ToggleProject();
Check(state.Items.Count == 3 && state.Selected?.Container == null, "Expanding must preserve project selection.");
state.Move(100);
Check(state.Selected?.Container?.Id == "db", "Navigation must clamp to the last item.");
Check(state.VisibleItems(1).Single().Selected, "Scrolling must keep the selected item visible.");
state.Update([]);
Check(state.Selected == null && !state.VisibleItems(3).Any(), "An empty Docker host must have no actionable selection.");

string Render(BrowserState browser, int width, int height, bool ansi = false)
{
    using var writer = new StringWriter();
    var console = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = ansi ? AnsiSupport.Yes : AnsiSupport.No,
        ColorSystem = ColorSystemSupport.Standard,
        Out = new AnsiConsoleOutput(writer),
        Interactive = InteractionSupport.No
    });
    console.Profile.Width = width;
    console.Profile.Height = height;
    console.Write(Dashboard.Render(browser, width, height, "Ready"));
    return writer.ToString();
}

var project = Project("app[red]literal[/]", "api");
project.Containers[0].Name = "container[bold]literal[/]\u001b\n";
project.Containers[0].Image = new string('x', 200);
state.Update([project]);
state.Move(1);
foreach (var (width, height) in new[] { (42, 16), (60, 20), (80, 24), (120, 30) })
{
    var rendered = Render(state, width, height);
    var lines = rendered.Replace("\r", "").TrimEnd('\n').Split('\n');
    Check(lines.Length <= height, $"Dashboard must fit height at {width}×{height}.");
    Check(lines.All(line => line.Length <= width), $"Dashboard must fit width at {width}×{height}.");
    Check(rendered.Contains("q") && rendered.Contains("quit"), "Quit hint must remain visible.");
    Check(!rendered.Contains('\u001b'), "Docker metadata must not inject terminal escapes.");
}
var wide = Render(state, 120, 30);
Check(wide.Contains("app[red]literal[/]"), "Docker names must render as literal text, not markup.");
Check(wide.Contains("container[bold]literal[/]"), "Container details must escape markup.");
var colored = Render(state, 120, 30, ansi: true);
var sgr = Regex.Matches(colored, "\u001b\\[([0-9;]*)m");
Check(sgr.Count > 0, "ANSI rendering must emit styles.");
Check(sgr.All(match => !match.Groups[1].Value.Split(';').Any(code =>
    int.TryParse(code, out var n) && (n is >= 40 and <= 48 || n is >= 100 and <= 107))),
    "Dashboard must never set a background color.");
Check(colored.Contains("\u001b[7m") || sgr.Any(match => match.Groups[1].Value.Split(';').Contains("7")),
    "Selection must use inverse terminal colors.");
Check(Render(state, 30, 10).Contains("Resize terminal"), "Tiny terminals must display a usable fallback.");

ContainerStatsResponse Stats(ulong cpu, ulong previousCpu, ulong system, ulong previousSystem, uint processors = 4) => new()
{
    CPUStats = new CPUStats { CPUUsage = new CPUUsage { TotalUsage = cpu }, SystemUsage = system, OnlineCPUs = processors },
    PreCPUStats = new CPUStats { CPUUsage = new CPUUsage { TotalUsage = previousCpu }, SystemUsage = previousSystem },
    MemoryStats = new MemoryStats { Usage = 300 * 1024 * 1024, Limit = 1024 * 1024 * 1024,
        Stats = new Dictionary<string, ulong> { ["total_inactive_file"] = 100 * 1024 * 1024 } }
};

var sample = Stats(200, 100, 1400, 1000);
var usage = UsageCalculator.FromStats(sample);
Check(usage.CpuPercent == 100 && usage.MemoryBytes == 200 * 1024 * 1024,
    "Linux stats must account for the CPU count and subtract cgroup v1 file cache.");
sample.CPUStats.OnlineCPUs = 0;
sample.CPUStats.CPUUsage.PercpuUsage = new List<ulong> { 1, 1, 1, 1 };
Check(UsageCalculator.FromStats(sample).CpuPercent == 100, "CPU count must fall back to per-core counters.");
sample.MemoryStats.Stats = new Dictionary<string, ulong> { ["inactive_file"] = 50 * 1024 * 1024 };
Check(UsageCalculator.FromStats(sample).MemoryBytes == 250 * 1024 * 1024, "RAM must subtract cgroup v2 file cache.");
sample.MemoryStats.Stats["inactive_file"] = ulong.MaxValue;
Check(UsageCalculator.FromStats(sample).MemoryBytes == sample.MemoryStats.Usage, "Invalid cache counters must not underflow memory usage.");
Check(UsageCalculator.FromStats(Stats(100, 100, 1400, 1000)).CpuPercent == 0, "A valid idle CPU sample must be zero.");
Check(UsageCalculator.FromStats(Stats(300, 100, 1400, 1000)).CpuPercent == 200, "Multi-core CPU usage must not be clamped to 100%.");
Check(UsageCalculator.FromStats(Stats(0, 100, 500, 1000)).CpuPercent == null, "Reset CPU counters must be unavailable, not overflow.");
Check(UsageCalculator.FromStats(Stats(100, 0, 1000, 0)).CpuPercent == null, "A missing prior sample must not display a fabricated percentage.");
Check(UsageCalculator.FromStats(Stats(100, 50, 1000, 1000)).CpuPercent == null, "CPU calculation must guard against division by zero.");
var windowsSample = Stats(20_000_000, 0, 0, 0);
windowsSample.NumProcs = 4;
windowsSample.PreRead = DateTime.UtcNow;
windowsSample.Read = windowsSample.PreRead.AddSeconds(1);
windowsSample.MemoryStats.PrivateWorkingSet = 42 * 1024 * 1024;
var windowsUsage = UsageCalculator.FromStats(windowsSample);
Check(windowsUsage.CpuPercent == 50 && windowsUsage.MemoryBytes == 42 * 1024 * 1024 && windowsUsage.MemoryLimitBytes == null,
    "Native Windows stats must use elapsed 100ns intervals and private working set.");

var metricsProject = Project("metrics", "web", "db", "stopped");
metricsProject.Containers[0].Usage = new ContainerUsage(12.5, 100 * 1024 * 1024);
metricsProject.Containers[1].Usage = new ContainerUsage(2.5, 200 * 1024 * 1024);
metricsProject.Containers[2].State = "exited";
metricsProject.Containers[2].Usage = new ContainerUsage(99, 999);
Check(metricsProject.Usage?.CpuPercent == 15 && metricsProject.Usage.MemoryBytes == 300 * 1024 * 1024,
    "Project totals must sum running containers and ignore stopped containers' stale metrics.");
metricsProject.Containers[1].Usage = new ContainerUsage(null, 200 * 1024 * 1024);
Check(metricsProject.Usage?.CpuPercent == null && metricsProject.Usage?.MemoryBytes == 300 * 1024 * 1024,
    "Incomplete CPU totals must remain unknown while independently available RAM can still be shown.");
Check(Dashboard.FormatMemory(new ContainerUsage(0, 1536 * 1024 * 1024)) == "1.5 GiB", "RAM must use readable binary units.");
Check(Dashboard.FormatCpu(new ContainerUsage(12.5, 0)) == "12.5%", "CPU formatting must be stable across locales.");
Check(Dashboard.FormatCpu(null) == "—" && Dashboard.FormatMemory(null) == "—", "Unavailable metrics must display a dash.");

state.Update([metricsProject]);
state.Move(-state.Items.Count);
foreach (var (width, height) in new[] { (42, 16), (72, 20), (80, 24), (110, 30), (140, 40) })
{
    var rendered = Render(state, width, height);
    var lines = rendered.Replace("\r", "").TrimEnd('\n').Split('\n');
    Check(lines.Length <= height && lines.All(line => line.Length <= width), "Resource columns must fit wide and narrow terminals.");
    Check(rendered.Contains("CPU") && rendered.Contains("RAM"), "Resource metrics must be visible in table or details.");
}
state.Move(1);
Check(Render(state, 80, 24).Contains("12.5%") && Render(state, 80, 24).Contains("100 MiB"), "Container metrics must render with values.");
state.Update([Project("metrics", "db", "web")]);
Check(state.Selected?.Container?.Id == "web" && state.Selected.Container.Usage?.CpuPercent == 12.5,
    "List refresh must preserve both selection identity and recent metrics.");
state.Update([Project("metrics", "web")]);
state.Projects[0].Containers[0].State = "exited";
state.ApplyUsage(new Dictionary<string, ContainerUsage?> { ["web"] = new ContainerUsage(99, 1234) });
Check(state.Projects[0].Containers[0].Usage == null, "A late statistics result must not repopulate a stopped container.");
state.Update([Project("metrics", "web")]);
state.ApplyUsage(new Dictionary<string, ContainerUsage?>());
Check(state.Projects[0].Usage?.CpuPercent == null, "A failed statistics request must clear stale values.");

var statsApi = DispatchProxy.Create<IContainerOperations, StatsApiStub>();
var statsStub = (StatsApiStub)(object)statsApi;
var releaseStats = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
statsStub.GetSample = async (id, token) =>
{
    await releaseStats.Task.WaitAsync(token);
    if (id == "missing") throw new InvalidOperationException("Container disappeared");
    return Stats(200, 100, 1400, 1000);
};
var dockerApi = DispatchProxy.Create<IDockerClient, DockerApiStub>();
((DockerApiStub)(object)dockerApi).Containers = statsApi;
using (var service = new DockerService(dockerApi))
{
    var pendingStats = service.GetUsageAsync(Enumerable.Range(0, 12).Select(i => $"container-{i}").Append("missing"));
    Check(!pendingStats.IsCompleted && statsStub.Active <= 8, "Statistics must run asynchronously with bounded concurrency.");
    releaseStats.SetResult();
    var samples = pendingStats.GetAwaiter().GetResult();
    Check(samples.Count == 13 && samples["missing"] == null && samples["container-0"]?.CpuPercent == 100,
        "One failed container must not hide successful statistics from the others.");
    Check(statsStub.MaxActive <= 8 && statsStub.SnapshotRequests, "Stats must request finite snapshots, not indefinite streams.");
}

var cancelledApi = DispatchProxy.Create<IContainerOperations, StatsApiStub>();
((StatsApiStub)(object)cancelledApi).GetSample = async (_, token) =>
{
    await Task.Delay(Timeout.Infinite, token);
    throw new Exception("Unreachable");
};
var cancelledDocker = DispatchProxy.Create<IDockerClient, DockerApiStub>();
((DockerApiStub)(object)cancelledDocker).Containers = cancelledApi;
var cancelledService = new DockerService(cancelledDocker);
var cancelledRequest = cancelledService.GetUsageAsync(["running"]);
cancelledService.Dispose();
Check(SpinWait.SpinUntil(() => cancelledRequest.IsCompleted, TimeSpan.FromSeconds(1)), "Quitting must cancel statistics promptly.");
try { cancelledRequest.GetAwaiter().GetResult(); Check(false, "Cancelled stats must not succeed."); }
catch (OperationCanceledException) { Check(true, "Statistics cancellation observed."); }

using var updates = new StringWriter();
var screenConsole = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = AnsiSupport.Yes, ColorSystem = ColorSystemSupport.Standard,
    Out = new AnsiConsoleOutput(updates), Interactive = InteractionSupport.No
});
screenConsole.Profile.Width = 80;
screenConsole.Profile.Height = 24;
using var screen = new TerminalScreen(screenConsole);
state.Update([Project("app", "api", "db"), Project("other", "worker")]);
state.Move(-state.Items.Count);
var terminal = new TestTerminal(80, 24);

void DrawAndVerify(int width = 80, int height = 24)
{
    updates.GetStringBuilder().Clear();
    screen.Draw(Dashboard.Render(state, width, height, "Ready"), width, height);
    terminal.Apply(updates.ToString());
    var expected = Render(state, width, height).Replace("\r", "").TrimEnd('\n').Split('\n');
    Check(terminal.Lines.Take(expected.Length).SequenceEqual(expected.Select(line => line.PadRight(width))),
        "Incremental updates must match a complete dashboard after navigation or data changes.");
}

DrawAndVerify();
updates.GetStringBuilder().Clear();
Check(screen.Draw(Dashboard.Render(state, 80, 24, "Ready"), 80, 24) == 0 && updates.ToString() == "",
    "An unchanged screen must produce no terminal output.");
state.Move(1);
DrawAndVerify();
Check(!updates.ToString().Contains("\u001b[2J") && !updates.ToString().Contains("PowerDocker"),
    "Navigation must not clear or reprint the whole dashboard.");
Check(Regex.Matches(updates.ToString(), "\u001b\\[[0-9]+;1H").Count < 12,
    "Navigation must repaint only the affected rows.");
state.Move(1);
DrawAndVerify();
state.Move(-state.Items.Count);
state.ToggleProject();
DrawAndVerify();
state.Update([]);
DrawAndVerify();
terminal = new TestTerminal(42, 16);
DrawAndVerify(42, 16);
Check(updates.ToString().Contains("\u001b[2J"), "Resize must repaint the complete visible screen.");

updates.GetStringBuilder().Clear();
screen.Draw(new Text("One\nTwo\nThree"), 42, 16);
terminal.Apply(updates.ToString());
updates.GetStringBuilder().Clear();
screen.Draw(new Text("One"), 42, 16);
terminal.Apply(updates.ToString());
Check(terminal.Lines.Skip(1).All(line => string.IsNullOrWhiteSpace(line)),
    "Rows removed from a frame must be erased without leaving stale text.");
updates.GetStringBuilder().Clear();
screen.Draw(new Text(new string('x', 42)), 42, 16);
terminal.Apply(updates.ToString());
Check(terminal.Lines.First() == new string('x', 42), "A full-width row must retain its last cell despite delayed wrapping.");

Console.WriteLine($"Passed {checks} checks.");

// Minimal terminal model checks visible text, cursor addressing and erasing.
sealed class TestTerminal(int width, int height)
{
    private readonly char[][] _cells = Enumerable.Range(0, height).Select(_ => new string(' ', width).ToCharArray()).ToArray();
    private int _row;
    private int _column;
    private bool _wrapPending;
    public IEnumerable<string> Lines => _cells.Select(row => new string(row));

    public void Apply(string output)
    {
        var tokens = Regex.Split(output, "(\u001b\\[[0-9;]*[A-Za-z])");
        foreach (var token in tokens)
        {
            if (token.StartsWith("\u001b["))
            {
                var args = token[2..^1].Split(';').Select(n => string.IsNullOrEmpty(n) ? 0 : int.Parse(n)).ToArray();
                switch (token[^1])
                {
                    case 'H': _row = args[0] - 1; _column = args[1] - 1; _wrapPending = false; break;
                    case 'J' when args[0] == 2:
                        foreach (var row in _cells) Array.Fill(row, ' ');
                        break;
                    case 'K': Array.Fill(_cells[_row], ' ', _column, width - _column); break;
                    case 'm': break;
                    default: throw new Exception($"Unexpected terminal command: {token}");
                }
            }
            else
                foreach (var character in token)
                {
                    if (_wrapPending) throw new Exception("Drawing must address the next row explicitly without wrapping.");
                    _cells[_row][_column] = character;
                    if (_column == width - 1) _wrapPending = true;
                    else _column++;
                }
        }
    }
}

public class DockerApiStub : DispatchProxy
{
    public IContainerOperations Containers { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "get_Containers" => Containers,
        "Dispose" => null,
        _ => throw new NotSupportedException(method.Name)
    };
}

public class StatsApiStub : DispatchProxy
{
    public Func<string, CancellationToken, Task<ContainerStatsResponse>> GetSample { get; set; } = null!;
    public int Active;
    public int MaxActive;
    public bool SnapshotRequests = true;

    protected override object Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name != "GetContainerStatsAsync") throw new NotSupportedException(method.Name);
        var parameters = (ContainerStatsParameters)args![1]!;
        SnapshotRequests &= !parameters.Stream && parameters.OneShot == false;
        return Capture((string)args[0]!, (IProgress<ContainerStatsResponse>)args[2]!, (CancellationToken)args[3]!);
    }

    private async Task Capture(string id, IProgress<ContainerStatsResponse> progress, CancellationToken token)
    {
        var active = Interlocked.Increment(ref Active);
        MaxActive = Math.Max(MaxActive, active);
        try { progress.Report(await GetSample(id, token)); }
        finally { Interlocked.Decrement(ref Active); }
    }
}
