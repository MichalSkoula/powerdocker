using PowerDocker.Models;

namespace PowerDocker.UI;

internal static class DemoProjects
{
    public static List<ComposeProject> Create() =>
    [
        Project("storefront",
            Container("storefront-web-1", "web", "running", "nginx:alpine", "Up 2 hours", 0.3, 18),
            Container("storefront-api-1", "api", "running", "storefront-api:latest", "Up 2 hours (healthy)", 12.4, 164),
            Container("storefront-db-1", "db", "running", "postgres:16", "Up 2 hours (healthy)", 2.1, 246)),
        Project("observability",
            Container("observability-grafana-1", "grafana", "running", "grafana/grafana:latest", "Up 45 minutes", 0.8, 92),
            Container("observability-prometheus-1", "prometheus", "exited", "prom/prometheus:latest", "Exited (0) 10 minutes ago")),
        Project("playground",
            Container("playground-redis-1", "redis", "exited", "redis:7-alpine", "Exited (0) yesterday")),
        Project("Standalone",
            Container("local-registry", "", "running", "registry:2", "Up 3 days", 0.1, 24))
    ];

    private static ComposeProject Project(string name, params DockerContainer[] containers)
    {
        foreach (var container in containers) container.ComposeProject = name;
        return new ComposeProject { Name = name, Containers = containers.ToList() };
    }

    private static DockerContainer Container(string name, string service, string state, string image, string status,
        double? cpu = null, ulong? memoryMiB = null) => new()
        {
            Id = name,
            Name = name,
            ComposeService = service,
            State = state,
            Image = image,
            Status = status,
            Usage = state == "running" ? new ContainerUsage(cpu, memoryMiB * 1024 * 1024) : null
        };
}
