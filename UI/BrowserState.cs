using PowerDocker.Models;

namespace PowerDocker.UI;

public sealed record BrowserItem(ComposeProject Project, DockerContainer? Container = null)
{
    public string Key => Container is { } c ? $"container:{c.Id}" : $"project:{Project.Name}";
}

public sealed class BrowserState
{
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    public IReadOnlyList<ComposeProject> Projects { get; private set; } = [];
    public List<BrowserItem> Items { get; } = [];
    public int Selection { get; private set; }
    public int ScrollOffset { get; private set; }
    public BrowserItem? Selected => Items.Count == 0 ? null : Items[Selection];
    public bool IsCollapsed(ComposeProject project) => _collapsed.Contains(project.Name);

    public void Update(IReadOnlyList<ComposeProject> projects)
    {
        var selectedKey = Selected?.Key;
        var selectedProject = Selected?.Project.Name;
        var previousUsage = Projects.SelectMany(p => p.Containers).Where(c => c.IsRunning)
            .ToDictionary(c => c.Id, c => c.Usage);
        foreach (var container in projects.SelectMany(p => p.Containers))
        {
            if (container.IsRunning && container.Usage == null && previousUsage.TryGetValue(container.Id, out var usage)
                && usage != null && DateTime.UtcNow - usage.SampledAt < TimeSpan.FromSeconds(15))
                container.Usage = usage;
            if (!container.IsRunning) container.Usage = null;
        }
        Projects = projects;
        _collapsed.IntersectWith(projects.Select(p => p.Name));
        Rebuild();
        var index = Items.FindIndex(item => item.Key == selectedKey);
        if (index < 0 && selectedProject != null)
            index = Items.FindIndex(item => item.Container == null && item.Project.Name == selectedProject);
        Selection = index >= 0 ? index : Math.Clamp(Selection, 0, Math.Max(0, Items.Count - 1));
    }

    public void Move(int delta) => Selection = Math.Clamp(Selection + delta, 0, Math.Max(0, Items.Count - 1));

    public void ApplyUsage(IReadOnlyDictionary<string, ContainerUsage?> usage)
    {
        foreach (var container in Projects.SelectMany(p => p.Containers))
            container.Usage = container.IsRunning && usage.TryGetValue(container.Id, out var value) ? value : null;
    }

    public void ToggleProject()
    {
        if (Selected is not { Container: null } selected) return;
        var key = selected.Key;
        if (!_collapsed.Add(selected.Project.Name)) _collapsed.Remove(selected.Project.Name);
        Rebuild();
        Selection = Items.FindIndex(item => item.Key == key);
    }

    public IEnumerable<(BrowserItem Item, bool Selected)> VisibleItems(int count)
    {
        count = Math.Max(1, count);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, Items.Count - count));
        if (Selection < ScrollOffset) ScrollOffset = Selection;
        if (Selection >= ScrollOffset + count) ScrollOffset = Selection - count + 1;
        return Items.Skip(ScrollOffset).Take(count).Select((item, i) => (item, ScrollOffset + i == Selection));
    }

    private void Rebuild()
    {
        Items.Clear();
        foreach (var project in Projects)
        {
            Items.Add(new BrowserItem(project));
            if (!IsCollapsed(project)) Items.AddRange(project.Containers.Select(c => new BrowserItem(project, c)));
        }
    }
}
