using System.Diagnostics;

namespace Shared.Utils;

public class NetworkMetrics
{
    private long totalBytesSent;
    private long totalBytesReceived;
    private int totalPacketsSent;
    private int totalPacketsReceived;
    private readonly Stopwatch measurementTime;
    private readonly object lockObject = new object();

    public NetworkMetrics()
    {
        measurementTime = Stopwatch.StartNew();
    }

    public void RecordSentData(int bytes)
    {
        lock (lockObject)
        {
            totalBytesSent += bytes;
            totalPacketsSent++;
        }
    }

    public void RecordReceivedData(int bytes)
    {
        lock (lockObject)
        {
            totalBytesReceived += bytes;
            totalPacketsReceived++;
        }
    }

    public void PrintMetrics()
    {
        lock (lockObject)
        {
            Console.WriteLine("\nСетевая статистика:");
            Console.WriteLine($"Время измерения: {measurementTime.Elapsed.TotalSeconds:F2} сек");
            Console.WriteLine($"Отправлено пакетов: {totalPacketsSent}");
            Console.WriteLine($"Получено пакетов: {totalPacketsReceived}");
            Console.WriteLine($"Отправлено данных: {FormatBytes(totalBytesSent)}");
            Console.WriteLine($"Получено данных: {FormatBytes(totalBytesReceived)}");
            Console.WriteLine($"Средняя скорость отправки: {FormatBytesPerSecond(totalBytesSent)}");
            Console.WriteLine($"Средняя скорость получения: {FormatBytesPerSecond(totalBytesReceived)}");
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }

    private string FormatBytesPerSecond(long totalBytes)
    {
        double seconds = measurementTime.Elapsed.TotalSeconds;
        double bytesPerSecond = totalBytes / seconds;
        return $"{FormatBytes((long)bytesPerSecond)}/s";
    }
} 