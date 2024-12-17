using Shared.Models;
using Shared.Utils;

namespace Node.Services;

public class BlockProcessingService
{
    private readonly int nodeId;
    private readonly NetworkMetrics networkMetrics;
    private readonly PerformanceMetrics performanceMetrics;

    public BlockProcessingService(int nodeId)
    {
        this.nodeId = nodeId;
        this.networkMetrics = new NetworkMetrics();
        this.performanceMetrics = new PerformanceMetrics(0, 0);
    }

    public double[,] ProcessBlock(NetworkMessages.BlockTask task)
    {
        try
        {
            Console.WriteLine($"Начало обработки блока [{task.BlockRow}, {task.BlockCol}]");
            Console.WriteLine($"Размеры блока: {task.Matrix.Rows}x{task.Matrix.Cols}");
            Console.WriteLine($"Размер вектора: {task.Vector.Length}");

            performanceMetrics.Initialize(task.Matrix.Rows, 1);
            performanceMetrics.StartMeasurement();

            var matrix = task.Matrix.ToMatrix();

            if (matrix.GetLength(0) != task.Vector.Length)
            {
                throw new ArgumentException(
                    $"Несоответствие размерностей: строк в матрице {matrix.GetLength(0)}, элементов в векторе {task.Vector.Length}");
            }

            var processedBlock = BlockGaussianElimination(matrix, task.Vector);

            performanceMetrics.StopMeasurement();
            networkMetrics.PrintMetrics();

            Console.WriteLine($"Блок [{task.BlockRow}, {task.BlockCol}] успешно обработан");

            return processedBlock;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке блока [{task.BlockRow}, {task.BlockCol}]: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private double[,] BlockGaussianElimination(double[,] block, double[] vectorPart)
    {
        try
        {
            int rows = block.GetLength(0);
            int cols = block.GetLength(1);

            Console.WriteLine($"Обработка блока размером {rows}x{cols}");

            if (vectorPart.Length != rows)
            {
                throw new ArgumentException(
                    $"Несоответствие размерностей: строк в блоке {rows}, элементов в векторе {vectorPart.Length}");
            }

            double[,] augmentedBlock = new double[rows, cols + 1];

            // Создание расширенной матрицы
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    augmentedBlock[i, j] = block[i, j];
                }
                augmentedBlock[i, cols] = vectorPart[i];
            }

            // Прямой ход метода Гаусса
            int minDim = Math.Min(rows, cols);
            for (int i = 0; i < minDim; i++)
            {
                // Выбор главного элемента
                double maxElement = Math.Abs(augmentedBlock[i, i]);
                int maxRow = i;
                for (int k = i + 1; k < rows; k++)
                {
                    if (Math.Abs(augmentedBlock[k, i]) > maxElement)
                    {
                        maxElement = Math.Abs(augmentedBlock[k, i]);
                        maxRow = k;
                    }
                }

                if (maxElement < 1e-10)
                {
                    Console.WriteLine($"Предупреждение: близкий к нулю элемент на диагонали в строке {i}");
                    continue;
                }

                // Перестановка строк
                if (maxRow != i)
                {
                    for (int j = 0; j <= cols; j++)
                    {
                        (augmentedBlock[i, j], augmentedBlock[maxRow, j]) =
                            (augmentedBlock[maxRow, j], augmentedBlock[i, j]);
                    }
                }

                // Исключение переменных
                for (int k = i + 1; k < rows; k++)
                {
                    if (Math.Abs(augmentedBlock[i, i]) > 1e-10)
                    {
                        double factor = augmentedBlock[k, i] / augmentedBlock[i, i];
                        for (int j = i; j <= cols; j++)
                        {
                            augmentedBlock[k, j] -= factor * augmentedBlock[i, j];
                        }
                    }
                }
            }

            return augmentedBlock;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в BlockGaussianElimination: {ex.Message}");
            Console.WriteLine($"Размеры блока: {block.GetLength(0)}x{block.GetLength(1)}, размер вектора: {vectorPart.Length}");
            throw;
        }
    }
} 