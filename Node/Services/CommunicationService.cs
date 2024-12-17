using Shared.Models;
using Shared.Network;
using Shared.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Node.Services;

public class CommunicationService
{
    private readonly UdpClient udpClient;
    private readonly IPEndPoint localEP;
    private readonly int nodeId;
    private readonly BlockProcessingService blockProcessor;
    private readonly NetworkMetrics networkMetrics;
    private readonly Dictionary<string, DateTime> sentChunks;
    private readonly TransmissionRetryPolicy retryPolicy;
    private readonly object chunkLock = new object();
    private bool isRunning = true;
    private readonly Dictionary<string, NetworkMessages.BlockResultChunk> chunkCache =
    new Dictionary<string, NetworkMessages.BlockResultChunk>();

    public CommunicationService(int port)
    {
        nodeId = port - NetworkConfiguration.Ports.BASE_NODE_PORT;
        udpClient = new UdpClient(port);
        localEP = new IPEndPoint(IPAddress.Any, port);
        blockProcessor = new BlockProcessingService(nodeId);
        networkMetrics = new NetworkMetrics();
        sentChunks = new Dictionary<string, DateTime>();
        retryPolicy = new TransmissionRetryPolicy();

        StartHeartbeat();
        StartUnacknowledgedChunksMonitor();
    }

    private async void StartHeartbeat()
    {
        while (isRunning)
        {
            try
            {
                string heartbeat = $"HEARTBEAT|{nodeId}";
                byte[] bytes = Encoding.UTF8.GetBytes(heartbeat);
                await udpClient.SendAsync(bytes, bytes.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"), NetworkConfiguration.Ports.SERVER_PORT));
                networkMetrics.RecordSentData(bytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки heartbeat: {ex.Message}");
            }
            await Task.Delay(NetworkConfiguration.Timeouts.HEARTBEAT_INTERVAL_MS);
        }
    }

    private async void StartUnacknowledgedChunksMonitor()
    {
        while (isRunning)
        {
            try
            {
                List<string> chunksToResend = new List<string>();
                lock (chunkLock)
                {
                    var now = DateTime.Now;
                    foreach (var chunk in sentChunks)
                    {
                        if ((now - chunk.Value).TotalMilliseconds > NetworkConfiguration.Timeouts.CHUNK_ACKNOWLEDGEMENT_TIMEOUT_MS)
                        {
                            chunksToResend.Add(chunk.Key);
                        }
                    }
                }

                foreach (var chunkId in chunksToResend)
                {
                    await ResendChunk(chunkId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в мониторинге неподтвержденных чанков: {ex.Message}");
            }
            await Task.Delay(NetworkConfiguration.Timeouts.UNACKNOWLEDGED_CHECK_INTERVAL_MS);
        }
    }

    private async Task ResendChunk(string chunkId)
    {
        try
        {
            Console.WriteLine($"Повторная отправка чанка: {chunkId}");

            // Парсим ID чанка для получения информации о блоке
            var parts = chunkId.Split('_');
            if (parts.Length != 3)
            {
                throw new ArgumentException($"Неверный формат ID чанка: {chunkId}");
            }

            int blockRow = int.Parse(parts[0]);
            int blockCol = int.Parse(parts[1]);
            int chunkIndex = int.Parse(parts[2]);

            // Получаем сохраненный чанк из кэша или пересоздаем его
            NetworkMessages.BlockResultChunk chunk;
            if (chunkCache.TryGetValue(chunkId, out chunk))
            {
                // Если чанк найден в кэше, используем его
                await retryPolicy.ExecuteWithRetryAsync(async () =>
                {
                    await UdpHelper.SendAsync(udpClient, chunk,
                        new IPEndPoint(IPAddress.Parse("127.0.0.1"), NetworkConfiguration.Ports.SERVER_PORT));
                    networkMetrics.RecordSentData(JsonSerializer.Serialize(chunk).Length);
                });

                // Обновляем время отправки
                lock (chunkLock)
                {
                    sentChunks[chunkId] = DateTime.Now;
                }

                Console.WriteLine($"Чанк {chunkId} успешно переотправлен из кэша");
            }
            else
            {
                // Если чанк не найден в кэше, логируем ошибку
                Console.WriteLine($"Чанк {chunkId} не найден в кэше для повторной отправки");

                // Удаляем из отслеживания, так как не можем переотправить
                lock (chunkLock)
                {
                    sentChunks.Remove(chunkId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при повторной отправке чанка {chunkId}: {ex.Message}");

            // В случае критической ошибки удаляем чанк из отслеживания
            lock (chunkLock)
            {
                sentChunks.Remove(chunkId);
            }
        }
    }
    public async Task StartReceiving()
    {
        Console.WriteLine($"Узел {nodeId} запущен на порту {localEP.Port}...");

        while (isRunning)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                networkMetrics.RecordReceivedData(result.Buffer.Length);
                string data = Encoding.UTF8.GetString(result.Buffer);

                if (data.StartsWith("ACK|"))
                {
                    ProcessAcknowledgement(data);
                }
                else
                {
                    try
                    {
                        // Проверяем, не является ли сообщение командой завершения
                        if (data.Contains("\"Type\":\"SHUTDOWN\""))
                        {
                            Console.WriteLine("Получен сигнал завершения работы");
                            isRunning = false;
                            Close();
                            Environment.Exit(0); // Явное завершение процесса
                            return;
                        }

                        var blockTask = JsonSerializer.Deserialize<NetworkMessages.BlockTask>(data);
                        if (blockTask?.Type == "TASK")
                        {
                            Console.WriteLine($"Узел {nodeId} получил задачу для блока [{blockTask.BlockRow}, {blockTask.BlockCol}]");
                            await ProcessAndSendResult(blockTask);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Ошибка десериализации задачи: {ex.Message}");
                        Console.WriteLine($"Полученные данные: {data}");
                    }
                }
            }
            catch (SocketException ex)
            {
                if (!isRunning) break;
                Console.WriteLine($"Ошибка сети: {ex.Message}");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                if (!isRunning) break;
                Console.WriteLine($"Ошибка при получении задачи: {ex.Message}");
            }
        }
    }

    public void Close()
    {
        isRunning = false;
        try
        {
            udpClient?.Close();
            udpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при закрытии соединения: {ex.Message}");
        }
    }

    private async Task ProcessAndSendResult(NetworkMessages.BlockTask task)
    {
        try
        {
            Console.WriteLine($"Начало обработки блока [{task.BlockRow}, {task.BlockCol}]");
            var processedBlock = blockProcessor.ProcessBlock(task);
            var blockResult = new NetworkMessages.BlockResult
            {
                Type = "RESULT",
                NodeId = nodeId,
                BlockRow = task.BlockRow,
                BlockCol = task.BlockCol,
                BlockData = MatrixBlock.FromMatrix(processedBlock)
            };

            await SendResultInChunks(blockResult);
            Console.WriteLine($"Результат для блока [{task.BlockRow}, {task.BlockCol}] отправлен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке и отправке результата: {ex.Message}");
        }
    }

    private void ProcessAcknowledgement(string data)
    {
        try
        {
            var parts = data.Split('|', 2);
            if (parts.Length != 2)
            {
                throw new FormatException("Неверный формат подтверждения");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var ack = JsonSerializer.Deserialize<ChunkAck>(parts[1], jsonOptions);
            if (ack == null)
            {
                throw new InvalidOperationException("Не удалось десериализовать подтверждение");
            }

            string chunkId = $"{ack.BlockRow}_{ack.BlockCol}_{ack.ChunkId}";
            Console.WriteLine($"Получено подтверждение для чанка: {chunkId}");

            lock (chunkLock)
            {
                sentChunks.Remove(chunkId);
                chunkCache.Remove(chunkId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке подтверждения: {ex.Message}");
        }
    }

    private async Task SendResultInChunks(NetworkMessages.BlockResult result)
    {
        try
        {
            var chunks = BlockTransmissionService.PrepareBlockResultChunks(result);
            Console.WriteLine($"Подготовлено {chunks.Count} чанков для отправки");

            foreach (var chunk in chunks)
            {
                bool sent = false;
                int retryCount = 0;

                while (!sent && retryCount < NetworkConfiguration.Retry.MAX_RETRIES)
                {
                    try
                    {
                        await retryPolicy.ExecuteWithRetryAsync(async () =>
                        {
                            var jsonOptions = new JsonSerializerOptions
                            {
                                WriteIndented = false
                            };

                            string jsonData = JsonSerializer.Serialize(chunk, jsonOptions);
                            byte[] bytes = Encoding.UTF8.GetBytes(jsonData);

                            if (bytes.Length > NetworkConfiguration.Sizes.MAX_UDP_PACKET_SIZE)
                            {
                                throw new InvalidOperationException(
                                    $"Размер чанка превышает допустимый: {bytes.Length} байт");
                            }

                            await udpClient.SendAsync(bytes, bytes.Length,
                                new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                                    NetworkConfiguration.Ports.SERVER_PORT));

                            networkMetrics.RecordSentData(bytes.Length);
                            sent = true;
                        });

                        string chunkId = $"{chunk.BlockRow}_{chunk.BlockCol}_{chunk.ChunkId}";
                        lock (chunkLock)
                        {
                            sentChunks[chunkId] = DateTime.Now;
                            chunkCache[chunkId] = chunk;
                        }

                        Console.WriteLine($"Отправлен чанк {chunk.ChunkId + 1} из {chunk.TotalChunks}");
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        Console.WriteLine($"Попытка {retryCount}: Ошибка при отправке чанка: {ex.Message}");
                        await Task.Delay(NetworkConfiguration.Timeouts.RETRY_BASE_DELAY_MS * retryCount);
                    }
                }

                if (!sent)
                {
                    throw new Exception($"Не удалось отправить чанк после {NetworkConfiguration.Retry.MAX_RETRIES} попыток");
                }

                await Task.Delay(NetworkConfiguration.Timeouts.CHUNK_TRANSMISSION_DELAY_MS);
            }

            Console.WriteLine($"Узел {nodeId} отправил результат обработки блока [{result.BlockRow}, {result.BlockCol}] ({chunks.Count} чанков)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке результата: {ex.Message}");
            throw;
        }
    }
}