using Shared.Network;
using Shared.Solvers;
using static Shared.Models.NetworkMessages;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Client.Windows;

namespace Client.Services;

public class ResultReceivingService
{
    private readonly UdpClient _udpClient;
    private readonly double[,] _originalMatrix;
    private readonly double[] _originalVector;
    private readonly GaussSolver _sequentialSolver;

    public ResultReceivingService(UdpClient udpClient, double[,] matrix, double[] vector)
    {
        _udpClient = udpClient;
        _originalMatrix = matrix;
        _originalVector = vector;
        _sequentialSolver = new GaussSolver();
    }

    public async Task<SolutionResult> ReceiveResults()
    {
        try
        {
            Console.WriteLine("Ожидание результатов...");
            var result = await UdpHelper.ReceiveAsync<SolutionResult>(_udpClient);

            var sequentialTime = MeasureSequentialSolution();
            PrintResults(result, sequentialTime);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении результатов: {ex.Message}");
            throw;
        }
    }

    private long MeasureSequentialSolution()
    {
        Console.WriteLine("Выполняем последовательное решение для сравнения...");
        var watch = Stopwatch.StartNew();
        var sequentialSolution = _sequentialSolver.Solve(_originalMatrix, _originalVector);
        watch.Stop();
        return watch.ElapsedMilliseconds;
    }

    private void PrintResults(SolutionResult result, long sequentialTime)
    {
        var output = new StringBuilder();
        output.AppendLine("\nРезультаты вычислений:");
        output.AppendLine($"Размер матрицы: {result.MatrixSize}x{result.MatrixSize}");
        output.AppendLine($"Количество узлов: {result.NodesCount}");
        output.AppendLine($"Время распределённого решения: {result.DistributedTime} мс");
        output.AppendLine($"Время последовательного решения: {sequentialTime} мс");
        output.AppendLine($"Ускорение: {(double)sequentialTime / result.DistributedTime:F2}x");
        output.AppendLine($"Максимальная невязка: {result.MaxResidual:E6}");

        output.AppendLine("\nПервые 10 элементов решения:");
        for (int i = 0; i < Math.Min(10, result.Solution.Length); i++)
        {
            output.AppendLine($"x[{i}] = {result.Solution[i]:F6}");
        }

        result.SequentialTime = sequentialTime;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.UpdateResults(output.ToString());
        });
    }
}