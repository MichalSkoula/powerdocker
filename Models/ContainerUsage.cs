namespace PowerDocker.Models;

public sealed record ContainerUsage(double? CpuPercent, ulong? MemoryBytes, ulong? MemoryLimitBytes = null)
{
    public DateTime SampledAt { get; init; } = DateTime.UtcNow;

    public static ContainerUsage? Sum(IEnumerable<DockerContainer> containers)
    {
        var running = containers.Where(c => c.IsRunning).ToArray();
        if (running.Length == 0) return null;
        double? cpu = running.All(c => c.Usage?.CpuPercent != null)
            ? running.Sum(c => c.Usage!.CpuPercent!.Value) : null;
        ulong? memory = running.All(c => c.Usage?.MemoryBytes != null)
            ? checked((ulong)running.Sum(c => (decimal)c.Usage!.MemoryBytes!.Value)) : null;
        return new ContainerUsage(cpu, memory);
    }
}
