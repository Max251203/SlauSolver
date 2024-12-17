using Shared.Models;
using Shared.Network;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Node.Services;

public class BlockTransmissionService
{
    private const int MAX_CHUNK_SIZE = 58000;

    public static List<NetworkMessages.BlockResultChunk> PrepareBlockResultChunks(NetworkMessages.BlockResult result)
    {
        var chunks = new List<NetworkMessages.BlockResultChunk>();

        // Оптимизируем сериализацию блока данных
        var blockData = new
        {
            result.BlockData.Data,
            result.BlockData.Rows,
            result.BlockData.Cols
        };

        var serializedBlock = JsonSerializer.Serialize(blockData, new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true
        });

        int totalChunks = (int)Math.Ceiling((double)serializedBlock.Length / MAX_CHUNK_SIZE);
        Console.WriteLine($"Размер данных: {serializedBlock.Length} байт, будет разделен на {totalChunks} чанков");

        for (int i = 0; i < serializedBlock.Length; i += MAX_CHUNK_SIZE)
        {
            int chunkSize = Math.Min(MAX_CHUNK_SIZE, serializedBlock.Length - i);
            var chunk = new NetworkMessages.BlockResultChunk
            {
                BlockRow = result.BlockRow,
                BlockCol = result.BlockCol,
                NodeId = result.NodeId,
                ChunkId = chunks.Count,
                TotalChunks = totalChunks,
                Data = serializedBlock.Substring(i, chunkSize)
            };
            chunks.Add(chunk);
        }

        return chunks;
    }

    public static MatrixBlock ReassembleBlock(List<string> chunkData)
    {
        var completeData = string.Concat(chunkData);
        using JsonDocument document = JsonDocument.Parse(completeData);
        JsonElement root = document.RootElement;

        // Получаем массив данных
        double[] data = root.GetProperty("Data").EnumerateArray()
            .Select(element => element.GetDouble())
            .ToArray();

        // Получаем размерности
        int rows = root.GetProperty("Rows").GetInt32();
        int cols = root.GetProperty("Cols").GetInt32();

        return new MatrixBlock
        {
            Data = data,
            Rows = rows,
            Cols = cols
        };
    }
}