using Shared.Models;
using Shared.Network;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using Server.Services;
using Shared.Utils;
using Node.Services;

public class ServerInitializer
{
    private readonly UdpClient udpServer;
    private readonly TaskDistributionService taskDistributionService;
    private readonly NodeManagementService nodeManagementService;
    private readonly SolutionAssemblyService solutionAssemblyService;
    private readonly BlockResultAssembler blockResultAssembler;
    private readonly LoadBalancer loadBalancer;
    private readonly NodeHealthMonitor healthMonitor;
    private readonly ErrorHandler errorHandler;
    private readonly NetworkMetrics networkMetrics;
    private readonly Stopwatch distributedSolveTimer;

    private List<string> receivedChunks;
    private int expectedChunksCount;
    private bool isReceivingChunks;
    private int matrixRows;
    private int matrixCols;
    private bool isInitialized;
    private int totalNodes;
    private BlockMatrix blockMatrix;
    private double[] vector;
    private IPEndPoint clientEndPoint;

    public ServerInitializer(int port)
    {
        udpServer = new UdpClient(port);
        udpServer.Client.ReceiveBufferSize = NetworkConfiguration.Sizes.RECEIVE_BUFFER_SIZE;
        udpServer.Client.SendBufferSize = NetworkConfiguration.Sizes.SEND_BUFFER_SIZE;

        taskDistributionService = new TaskDistributionService();
        nodeManagementService = new NodeManagementService();
        solutionAssemblyService = new SolutionAssemblyService(udpServer);
        blockResultAssembler = new BlockResultAssembler();
        loadBalancer = new LoadBalancer();
        healthMonitor = new NodeHealthMonitor();
        errorHandler = new ErrorHandler();
        networkMetrics = new NetworkMetrics();
        distributedSolveTimer = new Stopwatch();

        StartHealthCheck();
    }

    private async void StartHealthCheck()
    {
        while (true)
        {
            var unhealthyNodes = healthMonitor.GetUnhealthyNodes();
            foreach (var nodeId in unhealthyNodes)
            {
                await HandleUnhealthyNode(nodeId);
            }
            await Task.Delay(NetworkConfiguration.Timeouts.HEALTH_CHECK_INTERVAL_MS);
        }
    }

    private async Task HandleUnhealthyNode(int nodeId)
    {
        Console.WriteLine($"Обнаружен неактивный узел: {nodeId}");
        var affectedBlocks = taskDistributionService.GetNodeBlocks(nodeId);

        foreach (var block in affectedBlocks)
        {
            if (errorHandler.ShouldRetryBlock(block))
            {
                int newNodeId = loadBalancer.GetOptimalNode(totalNodes);
                await RedistributeBlock(block, newNodeId);
            }
            else
            {
                Console.WriteLine($"Превышено количество попыток для блока {block}");
            }
        }
    }

    private async Task RedistributeBlock((int row, int col) block, int newNodeId)
    {
        var task = taskDistributionService.CreateBlockTask(block.row, block.col, blockMatrix);
        await SendTaskToNode(newNodeId, task);
        Console.WriteLine($"Блок [{block.row}, {block.col}] перераспределен на узел {newNodeId}");
    }

    public async Task Start()
    {
        Console.WriteLine("Сервер запущен...");
        await ProcessIncomingData();
    }

    private async Task ProcessIncomingData()
    {
        while (true)
        {
            try
            {
                var result = await udpServer.ReceiveAsync();
                networkMetrics.RecordReceivedData(result.Buffer.Length);

                string data = Encoding.UTF8.GetString(result.Buffer);

                if (data.StartsWith("INIT"))
                {
                    ProcessInitMessage(data, result.RemoteEndPoint);
                }
                else if (data.StartsWith("COUNT"))
                {
                    ProcessCountMessage(data);
                }
                else if (data.StartsWith("CHUNK"))
                {
                    ProcessChunk(data);
                }
                else if (data.StartsWith("ACK"))
                {
                    ProcessAcknowledgement(data);
                }
                else if (data.StartsWith("HEARTBEAT"))
                {
                    ProcessHeartbeat(data, result.RemoteEndPoint.Port - NetworkConfiguration.Ports.BASE_NODE_PORT);
                }
                else
                {
                    try
                    {
                        var blockChunk = JsonSerializer.Deserialize<NetworkMessages.BlockResultChunk>(data);
                        await ProcessBlockResultChunk(blockChunk);
                    }
                    catch
                    {
                        Console.WriteLine($"Получены неизвестные данные: {data.Substring(0, Math.Min(100, data.Length))}...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
            }
        }
    }

    private void ProcessInitMessage(string data, IPEndPoint clientEP)
    {
        var parts = data.Split('|');
        totalNodes = int.Parse(parts[1]);
        matrixRows = int.Parse(parts[2]);
        matrixCols = int.Parse(parts[3]);
        clientEndPoint = clientEP;
        isInitialized = true;

        Console.WriteLine($"Инициализация: матрица {matrixRows}x{matrixCols}, узлов: {totalNodes}");
    }

    private void ProcessCountMessage(string data)
    {
        if (!isInitialized)
        {
            throw new Exception("Получено сообщение о количестве чанков до инициализации");
        }

        var parts = data.Split('|');
        expectedChunksCount = int.Parse(parts[1]);
        receivedChunks = new List<string>(expectedChunksCount);
        isReceivingChunks = true;

        Console.WriteLine($"Ожидается {expectedChunksCount} частей данных");
    }

    private async void ProcessChunk(string data)
    {
        if (!isReceivingChunks) return;

        try
        {
            var parts = data.Split(new[] { '|' }, 3);
            int chunkIndex = int.Parse(parts[1]);
            string chunkData = parts[2];

            while (receivedChunks.Count <= chunkIndex)
            {
                receivedChunks.Add(null);
            }
            receivedChunks[chunkIndex] = chunkData;

            Console.WriteLine($"Получена часть {chunkIndex + 1} из {expectedChunksCount}");

            if (receivedChunks.Count == expectedChunksCount && !receivedChunks.Contains(null))
            {
                isReceivingChunks = false;
                var completeData = string.Concat(receivedChunks);
                await ProcessCompleteData(completeData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке чанка: {ex.Message}\n{ex.StackTrace}");
        }
    }
    private void ProcessAcknowledgement(string data)
    {
        var ack = JsonSerializer.Deserialize<ChunkAck>(data.Substring(4));
        blockResultAssembler.ConfirmChunkReceived(ack);
    }

    private void ProcessHeartbeat(string data, int nodeId)
    {
        healthMonitor.UpdateHeartbeat(nodeId);
    }

    private async Task ProcessCompleteData(string data)
    {
        try
        {
            var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine($"Получено строк данных: {lines.Length}");

            if (lines.Length != matrixRows)
            {
                throw new Exception($"Несоответствие количества строк: ожидалось {matrixRows}, получено {lines.Length}");
            }

            (var matrix, vector) = ParseMatrixData(lines);

            int blockSize = BlockSizeOptimizer.CalculateOptimalBlockSize(Math.Min(matrixRows, matrixCols), totalNodes);
            Console.WriteLine($"Оптимальный размер блока: {blockSize}x{blockSize}");

            blockMatrix = new BlockMatrix(matrix, blockSize);

            await nodeManagementService.StartNodes(totalNodes);
            Console.WriteLine("Ожидание инициализации узлов...");
            await Task.Delay(NetworkConfiguration.Timeouts.NODE_INITIALIZATION_DELAY_MS);

            taskDistributionService.Initialize(blockMatrix, vector);
            solutionAssemblyService.Initialize(blockMatrix.BlocksInRow * blockMatrix.BlocksInCol, clientEndPoint, totalNodes, blockMatrix);

            distributedSolveTimer.Restart();
            Console.WriteLine("Начало распределения задач...");

            // Распределяем все блоки матрицы
            for (int i = 0; i < blockMatrix.BlocksInRow; i++)
            {
                for (int j = 0; j < blockMatrix.BlocksInCol; j++)
                {
                    int targetNode = loadBalancer.GetOptimalNode(totalNodes);
                    var task = taskDistributionService.CreateBlockTask(i, j, blockMatrix);
                    taskDistributionService.AssignBlockToNode(targetNode, (i, j));

                    try
                    {
                        await SendTaskToNode(targetNode, task);
                        Console.WriteLine($"Распределен блок [{i}, {j}] узлу {targetNode}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при отправке блока [{i}, {j}] узлу {targetNode}: {ex.Message}");
                        // Попытка переназначить блок другому узлу
                        targetNode = (targetNode + 1) % totalNodes;
                        await SendTaskToNode(targetNode, task);
                        Console.WriteLine($"Блок [{i}, {j}] переназначен узлу {targetNode}");
                    }

                    await Task.Delay(NetworkConfiguration.Timeouts.TASK_DISTRIBUTION_DELAY_MS);
                }
            }

            Console.WriteLine("Все задачи распределены, ожидание результатов...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке данных: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task SendTaskToNode(int nodeId, NetworkMessages.BlockTask task)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"),
            NetworkConfiguration.Ports.GetNodePort(nodeId));

        var retryPolicy = new TransmissionRetryPolicy();
        await retryPolicy.ExecuteWithRetryAsync(async () =>
        {
            var jsonData = JsonSerializer.Serialize(task);
            var bytes = Encoding.UTF8.GetBytes(jsonData);
            await udpServer.SendAsync(bytes, bytes.Length, endpoint);
            networkMetrics.RecordSentData(bytes.Length);
        });
    }

    private async Task DistributeTasksWithLoadBalancing()
    {
        try
        {
            for (int i = 0; i < blockMatrix.BlocksInRow; i++)
            {
                for (int j = 0; j < blockMatrix.BlocksInCol; j++)
                {
                    int targetNode = loadBalancer.GetOptimalNode(totalNodes);
                    var task = taskDistributionService.CreateBlockTask(i, j, blockMatrix);
                    await SendTaskToNode(targetNode, task);
                    await Task.Delay(NetworkConfiguration.Timeouts.TASK_DISTRIBUTION_DELAY_MS);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при распределении задач: {ex.Message}");
            throw;
        }
    }

    private async Task ProcessBlockResultChunk(NetworkMessages.BlockResultChunk chunk)
    {
        try
        {
            Console.WriteLine($"Получен чанк [{chunk.BlockRow}, {chunk.BlockCol}] #{chunk.ChunkId} от узла {chunk.NodeId}");

            // Отправляем подтверждение получения чанка
            var ack = new ChunkAck
            {
                BlockRow = chunk.BlockRow,
                BlockCol = chunk.BlockCol,
                ChunkId = chunk.ChunkId,
                NodeId = chunk.NodeId,
                IsReceived = true
            };

            await SendAcknowledgement(ack);
            Console.WriteLine($"Отправлено подтверждение для чанка [{chunk.BlockRow}, {chunk.BlockCol}] #{chunk.ChunkId}");

            if (blockResultAssembler.TryAddChunk(chunk))
            {
                var completeBlock = blockResultAssembler.AssembleBlock(chunk.BlockRow, chunk.BlockCol);
                await ProcessNodeResult(completeBlock);
                errorHandler.ResetRetryCount((chunk.BlockRow, chunk.BlockCol));
                loadBalancer.RegisterTaskCompletion(chunk.NodeId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке чанка блока [{chunk.BlockRow}, {chunk.BlockCol}]: {ex.Message}");
        }
    }

    private async Task ProcessNodeResult(NetworkMessages.BlockResult result)
    {
        try
        {
            var blockKey = (result.BlockRow, result.BlockCol);

            if (taskDistributionService.IsBlockPending(blockKey))
            {
                Console.WriteLine($"Получен результат для блока [{result.BlockRow}, {result.BlockCol}]");
                taskDistributionService.MarkBlockComplete(blockKey);
                solutionAssemblyService.AddProcessedBlock(result.BlockRow, result.BlockCol, result.BlockData.ToMatrix());

                if (solutionAssemblyService.IsProcessingComplete)
                {
                    Console.WriteLine("Все блоки обработаны, формируем итоговое решение");
                    taskDistributionService.SetProcessingComplete();
                    distributedSolveTimer.Stop();
                    await solutionAssemblyService.AssembleSolution(blockMatrix.Matrix, vector, distributedSolveTimer.ElapsedMilliseconds);
                    networkMetrics.PrintMetrics();

                    // Отправляем сигнал завершения всем узлам
                    for (int i = 0; i < totalNodes; i++)
                    {
                        var shutdownMessage = new { Type = "SHUTDOWN" };
                        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                            NetworkConfiguration.Ports.GetNodePort(i));

                        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(shutdownMessage));
                        await udpServer.SendAsync(bytes, bytes.Length, endpoint);
                    }
                }
                else
                {
                    Console.WriteLine($"Обработано блоков: {solutionAssemblyService.GetCompletedBlocksCount()} из {blockMatrix.BlocksInRow * blockMatrix.BlocksInCol}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке результата от узла: {ex.Message}");
        }
    }

    private async Task SendAcknowledgement(ChunkAck ack)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string jsonAck = JsonSerializer.Serialize(ack, jsonOptions);
            string data = $"ACK|{jsonAck}";  // Изменили формат
            byte[] bytes = Encoding.UTF8.GetBytes(data);

            await udpServer.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                NetworkConfiguration.Ports.GetNodePort(ack.NodeId)));

            networkMetrics.RecordSentData(bytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке подтверждения: {ex.Message}");
        }
    }

    private (double[,] matrix, double[] vector) ParseMatrixData(string[] lines)
    {
        var matrix = new double[matrixRows, matrixCols];
        var vector = new double[matrixRows];

        for (int i = 0; i < matrixRows; i++)
        {
            var parts = lines[i].Split('|');
            var values = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (int j = 0; j < matrixCols; j++)
            {
                matrix[i, j] = double.Parse(values[j], System.Globalization.CultureInfo.InvariantCulture);
            }
            vector[i] = double.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }

        return (matrix, vector);
    }

    public void Cleanup()
    {
        try
        {
            nodeManagementService.CleanupNodes();
            udpServer?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при очистке ресурсов: {ex.Message}");
        }
    }
}