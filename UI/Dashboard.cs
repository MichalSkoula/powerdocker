using Spectre.Console;
using Spectre.Console.Rendering;

namespace PowerDocker.UI;

public static class Dashboard
{
    // No background colors: normal cells use the terminal's own defaults.
    // Inverse video highlights the selection using those same default colors.
    public static IRenderable Render(BrowserState browser, int width, int height,
        string message, string messageStyle = "dim", bool busy = false, bool demo = false)
    {
        if (width < 42 || height < 16)
            return new Rows(new Text("PowerDocker", new Style(decoration: Decoration.Bold)),
                new Text("Resize terminal to at least 42 × 16."), new Text("q / Esc  Quit"));

        var running = browser.Projects.Sum(p => p.RunningCount);
        var total = browser.Projects.Sum(p => p.TotalCount);
        var composeCount = browser.Projects.Count(p => p.Name != "Standalone");
        var layout = new Layout("root").SplitRows(
            new Layout("header").Size(3),
            new Layout("list").Size(height - 11),
            new Layout("detail").Size(4),
            new Layout("footer").Size(3));

        layout["header"].Update(new Rows(
            new Markup($"[bold cyan] PowerDocker[/]  [dim]/{(demo ? " preview" : " containers")}[/]"),
            new Markup($" {composeCount} projects  [dim]·[/]  [green]{running} running[/]  [dim]·[/]  {total - running} inactive")));

        var visible = browser.VisibleItems(height - 14).ToList();
        var table = new Table().Border(TableBorder.None).Expand();
        table.AddColumn(new TableColumn(" ").Width(1).Padding(0, 0));
        table.AddColumn(new TableColumn("[dim]PROJECT / CONTAINER[/]").NoWrap());
        table.AddColumn(new TableColumn("[dim]STATE[/]").Width(14).NoWrap());
        var showImages = width >= 100;
        if (showImages) table.AddColumn(new TableColumn("[dim]IMAGE[/]").Width(30).NoWrap());

        foreach (var (item, selected) in visible)
        {
            var isProject = item.Container == null;
            var name = isProject
                ? $"{(browser.IsCollapsed(item.Project) ? "▸" : "▾")} {item.Project.Name}"
                : $"  {(item == browser.Items.LastOrDefault(i => i.Project == item.Project) ? "└─" : "├─")} {item.Container!.Name}";
            var state = isProject ? $"{item.Project.RunningCount}/{item.Project.TotalCount} running" : item.Container!.State;
            var stateColor = isProject
                ? item.Project.AllRunning ? "green" : item.Project.AnyRunning ? "yellow" : "dim"
                : item.Container!.State switch
                {
                    "running" => "green",
                    "paused" or "restarting" => "yellow",
                    "dead" => "red",
                    _ => "dim"
                };
            var cells = new List<IRenderable>
            {
                Cell(selected ? "›" : " ", selected ? "invert" : "default"),
                Cell(name, selected ? "bold invert" : isProject ? "bold cyan" : "default"),
                Cell(state, selected ? "invert" : stateColor)
            };
            if (showImages) cells.Add(Cell(item.Container?.Image ?? "", selected ? "invert" : "dim"));
            table.AddRow(cells.ToArray());
        }

        IRenderable list = browser.Items.Count == 0
            ? new Text(message.StartsWith("Connecting", StringComparison.Ordinal) ? "Connecting to Docker…" : "No containers to show. Start Docker or create a container, then press F5.")
            : table;
        var position = browser.Items.Count == 0 ? "" : $"  {browser.Selection + 1}/{browser.Items.Count}";
        layout["list"].Update(new Panel(list).Header($"[dim] Projects{position} [/]")
            .RoundedBorder().BorderStyle(new Style(decoration: Decoration.Dim)).Expand());

        var selectedItem = browser.Selected;
        string detail;
        string detailTitle;
        if (selectedItem?.Container is { } container)
        {
            detailTitle = "Container";
            detail = $"[bold]{Escape(container.Name)}[/]  [dim]·[/]  {Escape(container.Status)}\n"
                + $"[dim]Image[/] {Escape(container.Image)}  [dim]· Service[/] {Escape(string.IsNullOrEmpty(container.ComposeService) ? "standalone" : container.ComposeService)}";
        }
        else if (selectedItem is { } projectItem)
        {
            detailTitle = projectItem.Project.Name == "Standalone" ? "Standalone group" : "Compose project";
            detail = $"[bold]{Escape(projectItem.Project.Name)}[/]  [dim]·[/]  {projectItem.Project.TotalCount} containers\n"
                + "Actions apply to every container in this group.";
        }
        else
        {
            detailTitle = "Selection";
            detail = "Select a project or container with ↑ / ↓.";
        }
        var details = new Table().Border(TableBorder.None).HideHeaders().Expand();
        details.AddColumn(new TableColumn("").NoWrap().Padding(0, 0));
        foreach (var line in detail.Split('\n')) details.AddRow(new Markup(line).Overflow(Overflow.Ellipsis));
        layout["detail"].Update(new Panel(details).Header($"[dim] {detailTitle} [/]")
            .RoundedBorder().BorderStyle(new Style(decoration: Decoration.Dim)).Expand());

        var startLabel = (selectedItem?.Container?.IsRunning ?? selectedItem?.Project.AnyRunning) == true ? "restart" : "start";
        var actionStyle = busy || demo || selectedItem == null ? "dim" : "bold cyan";
        layout["footer"].Update(new Rows(
            Cell($" {message}", messageStyle),
            new Markup($" [bold cyan]↑↓[/] select  [{actionStyle}]r[/] {startLabel}  [{actionStyle}]s[/] stop  [bold cyan]q[/] quit"),
            Cell(width < 60 ? " Enter fold / unfold · F5 refresh" : " Enter fold / unfold · F5 refresh · auto-refresh 5s", "dim")));
        return layout;
    }

    private static Text Cell(string value, string style) => new Text(Clean(value), Style.Parse(style))
        .Overflow(Overflow.Ellipsis);

    private static string Escape(string value) => Markup.Escape(Clean(value));

    // Docker metadata and exceptions are untrusted terminal text, not ANSI or markup.
    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).ToArray());
}
