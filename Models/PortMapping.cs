namespace PowerDocker.Models;

public sealed record PortMapping(ushort HostPort, ushort ContainerPort, string Protocol = "tcp");
