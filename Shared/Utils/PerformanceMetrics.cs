using System.Diagnostics;

namespace Shared.Utils;

public class PerformanceMetrics
{
    private readonly Stopwatch stopwatch;
    private int matrixSize;
    private int nodesCount;

    public PerformanceMetrics(int matrixSize, int nodesCount)
    {
        this.stopwatch = new Stopwatch();
        this.matrixSize = matrixSize;
        this.nodesCount = nodesCount;
    }

    public void Initialize(int matrixSize, int nodesCount)
    {
        this.matrixSize = matrixSize;
        this.nodesCount = nodesCount;
    }

    public void StartMeasurement()
    {
        stopwatch.Restart();
    }

    public void StopMeasurement()
    {
        stopwatch.Stop();
        PrintMetrics();
    }

    private void PrintMetrics()
    {
        Console.WriteLine("\nПоказатели производительности:");
        Console.WriteLine($"Размер матрицы: {matrixSize}x{matrixSize}");
        Console.WriteLine($"Количество узлов: {nodesCount}");
        Console.WriteLine($"Время выполнения: {stopwatch.ElapsedMilliseconds} мс");

        // Приблизительное количество операций с плавающей точкой
        double operations = (2.0 * matrixSize * matrixSize * matrixSize) / 3.0;
        double gflops = (operations / stopwatch.ElapsedMilliseconds) / 1_000_000.0;

        Console.WriteLine($"Производительность: {gflops:F2} GFLOPS");
    }
}