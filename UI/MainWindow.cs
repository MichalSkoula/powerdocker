using Terminal.Gui;
using PowerDocker.Models;
using PowerDocker.Services;
using System.Timers;

namespace PowerDocker.UI;

public class MainWindow : View
{
    private readonly DockerService _dockerService;
    private readonly ListView _listView;
    private readonly Label _statusLabel;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _exitButton;
    private readonly System.Timers.Timer _autoRefreshTimer;
    private readonly string _defaultStatusText = "Ready (auto-refresh: 5s)";

    private List<ComposeProject> _projects = new();
    private List<ListItem> _listItems = new();
    
    public MainWindow()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();
        
        try
        {
            _dockerService = new DockerService();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize Docker service. Make sure Docker is running and accessible.", ex);
        }
        
        _listView = new ListView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 3
        };
        
        _statusLabel = new Label()
        {
            X = 0,
            Y = Pos.Bottom(_listView),
            Width = Dim.Fill(),
            Height = 1,
            Text = this._defaultStatusText
        };
        
        _startButton = new Button("Restart")
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel)
        };
        
        _stopButton = new Button("Stop")
        {
            X = Pos.Right(_startButton) + 1,
            Y = Pos.Bottom(_statusLabel)
        };
        
        _exitButton = new Button("Exit")
        {
            X = Pos.Right(_stopButton) + 1,
            Y = Pos.Bottom(_statusLabel)
        };
        
        Add(_listView, _statusLabel, _startButton, _stopButton, _exitButton);
        
        _startButton.Clicked += OnStartClicked;
        _stopButton.Clicked += OnStopClicked;
        _exitButton.Clicked += OnExitClicked;
        
        _listView.SelectedItemChanged += OnSelectionChanged;
        _listView.KeyPress += OnKeyPress;
        
        CanFocus = true;
        
        _autoRefreshTimer = new System.Timers.Timer(5000);
        _autoRefreshTimer.Elapsed += async (sender, e) => await AutoRefreshAsync();
        _autoRefreshTimer.Start();
        
        _ = RefreshDataAsync();
    }
    
    
    private async void OnStartClicked()
    {
        var selected = GetSelectedItem();
        if (selected == null) return;
        
        var currentSelection = _listView.SelectedItem;
        bool success;
        
        if (selected is DockerContainer container)
        {
            if (container.IsRunning)
            {
                _statusLabel.Text = "Restarting container...";
                Application.Refresh();
                success = await _dockerService.RestartContainerAsync(container.Id);
                _statusLabel.Text = success ? "Container restarted successfully" : "Failed to restart container";
            }
            else
            {
                _statusLabel.Text = "Starting container...";
                Application.Refresh();
                success = await _dockerService.StartContainerAsync(container.Id);
                _statusLabel.Text = success ? "Container started successfully" : "Failed to start container";
            }
        }
        else if (selected is ComposeProject project)
        {
            if (project.RunningCount > 0)
            {
                _statusLabel.Text = "Restarting project...";
                Application.Refresh();
                success = await _dockerService.RestartComposeProjectAsync(project);
                _statusLabel.Text = success ? "Project restarted successfully" : "Failed to restart project";
            }
            else
            {
                _statusLabel.Text = "Starting project...";
                Application.Refresh();
                success = await _dockerService.StartComposeProjectAsync(project);
                _statusLabel.Text = success ? "Project started successfully" : "Failed to start project";
            }
        }
        else
        {
            return;
        }
        
        await RefreshDataAsync();
        
        // Restore cursor position
        if (currentSelection >= 0 && currentSelection < _listItems.Count)
        {
            _listView.SelectedItem = currentSelection;
        }
    }
    
    private async void OnStopClicked()
    {
        var selected = GetSelectedItem();
        if (selected == null) return;
        
        var currentSelection = _listView.SelectedItem;
        _statusLabel.Text = "Stopping...";
        Application.Refresh();
        
        bool success;
        if (selected is DockerContainer container)
        {
            success = await _dockerService.StopContainerAsync(container.Id);
        }
        else if (selected is ComposeProject project)
        {
            success = await _dockerService.StopComposeProjectAsync(project);
        }
        else
        {
            return;
        }
        
        _statusLabel.Text = success ? "Stopped successfully" : "Failed to stop";
        await RefreshDataAsync();
        
        // Restore cursor position
        if (currentSelection >= 0 && currentSelection < _listItems.Count)
        {
            _listView.SelectedItem = currentSelection;
        }
    }
    
    private void OnExitClicked()
    {
        Application.RequestStop();
    }
    
    private void OnSelectionChanged(ListViewItemEventArgs args)
    {
        var selected = GetSelectedItem();
        
        if (selected is DockerContainer container)
        {
            _statusLabel.Text = $"Container: {container.Name} [{container.State}]";
        }
        else if (selected is ComposeProject project)
        {
            _statusLabel.Text = $"Project: {project.Name} [{project.RunningCount}/{project.TotalCount} running]";
        }
        else
        {
            _statusLabel.Text = this._defaultStatusText;
        }
    }
    
    private void OnKeyPress(KeyEventEventArgs keyEvent)
    {
        switch ((char)keyEvent.KeyEvent.KeyValue)
        {
            case 'r':
                if (GetSelectedItem() is DockerContainer or ComposeProject)
                {
                    OnStartClicked();
                    keyEvent.Handled = true;
                }
                break;
                
            case 's':
                if (GetSelectedItem() is DockerContainer or ComposeProject)
                {
                    OnStopClicked();
                    keyEvent.Handled = true;
                }
                break;
                
            case 'e':
                Application.RequestStop();
                keyEvent.Handled = true;
                break;
        }
    }
    
    private object? GetSelectedItem()
    {
        if (_listView.SelectedItem < 0 || _listView.SelectedItem >= _listItems.Count)
            return null;
            
        return _listItems[_listView.SelectedItem].Data;
    }
    
    private async Task RefreshDataAsync()
    {
        try
        {
            _statusLabel.Text = "Loading...";
            Application.Refresh();
            
            _projects = await _dockerService.GetComposeProjectsAsync();
            BuildList();
            
            _statusLabel.Text = $"Loaded {_projects.Sum(p => p.TotalCount)} containers in {_projects.Count} projects";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }
    
    private async Task AutoRefreshAsync()
    {
        try
        {
            _projects = await _dockerService.GetComposeProjectsAsync();
            Application.MainLoop.Invoke(() => 
            {
                var currentSelection = _listView.SelectedItem;
                BuildList();
                
                // Preserve cursor position if possible
                if (currentSelection >= 0 && currentSelection < _listItems.Count)
                {
                    _listView.SelectedItem = currentSelection;
                }
                
                _statusLabel.Text = $"Auto-refreshed: {_projects.Sum(p => p.TotalCount)} containers in {_projects.Count} projects";
                Application.Refresh();
            });
        }
        catch (Exception ex)
        {
            Application.MainLoop.Invoke(() => 
            {
                _statusLabel.Text = $"Auto-refresh error: {ex.Message}";
                Application.Refresh();
            });
        }
    }
    
    private void BuildList()
    {
        _listItems.Clear();
        
        foreach (var project in _projects)
        {
            var projectDisplay = $"--> {project.Name.ToUpper()} ({project.RunningCount}/{project.TotalCount} running)";
            _listItems.Add(new ListItem(projectDisplay, project));
            
            foreach (var container in project.Containers)
            {
                var statusIcon = GetContainerStatusIcon(container);
                var containerDisplay = $"    {statusIcon} {container.Name} [{container.State}]";
                _listItems.Add(new ListItem(containerDisplay, container));
            }
        }
        
        _listView.SetSource(_listItems.Select(i => i.Display).ToList());
    }
    
    private string GetContainerStatusIcon(DockerContainer container)
    {
        return container.State.ToLower() switch
        {
            "running" => "●",
            "paused" => "‖",
            "restarting" => "↻",
            "exited" => "○",
            "dead" => "✗",
            "created" => "◦",
            _ => "?"
        };
    }
    
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        switch (keyEvent.Key)
        {
            case Key.Enter:
                _listView.SetFocus();
                return true;
                
            case Key.Esc:
                return base.ProcessKey(keyEvent);
        }
        
        return base.ProcessKey(keyEvent);
    }
    
    private async Task RemoveContainerAsync(DockerContainer container)
    {
        _statusLabel.Text = "Removing container...";
        Application.Refresh();
        
        try
        {
            var success = await _dockerService.RemoveContainerAsync(container.Id);
            _statusLabel.Text = success ? "Container removed successfully" : "Failed to remove container";
            if (success)
            {
                await RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error removing container: {ex.Message}";
        }
    }
    
    private async Task RemoveProjectAsync(ComposeProject project)
    {
        _statusLabel.Text = "Removing project...";
        Application.Refresh();
        
        try
        {
            var success = await _dockerService.StopComposeProjectAsync(project);
            _statusLabel.Text = success ? "Project stopped successfully" : "Failed to stop project";
            if (success)
            {
                await RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error stopping project: {ex.Message}";
        }
    }
    
    private async Task ViewLogsAsync()
    {
        var selected = GetSelectedItem();
        if (selected is DockerContainer container)
        {
            _statusLabel.Text = $"Viewing logs for {container.Name} (feature not implemented yet)";
        }
        else if (selected is ComposeProject project)
        {
            _statusLabel.Text = $"Viewing logs for {project.Name} (feature not implemented yet)";
        }
        else
        {
            _statusLabel.Text = "No container or project selected";
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Dispose();
            _dockerService?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class ListItem
{
    public string Display { get; }
    public object Data { get; }
    
    public ListItem(string display, object data)
    {
        Display = display;
        Data = data;
    }
}