using PowerDocker.Models;

namespace PowerDocker.UI;

internal static class DemoProjects
{
    public static List<ComposeProject> Create() =>
    [
        Project("storefront",
            Container("storefront-web-1", "web", "running", "nginx:alpine", "Up 2 hours"),
            Container("storefront-api-1", "api", "running", "storefront-api:latest", "Up 2 hours (healthy)"),
            Container("storefront-db-1", "db", "running", "postgres:16", "Up 2 hours (healthy)")),
        Project("observability",
            Container("observability-grafana-1", "grafana", "running", "grafana/grafana:latest", "Up 45 minutes"),
            Container("observability-prometheus-1", "prometheus", "exited", "prom/prometheus:latest", "Exited (0) 10 minutes ago")),
        Project("playground",
            Container("playground-redis-1", "redis", "exited", "redis:7-alpine", "Exited (0) yesterday")),
        Project("Standalone",
            Container("local-registry", "", "running", "registry:2", "Up 3 days"))
    ];

    private static ComposeProject Project(string name, params DockerContainer[] containers)
    {
        foreach (var container in containers) container.ComposeProject = name;
        return new ComposeProject { Name = name, Containers = containers.ToList() };
    }

    private static DockerContainer Container(string name, string service, string state, string image, string status) => new()
    {
        Id = name,
        Name = name,
        ComposeService = service,
        State = state,
        Image = image,
        Status = status
    };
}
