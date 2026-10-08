using Docker.DotNet.Models;
using PowerDocker.Models;

namespace PowerDocker.Services;

public static class UsageCalculator
{
    // Same CPU and cache-adjusted memory calculations as docker stats.
    public static ContainerUsage FromStats(ContainerStatsResponse stats)
    {
        var current = stats.CPUStats;
        var previous = stats.PreCPUStats;
        var windows = stats.NumProcs > 0 && (current?.SystemUsage ?? 0) == 0;
        double? cpu = null;
        if (current?.CPUUsage != null && previous?.CPUUsage != null &&
            current.CPUUsage.TotalUsage >= previous.CPUUsage.TotalUsage)
        {
            var delta = current.CPUUsage.TotalUsage - previous.CPUUsage.TotalUsage;
            if (windows)
            {
                var elapsed = stats.Read - stats.PreRead;
                if (stats.PreRead != default && elapsed.Ticks > 0)
                    cpu = (double)delta / elapsed.Ticks / stats.NumProcs * 100;
            }
            else if (previous.SystemUsage > 0 && current.SystemUsage > previous.SystemUsage)
            {
                var processors = current.OnlineCPUs > 0 ? current.OnlineCPUs : (uint)(current.CPUUsage.PercpuUsage?.Count ?? 0);
                if (processors > 0)
                    cpu = (double)delta / (current.SystemUsage - previous.SystemUsage) * processors * 100;
            }
        }

        ulong? memory = null;
        ulong? limit = null;
        if (stats.MemoryStats is { } memoryStats)
        {
            memory = windows ? memoryStats.PrivateWorkingSet : memoryStats.Usage;
            if (!windows)
            {
                if (memoryStats.Stats is { } values)
                {
                    ulong cache = 0;
                    if (!values.TryGetValue("total_inactive_file", out cache))
                        values.TryGetValue("inactive_file", out cache);
                    if (cache < memoryStats.Usage) memory -= cache;
                }
                if (memoryStats.Limit > 0) limit = memoryStats.Limit;
            }
        }
        return new ContainerUsage(cpu, memory, limit);
    }
}
