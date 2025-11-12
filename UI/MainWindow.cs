using Spectre.Console;
using PowerDocker.Models;
using PowerDocker.Services;

namespace PowerDocker.UI;

public class MainWindow
{
    private readonly DockerService _dockerService;
    private List<ComposeProject> _projects = new();
    private List<MenuItem> _menuItems = new();
    private int _selectedIndex = 0;
    private bool _running = true;
    private CancellationTokenSource _cancellationTokenSource = new();
    
    public MainWindow()
    {
        try
        {
            _dockerService = new DockerService();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize Docker service. Make sure Docker is running and accessible.", ex);
        }
    }
    
    public async Task RunAsync()
    {
        // Start auto-refresh task
        var autoRefreshTask = AutoRefreshAsync();
        
        // Initial data load
        await RefreshDataAsync();
        
        // Main UI loop
        while (_running)
        {
            RenderUI();
            await HandleInputAsync();
        }
        
        // Cleanup
        _cancellationTokenSource.Cancel();
        try
        {
            await autoRefreshTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
    }
    
    private void RenderUI()
    {
        AnsiConsole.Clear();
        
        // Render header
        var rule = new Rule("[bold cyan]PowerDocker[/]");
        rule.Style = Style.Parse("cyan");
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
        
        // Build menu items
        BuildMenuItems();
        
        // Render container list
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderStyle = Style.Parse("grey");
        table.AddColumn(new TableColumn("[bold]Status[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Name[/]"));
        table.AddColumn(new TableColumn("[bold]State[/]"));
        
        for (int i = 0; i < _menuItems.Count; i++)
        {
            var item = _menuItems[i];
            var isSelected = i == _selectedIndex;
            
            if (item.IsProject)
            {
                var project = (ComposeProject)item.Data;
                var arrow = isSelected ? "[yellow]→[/]" : " ";
                var projectName = isSelected ? $"[yellow bold]{Markup.Escape(project.Name.ToUpper())}[/]" : $"[bold]{Markup.Escape(project.Name.ToUpper())}[/]";
                var status = $"{project.RunningCount}/{project.TotalCount} running";
                
                table.AddRow(arrow, projectName, $"[dim]{status}[/]");
            }
            else
            {
                var container = (DockerContainer)item.Data;
                var arrow = isSelected ? "[yellow]→[/]" : " ";
                var statusIcon = GetContainerStatusIcon(container);
                var containerName = isSelected ? $"[yellow]  {Markup.Escape(container.Name)}[/]" : $"[dim]  {Markup.Escape(container.Name)}[/]";
                var state = GetColoredState(container.State);
                
                table.AddRow($"{arrow} {statusIcon}", containerName, state);
            }
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        // Render status/help
        var panel = new Panel(new Markup(
            "[dim]Controls:[/] " +
            "[cyan]↑/↓[/] Navigate  " +
            "[cyan]r[/] Restart  " +
            "[cyan]s[/] Stop  " +
            "[cyan]e[/] Exit  " +
            "[dim]│ Auto-refresh: 5s[/]"
        ));
        panel.Border = BoxBorder.Rounded;
        panel.BorderStyle = Style.Parse("grey");
        panel.Header = new PanelHeader(GetStatusText());
        AnsiConsole.Write(panel);
    }
    
    private string GetStatusText()
    {
        if (_menuItems.Count == 0)
            return "[dim]No containers found[/]";
            
        var selectedItem = GetSelectedItem();
        if (selectedItem is DockerContainer container)
        {
            return $"[cyan]Container:[/] {Markup.Escape(container.Name)} {GetColoredState(container.State)}";
        }
        else if (selectedItem is ComposeProject project)
        {
            return $"[cyan]Project:[/] {Markup.Escape(project.Name)} - {project.RunningCount}/{project.TotalCount} running";
        }
        
        return $"[cyan]Ready[/] - {_projects.Sum(p => p.TotalCount)} containers in {_projects.Count} projects";
    }
    
    private async Task HandleInputAsync()
    {
        if (!Console.KeyAvailable)
        {
            await Task.Delay(50);
            return;
        }
        
        var key = Console.ReadKey(true);
        
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
                
            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(_menuItems.Count - 1, _selectedIndex + 1);
                break;
                
            case ConsoleKey.R:
                await HandleRestartAsync();
                break;
                
            case ConsoleKey.S:
                await HandleStopAsync();
                break;
                
            case ConsoleKey.E:
            case ConsoleKey.Escape:
                _running = false;
                break;
        }
    }
    
    private async Task HandleRestartAsync()
    {
        var selected = GetSelectedItem();
        if (selected == null) return;
        
        var currentSelection = _selectedIndex;
        
        if (selected is DockerContainer container)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(container.IsRunning ? "Restarting container..." : "Starting container...", async ctx =>
                {
                    bool success;
                    if (container.IsRunning)
                    {
                        success = await _dockerService.RestartContainerAsync(container.Id);
                        ctx.Status(success ? "[green]Container restarted successfully[/]" : "[red]Failed to restart container[/]");
                    }
                    else
                    {
                        success = await _dockerService.StartContainerAsync(container.Id);
                        ctx.Status(success ? "[green]Container started successfully[/]" : "[red]Failed to start container[/]");
                    }
                    await Task.Delay(500);
                });
        }
        else if (selected is ComposeProject project)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(project.RunningCount > 0 ? "Restarting project..." : "Starting project...", async ctx =>
                {
                    bool success;
                    if (project.RunningCount > 0)
                    {
                        success = await _dockerService.RestartComposeProjectAsync(project);
                        ctx.Status(success ? "[green]Project restarted successfully[/]" : "[red]Failed to restart project[/]");
                    }
                    else
                    {
                        success = await _dockerService.StartComposeProjectAsync(project);
                        ctx.Status(success ? "[green]Project started successfully[/]" : "[red]Failed to start project[/]");
                    }
                    await Task.Delay(500);
                });
        }
        
        await RefreshDataAsync();
        
        // Restore cursor position
        if (currentSelection >= 0 && currentSelection < _menuItems.Count)
        {
            _selectedIndex = currentSelection;
        }
    }
    
    private async Task HandleStopAsync()
    {
        var selected = GetSelectedItem();
        if (selected == null) return;
        
        var currentSelection = _selectedIndex;
        
        if (selected is DockerContainer container)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Stopping container...", async ctx =>
                {
                    var success = await _dockerService.StopContainerAsync(container.Id);
                    ctx.Status(success ? "[green]Container stopped successfully[/]" : "[red]Failed to stop container[/]");
                    await Task.Delay(500);
                });
        }
        else if (selected is ComposeProject project)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Stopping project...", async ctx =>
                {
                    var success = await _dockerService.StopComposeProjectAsync(project);
                    ctx.Status(success ? "[green]Project stopped successfully[/]" : "[red]Failed to stop project[/]");
                    await Task.Delay(500);
                });
        }
        
        await RefreshDataAsync();
        
        // Restore cursor position
        if (currentSelection >= 0 && currentSelection < _menuItems.Count)
        {
            _selectedIndex = currentSelection;
        }
    }
    
    private async Task RefreshDataAsync()
    {
        try
        {
            _projects = await _dockerService.GetComposeProjectsAsync();
        }
        catch (Exception)
        {
            _projects = new List<ComposeProject>();
        }
    }
    
    private async Task AutoRefreshAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, _cancellationTokenSource.Token);
                await RefreshDataAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Silently continue on refresh errors
            }
        }
    }
    
    private void BuildMenuItems()
    {
        _menuItems.Clear();
        
        foreach (var project in _projects)
        {
            _menuItems.Add(new MenuItem(true, project));
            
            foreach (var container in project.Containers)
            {
                _menuItems.Add(new MenuItem(false, container));
            }
        }
        
        // Ensure selected index is valid
        if (_selectedIndex >= _menuItems.Count)
        {
            _selectedIndex = Math.Max(0, _menuItems.Count - 1);
        }
    }
    
    private object? GetSelectedItem()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _menuItems.Count)
            return null;
            
        return _menuItems[_selectedIndex].Data;
    }
    
    private string GetContainerStatusIcon(DockerContainer container)
    {
        return container.State.ToLower() switch
        {
            "running" => "[green]●[/]",
            "paused" => "[yellow]‖[/]",
            "restarting" => "[cyan]↻[/]",
            "exited" => "[grey]○[/]",
            "dead" => "[red]✗[/]",
            "created" => "[dim]◦[/]",
            _ => "[dim]?[/]"
        };
    }
    
    private string GetColoredState(string state)
    {
        return state.ToLower() switch
        {
            "running" => "[green]running[/]",
            "paused" => "[yellow]paused[/]",
            "restarting" => "[cyan]restarting[/]",
            "exited" => "[grey]exited[/]",
            "dead" => "[red]dead[/]",
            "created" => "[dim]created[/]",
            _ => $"[dim]{state}[/]"
        };
    }
}

public class MenuItem
{
    public bool IsProject { get; }
    public object Data { get; }
    
    public MenuItem(bool isProject, object data)
    {
        IsProject = isProject;
        Data = data;
    }
}