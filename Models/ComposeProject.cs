namespace PowerDocker.Models;

public class ComposeProject
{
    public string Name { get; set; } = string.Empty;
    public List<DockerContainer> Containers { get; set; } = new();
    
    public bool AllRunning => Containers.All(c => c.IsRunning);
    public bool AnyRunning => Containers.Any(c => c.IsRunning);
    public int RunningCount => Containers.Count(c => c.IsRunning);
    public int TotalCount => Containers.Count;
}