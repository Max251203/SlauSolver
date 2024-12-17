using Shared.Network;
using Shared.Utils;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Shared.Solvers;
using static Shared.Models.NetworkMessages;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows;
using System.Xml.Linq;

namespace Client.Services;

public class MatrixTransmissionService
{
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _serverEndPoint;

    public MatrixTransmissionService(UdpClient udpClient, IPEndPoint serverEndPoint)
    {
        _udpClient = udpClient;
        _serverEndPoint = serverEndPoint;
    }

    public async Task<(double[,] matrix, double[] vector)> SendMatrix(int rows, int cols, int nodesCount)
    {
        var (matrix, vector) = MatrixGenerator.Generate(rows, cols);

        // Отправка информации о размерах
        var initData = $"INIT|{nodesCount}|{rows}|{cols}";
        var initBytes = Encoding.UTF8.GetBytes(initData);
        await _udpClient.SendAsync(initBytes, initBytes.Length, _serverEndPoint);
        await Task.Delay(100);

        // Разбиение и отправка данных
        var dataChunks = UdpHelper.PrepareDataChunks(matrix, vector);

        // Отправка количества чанков
        var chunksCountData = $"COUNT|{dataChunks.Count}";
        var chunksCountBytes = Encoding.UTF8.GetBytes(chunksCountData);
        await _udpClient.SendAsync(chunksCountBytes, chunksCountBytes.Length, _serverEndPoint);
        await Task.Delay(100);

        // Отправка чанков
        await SendChunks(dataChunks);

        return (matrix, vector);
    }

    private async Task SendChunks(List<string> chunks)
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkData = $"CHUNK|{i}|{chunks[i]}";
            var chunkBytes = Encoding.UTF8.GetBytes(chunkData);
            await _udpClient.SendAsync(chunkBytes, chunkBytes.Length, _serverEndPoint);
            await Task.Delay(50);
            Console.WriteLine($"Отправлена часть {i + 1} из {chunks.Count}");
        }
    }
}