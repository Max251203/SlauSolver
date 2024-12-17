using Shared.Models;

namespace Node.Services;

public class BlockResultAssembler
{
    private readonly Dictionary<(int row, int col), Dictionary<int, string>> blockChunks = new();
    private readonly Dictionary<(int row, int col), int> expectedChunks = new();
    private readonly Dictionary<(int row, int col), HashSet<int>> receivedChunks = new();
    private readonly object assemblyLock = new object();

    public bool TryAddChunk(NetworkMessages.BlockResultChunk chunk)
    {
        lock (assemblyLock)
        {
            var key = (chunk.BlockRow, chunk.BlockCol);

            if (!blockChunks.ContainsKey(key))
            {
                blockChunks[key] = new Dictionary<int, string>();
                expectedChunks[key] = chunk.TotalChunks;
                receivedChunks[key] = new HashSet<int>();
            }

            if (!receivedChunks[key].Contains(chunk.ChunkId))
            {
                blockChunks[key][chunk.ChunkId] = chunk.Data;
                receivedChunks[key].Add(chunk.ChunkId);
            }

            return IsBlockComplete(key);
        }
    }

    public void ConfirmChunkReceived(ChunkAck ack)
    {
        var key = (ack.BlockRow, ack.BlockCol);
        lock (assemblyLock)
        {
            if (receivedChunks.ContainsKey(key))
            {
                receivedChunks[key].Add(ack.ChunkId);
            }
        }
    }

    private bool IsBlockComplete((int row, int col) key)
    {
        return receivedChunks[key].Count == expectedChunks[key];
    }

    public NetworkMessages.BlockResult AssembleBlock(int row, int col)
    {
        lock (assemblyLock)
        {
            var key = (row, col);
            if (!IsBlockComplete(key))
            {
                throw new InvalidOperationException($"Попытка собрать неполный блок [{row}, {col}]");
            }

            try
            {
                var chunks = blockChunks[key];
                var orderedChunks = Enumerable.Range(0, expectedChunks[key])
                                            .Select(i => chunks[i])
                                            .ToList();

                var blockData = BlockTransmissionService.ReassembleBlock(orderedChunks);

                // Очистка использованных данных
                blockChunks.Remove(key);
                expectedChunks.Remove(key);
                receivedChunks.Remove(key);

                return new NetworkMessages.BlockResult
                {
                    BlockRow = row,
                    BlockCol = col,
                    BlockData = blockData,
                    Type = "RESULT"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сборке блока [{row}, {col}]: {ex.Message}");
                throw;
            }
        }
    }

    public IEnumerable<(int row, int col)> GetIncompleteBlocks()
    {
        lock (assemblyLock)
        {
            return blockChunks.Keys
                .Where(key => !IsBlockComplete(key))
                .ToList();
        }
    }

    public void Clear()
    {
        lock (assemblyLock)
        {
            blockChunks.Clear();
            expectedChunks.Clear();
            receivedChunks.Clear();
        }
    }
}