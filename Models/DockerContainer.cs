namespace PowerDocker.Models;

public class DockerContainer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ComposeProject { get; set; } = string.Empty;
    public string ComposeService { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    
    public bool IsRunning => State == "running";
}