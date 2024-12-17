using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Shared.Network;

public class UdpHelper
{
    private const int MAX_PACKET_SIZE = 60000;
    private const int MAX_RETRIES = 3;
    private const int RETRY_DELAY_MS = 100;

    public static async Task SendAsync<T>(UdpClient client, T data, IPEndPoint endpoint)
    {
        string serializedData = JsonSerializer.Serialize(data);
        byte[] bytes = Encoding.UTF8.GetBytes(serializedData);

        if (bytes.Length > MAX_PACKET_SIZE)
        {
            throw new InvalidOperationException($"Размер данных превышает максимально допустимый: {bytes.Length} байт");
        }

        for (int i = 0; i < MAX_RETRIES; i++)
        {
            try
            {
                await client.SendAsync(bytes, bytes.Length, endpoint);
                return;
            }
            catch (Exception) when (i < MAX_RETRIES - 1)
            {
                await Task.Delay(RETRY_DELAY_MS);
            }
        }
    }

    public static async Task<T> ReceiveAsync<T>(UdpClient client, int timeoutMs = 500000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var receiveTask = client.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cts.Token)) == receiveTask)
            {
                var result = await receiveTask;
                string data = Encoding.UTF8.GetString(result.Buffer);
                return JsonSerializer.Deserialize<T>(data);
            }
            throw new TimeoutException("Превышено время ожидания ответа");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Превышено время ожидания ответа");
        }
    }

    public static List<string> PrepareDataChunks(double[,] matrix, double[] vector)
    {
        var chunks = new List<string>();
        var sb = new StringBuilder();
        int currentSize = 0;
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            var rowBuilder = new StringBuilder();
            for (int j = 0; j < cols; j++)
            {
                rowBuilder.Append(matrix[i, j].ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
                         .Append(' ');
            }
            rowBuilder.Append('|')
                     .Append(vector[i].ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            string row = rowBuilder.ToString();

            if (currentSize + row.Length > MAX_PACKET_SIZE)
            {
                chunks.Add(sb.ToString());
                sb.Clear();
                currentSize = 0;
            }

            sb.AppendLine(row);
            currentSize += row.Length + Environment.NewLine.Length;
        }

        if (sb.Length > 0)
        {
            chunks.Add(sb.ToString());
        }

        return chunks;
    }
}