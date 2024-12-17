namespace Server.Services;

public class LoadBalancer
{
    private readonly Dictionary<int, int> nodeLoadCount = new();
    private readonly Dictionary<int, DateTime> lastTaskTime = new();
    private readonly object balancerLock = new object();

    public int GetOptimalNode(int nodesCount)
    {
        lock (balancerLock)
        {
            // Очистка устаревших записей
            CleanupOldRecords();

            // Поиск узла с минимальной нагрузкой
            var minLoad = nodeLoadCount.Count == 0 ? 0 :
                nodeLoadCount.Min(x => x.Value);

            // Поиск всех узлов с минимальной нагрузкой
            var candidateNodes = Enumerable.Range(0, nodesCount)
                .Where(i => !nodeLoadCount.ContainsKey(i) ||
                           nodeLoadCount[i] == minLoad)
                .ToList();

            // Выбор узла с наибольшим временем простоя
            var selectedNode = candidateNodes
                .OrderBy(n => lastTaskTime.ContainsKey(n) ?
                    lastTaskTime[n] : DateTime.MinValue)
                .First();

            // Обновление состояния
            nodeLoadCount[selectedNode] = (nodeLoadCount.ContainsKey(selectedNode) ?
                nodeLoadCount[selectedNode] : 0) + 1;
            lastTaskTime[selectedNode] = DateTime.Now;

            return selectedNode;
        }
    }

    public void RegisterTaskCompletion(int nodeId)
    {
        lock (balancerLock)
        {
            if (nodeLoadCount.ContainsKey(nodeId))
            {
                nodeLoadCount[nodeId] = Math.Max(0, nodeLoadCount[nodeId] - 1);
            }
        }
    }

    private void CleanupOldRecords()
    {
        var threshold = DateTime.Now.AddMinutes(-5);
        var oldNodes = lastTaskTime
            .Where(x => x.Value < threshold)
            .Select(x => x.Key)
            .ToList();

        foreach (var nodeId in oldNodes)
        {
            lastTaskTime.Remove(nodeId);
            nodeLoadCount.Remove(nodeId);
        }
    }
} 