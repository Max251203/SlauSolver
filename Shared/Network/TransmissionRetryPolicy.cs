using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

namespace Shared.Network;

public class TransmissionRetryPolicy
{
    private readonly int maxRetries;
    private readonly int baseDelayMs;
    private readonly Random random;

    public TransmissionRetryPolicy(int maxRetries = 3, int baseDelayMs = 100)
    {
        this.maxRetries = maxRetries;
        this.baseDelayMs = baseDelayMs;
        this.random = new Random();
    }

    public async Task ExecuteWithRetryAsync(Func<Task> action)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception) when (attempt < maxRetries - 1)
            {
                int delayMs = CalculateDelay(attempt);
                await Task.Delay(delayMs);
            }
        }
    }

    private int CalculateDelay(int attempt)
    {
        // Экспоненциальная задержка с случайным компонентом
        int exponentialDelay = baseDelayMs * (int)Math.Pow(2, attempt);
        int jitter = random.Next(-baseDelayMs / 2, baseDelayMs / 2);
        return exponentialDelay + jitter;
    }
} 