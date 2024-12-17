using Shared.Network;

namespace Shared.Utils;

public static class BlockSizeOptimizer
{
    public static int CalculateOptimalBlockSize(int matrixSize, int nodesCount)
    {
        const int dataOverhead = 1000;
        int maxDataSize = NetworkConfiguration.Sizes.MAX_UDP_PACKET_SIZE - dataOverhead;

        int suggestedSize = (int)Math.Sqrt((double)(matrixSize * matrixSize) / nodesCount);
        int maxBlockSize = (int)Math.Sqrt(maxDataSize / sizeof(double));

        return Math.Min(
            Math.Max(NetworkConfiguration.Sizes.MIN_BLOCK_SIZE, suggestedSize),
            Math.Min(maxBlockSize, NetworkConfiguration.Sizes.MAX_BLOCK_SIZE)
        );
    }
}
