using PowerDocker.Models;
using PowerDocker.Services;
using Spectre.Console;

namespace PowerDocker.UI;

public sealed class MainWindow : IDisposable
{
    private readonly DockerService? _docker;
    private readonly bool _demo;
    private readonly BrowserState _browser = new();
    private Task<List<ComposeProject>>? _refresh;
    private Task<IReadOnlyDictionary<string, ContainerUsage?>>? _usageRefresh;
    private Task<bool>? _operation;
    private string _operationName = "";
    private string _message = "Connecting to Docker…";
    private string _messageStyle = "dim";
    private DateTime _nextRefresh = DateTime.MinValue;
    private volatile bool _quit;
    private bool _dirty = true;

    public MainWindow(bool demo = false)
    {
        _demo = demo;
        if (demo)
        {
            _browser.Update(DemoProjects.Create());
            _message = "Preview mode · Docker is not connected";
        }
        else
        {
            _docker = new DockerService();
        }
    }

    public void Run()
    {
        if (!AnsiConsole.Profile.Capabilities.Ansi)
            throw new InvalidOperationException("Use a terminal with ANSI support (such as Windows Terminal).");

        ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; _quit = true; };
        Console.CancelKeyPress += onCancel;
        try
        {
            AnsiConsole.AlternateScreen(() =>
            {
                using var screen = new TerminalScreen(AnsiConsole.Console);
                AnsiConsole.Cursor.Hide();
                try
                {
                    var previousSize = (0, 0);
                    while (!_quit)
                    {
                        // Only this loop mutates UI state. Docker requests run asynchronously.
                        CompleteRequests();
                        if (!_demo && _operation == null && _refresh == null && DateTime.UtcNow >= _nextRefresh)
                            Refresh();

                        while (Console.KeyAvailable && !_quit)
                            HandleKey(Console.ReadKey(intercept: true));
                        if (_quit) break;

                        var size = (AnsiConsole.Profile.Width, AnsiConsole.Profile.Height);
                        if (_dirty || size != previousSize)
                        {
                            screen.Draw(Dashboard.Render(_browser, size.Item1, size.Item2,
                                _message, _messageStyle, _operation != null, _demo), size.Item1, size.Item2);
                            previousSize = size;
                            _dirty = false;
                        }
                        // Process queued keys immediately; only idle iterations wait briefly.
                        if (!Console.KeyAvailable) Thread.Sleep(5);
                    }
                }
                finally
                {
                    AnsiConsole.ResetDecoration();
                    AnsiConsole.Cursor.Show();
                }
            });
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        var previousSelection = _browser.Selection;
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: case ConsoleKey.K: _browser.Move(-1); break;
            case ConsoleKey.DownArrow: case ConsoleKey.J: _browser.Move(1); break;
            case ConsoleKey.Home: _browser.Move(-_browser.Items.Count); break;
            case ConsoleKey.End: _browser.Move(_browser.Items.Count); break;
            case ConsoleKey.PageUp: _browser.Move(-Math.Max(1, AnsiConsole.Profile.Height - 14)); break;
            case ConsoleKey.PageDown: _browser.Move(Math.Max(1, AnsiConsole.Profile.Height - 14)); break;
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                if (_browser.Selected is { Container: null })
                {
                    _browser.ToggleProject();
                    _dirty = true;
                }
                break;
            case ConsoleKey.F5:
                if (!_demo && _operation == null && _refresh == null) Refresh();
                break;
            case ConsoleKey.R: BeginOperation(stop: false); break;
            case ConsoleKey.S: BeginOperation(stop: true); break;
            case ConsoleKey.Q: case ConsoleKey.E: case ConsoleKey.Escape: _quit = true; break;
        }
        _dirty |= previousSelection != _browser.Selection;
    }

    private void Refresh()
    {
        _refresh = _docker!.GetComposeProjectsAsync();
        _nextRefresh = DateTime.UtcNow.AddSeconds(5);
    }

    private void CompleteRequests()
    {
        if (_refresh is { IsCompleted: true })
        {
            try
            {
                _browser.Update(_refresh.GetAwaiter().GetResult());
                if (_usageRefresh == null)
                {
                    var ids = _browser.Projects.SelectMany(p => p.Containers).Where(c => c.IsRunning).Select(c => c.Id).ToArray();
                    _usageRefresh = _docker!.GetUsageAsync(ids);
                }
                if (_message == "Connecting to Docker…" || _message.StartsWith("Docker unavailable:", StringComparison.Ordinal))
                    SetMessage("Ready · auto-refresh every 5s", "dim");
            }
            catch (Exception ex)
            {
                SetMessage($"Docker unavailable: {ex.Message} · F5 to retry", "red");
            }
            _refresh = null;
            _nextRefresh = DateTime.UtcNow.AddSeconds(5);
            _dirty = true;
        }

        if (_usageRefresh is { IsCompleted: true })
        {
            try { _browser.ApplyUsage(_usageRefresh.GetAwaiter().GetResult()); }
            catch { _browser.ApplyUsage(new Dictionary<string, ContainerUsage?>()); }
            _usageRefresh = null;
            _dirty = true;
        }

        if (_operation is { IsCompleted: true })
        {
            try
            {
                var success = _operation.GetAwaiter().GetResult();
                SetMessage(success ? $"{_operationName} completed" : $"{_operationName} failed · check Docker", success ? "green" : "red");
            }
            catch (Exception ex)
            {
                SetMessage($"{_operationName} failed: {ex.Message}", "red");
            }
            _operation = null;
            Refresh();
        }
    }

    private void BeginOperation(bool stop)
    {
        if (_demo || _operation != null || _refresh != null || _browser.Selected == null) return;

        var selected = _browser.Selected;
        var running = selected.Container?.IsRunning ?? selected.Project.AnyRunning;
        var verb = stop ? "Stop" : running ? "Restart" : "Start";
        var name = selected.Container?.Name ?? selected.Project.Name;
        _operationName = $"{verb}: {name}";
        SetMessage($"{_operationName}…", "yellow");

        if (selected.Container is { } container)
            _operation = stop ? _docker!.StopContainerAsync(container.Id)
                : running ? _docker!.RestartContainerAsync(container.Id)
                : _docker!.StartContainerAsync(container.Id);
        else
            _operation = stop ? _docker!.StopComposeProjectAsync(selected.Project)
                : running ? _docker!.RestartComposeProjectAsync(selected.Project)
                : _docker!.StartComposeProjectAsync(selected.Project);
    }

    private void SetMessage(string message, string style)
    {
        _message = message;
        _messageStyle = style;
        _dirty = true;
    }

    public void Dispose()
    {
        _docker?.Dispose();
        // Observe a pending refresh's failure if the user quits before it finishes.
        if (_refresh != null)
            _ = _refresh.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        if (_usageRefresh != null)
            _ = _usageRefresh.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
