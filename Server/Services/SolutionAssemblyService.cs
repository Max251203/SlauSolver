using Shared.Models;
using Shared.Network;
using System.Net.Sockets;
using System.Net;

namespace Server.Services;
public class SolutionAssemblyService
{
    private readonly Dictionary<(int row, int col), double[,]> processedBlocks;
    private readonly HashSet<(int row, int col)> completedBlocks;
    private readonly UdpClient udpServer;
    private IPEndPoint clientEndPoint;
    private int totalBlocksCount;
    private readonly object processLock = new object();
    private int totalNodes;
    private BlockMatrix blockMatrix;

    public SolutionAssemblyService(UdpClient udpServer)
    {
        this.udpServer = udpServer;
        processedBlocks = new Dictionary<(int row, int col), double[,]>();
        completedBlocks = new HashSet<(int row, int col)>();
    }

    public void Initialize(int totalBlocks, IPEndPoint clientEP, int nodesCount, BlockMatrix matrix = null)
    {
        lock (processLock)
        {
            totalBlocksCount = totalBlocks;
            clientEndPoint = clientEP;
            totalNodes = nodesCount;
            blockMatrix = matrix;
            processedBlocks.Clear();
            completedBlocks.Clear();
        }
    }

    public void AddProcessedBlock(int blockRow, int blockCol, double[,] blockData)
    {
        lock (processLock)
        {
            var blockKey = (blockRow, blockCol);
            processedBlocks[blockKey] = blockData;
            completedBlocks.Add(blockKey);
        }
    }

    public bool IsProcessingComplete
    {
        get
        {
            lock (processLock)
            {
                int totalBlocks = blockMatrix?.BlocksInRow * blockMatrix?.BlocksInCol ?? totalBlocksCount;
                bool isComplete = completedBlocks.Count == totalBlocks;
                if (isComplete)
                {
                    Console.WriteLine($"Обработка завершена: получены все {totalBlocks} блоков");
                }
                return isComplete;
            }
        }
    }

    public int GetCompletedBlocksCount()
    {
        lock (processLock)
        {
            return completedBlocks.Count;
        }
    }

    public async Task AssembleSolution(double[,] originalMatrix, double[] originalVector, long processingTime)
    {
        try
        {
            Console.WriteLine("Проверка наличия всех блоков...");

            // Проверяем все ли блоки на месте
            int blocksInRow = blockMatrix?.BlocksInRow ?? (int)Math.Sqrt(totalBlocksCount);
            int blocksInCol = blockMatrix?.BlocksInCol ?? blocksInRow;

            for (int i = 0; i < blocksInRow; i++)
            {
                for (int j = 0; j < blocksInCol; j++)
                {
                    if (!processedBlocks.ContainsKey((i, j)))
                    {
                        throw new Exception($"Отсутствует блок [{i}, {j}]");
                    }
                }
            }

            int matrixSize = originalMatrix.GetLength(0);
            double[] solution = new double[matrixSize];
            Console.WriteLine($"Сборка решения размерности {matrixSize}...");

            // Собираем решение из блоков
            foreach (var ((row, col), blockData) in processedBlocks)
            {
                int startRow = row * (matrixSize / blocksInRow);
                int rows = blockData.GetLength(0);

                for (int localRow = 0; localRow < rows; localRow++)
                {
                    int globalRow = startRow + localRow;
                    if (globalRow < solution.Length)
                    {
                        solution[globalRow] = blockData[localRow, blockData.GetLength(1) - 1];
                    }
                }
            }

            double maxResidual = CalculateResidual(originalMatrix, solution, originalVector);
            Console.WriteLine($"Решение собрано. Невязка: {maxResidual:E6}");

            await SendSolutionToClient(solution, maxResidual, processingTime, matrixSize);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сборке решения: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private double CalculateResidual(double[,] matrix, double[] solution, double[] vector)
    {
        double[] residual = new double[solution.Length];

        for (int i = 0; i < solution.Length; i++)
        {
            double sum = 0;
            for (int j = 0; j < solution.Length; j++)
            {
                sum += matrix[i, j] * solution[j];
            }
            residual[i] = Math.Abs(sum - vector[i]);
        }

        return residual.Max();
    }

    private async Task SendSolutionToClient(double[] solution, double maxResidual, long processingTime, int matrixSize)
    {
        try
        {
            var result = new NetworkMessages.SolutionResult
            {
                Solution = solution,
                MaxResidual = maxResidual,
                DistributedTime = processingTime,
                MatrixSize = matrixSize,
                NodesCount = totalNodes
            };

            await UdpHelper.SendAsync(udpServer, result, clientEndPoint);
            Console.WriteLine("Результаты отправлены клиенту");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке результатов клиенту: {ex.Message}");
            throw;
        }
    }
}