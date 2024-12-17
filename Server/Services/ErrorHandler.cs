using Shared.Network;

namespace Server.Services;

public class ErrorHandler
{
    private readonly Dictionary<(int row, int col), int> blockRetryCount = new();
    private readonly Dictionary<(int row, int col), List<Exception>> blockErrors = new();
    private readonly object errorLock = new object();

    public bool ShouldRetryBlock((int row, int col) blockKey)
    {
        lock (errorLock)
        {
            if (!blockRetryCount.ContainsKey(blockKey))
            {
                blockRetryCount[blockKey] = 0;
            }

            if (blockRetryCount[blockKey] < NetworkConfiguration.Retry.MAX_BLOCK_RETRIES)
            {
                blockRetryCount[blockKey]++;
                return true;
            }

            return false;
        }
    }

    public void ResetRetryCount((int row, int col) blockKey)
    {
        lock (errorLock)
        {
            blockRetryCount.Remove(blockKey);
            blockErrors.Remove(blockKey);
        }
    }
}
