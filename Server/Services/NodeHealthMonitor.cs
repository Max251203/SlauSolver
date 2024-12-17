using Shared.Network;

namespace Server.Services;

public class NodeHealthMonitor
{
    private readonly Dictionary<int, DateTime> lastHeartbeat = new();
    private readonly Dictionary<int, int> failureCount = new();
    private readonly object healthLock = new object();
    private readonly TimeSpan heartbeatTimeout =
        TimeSpan.FromMilliseconds(NetworkConfiguration.Timeouts.HEALTH_CHECK_INTERVAL_MS);

    public void UpdateHeartbeat(int nodeId)
    {
        lock (healthLock)
        {
            lastHeartbeat[nodeId] = DateTime.Now;
            if (failureCount.ContainsKey(nodeId))
            {
                failureCount[nodeId] = 0; // Сброс счетчика ошибок при успешном heartbeat
            }
        }
    }

    public List<int> GetUnhealthyNodes()
    {
        lock (healthLock)
        {
            var now = DateTime.Now;
            return lastHeartbeat
                .Where(x => now - x.Value > heartbeatTimeout ||
                           (failureCount.ContainsKey(x.Key) &&
                            failureCount[x.Key] >= NetworkConfiguration.Retry.MAX_RETRIES))
                .Select(x => x.Key)
                .ToList();
        }
    }
}