using System.Text.RegularExpressions;
using PowerDocker.Models;
using PowerDocker.UI;
using Spectre.Console;

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
Console.WriteLine($"Passed {checks} checks.");
