using Docker.DotNet;
using Docker.DotNet.Models;
using PowerDocker.Models;

namespace PowerDocker.Services;

public class DockerService
{
    private readonly DockerClient _dockerClient;
    
    public DockerService()
    {
        try
        {
            // Try different Docker endpoints
            var config = new DockerClientConfiguration();
            _dockerClient = config.CreateClient();
        }
        catch (Exception ex)
        {
            // Try with explicit Unix socket
            try
            {
                var config = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"));
                _dockerClient = config.CreateClient();
            }
            catch
            {
                throw new InvalidOperationException("Cannot connect to Docker daemon. Make sure Docker is running and accessible.", ex);
            }
        }
    }
    
    public async Task<List<ComposeProject>> GetComposeProjectsAsync()
    {
        var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true
        });
        
        var dockerContainers = containers.Select(MapToDockerContainer).ToList();
        
        return GroupByComposeProject(dockerContainers);
    }
    
    public async Task<bool> StartContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> StopContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RestartContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters());
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RemoveContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
            {
                Force = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> StartComposeProjectAsync(ComposeProject project)
    {
        var tasks = project.Containers.Select(c => StartContainerAsync(c.Id));
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }
    
    public async Task<bool> StopComposeProjectAsync(ComposeProject project)
    {
        var tasks = project.Containers.Select(c => StopContainerAsync(c.Id));
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }
    
    public async Task<bool> RestartComposeProjectAsync(ComposeProject project)
    {
        var tasks = project.Containers.Select(c => RestartContainerAsync(c.Id));
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }
    
    private static DockerContainer MapToDockerContainer(ContainerListResponse container)
    {
        var composeProject = container.Labels?.TryGetValue("com.docker.compose.project", out var project) == true ? project : "";
        var composeService = container.Labels?.TryGetValue("com.docker.compose.service", out var service) == true ? service : "";
        
        if (string.IsNullOrEmpty(composeProject))
        {
            composeProject = "Standalone";
        }
        
        return new DockerContainer
        {
            Id = container.ID,
            Name = container.Names?.FirstOrDefault()?.TrimStart('/') ?? "",
            Status = container.Status,
            State = container.State,
            ComposeProject = composeProject,
            ComposeService = composeService,
            Image = container.Image
        };
    }
    
    private static List<ComposeProject> GroupByComposeProject(List<DockerContainer> containers)
    {
        return containers
            .GroupBy(c => c.ComposeProject)
            .Select(g => new ComposeProject
            {
                Name = g.Key,
                Containers = g.OrderBy(c => c.Name).ToList()
            })
            .OrderBy(p => p.Name)
            .ToList();
    }
    
    public void Dispose()
    {
        _dockerClient?.Dispose();
    }
}