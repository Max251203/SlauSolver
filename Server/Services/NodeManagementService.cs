using Shared.Models;
using Shared.Network;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
namespace Server.Services;

public class NodeManagementService
{
    private readonly List<Process> nodeProcesses;

    public NodeManagementService()
    {
        nodeProcesses = [];
    }

    public async Task StartNodes(int nodesCount)
    {
        try
        {
            await Task.WhenAll(Enumerable.Range(0, nodesCount).Select(StartNode));
            await Task.Delay(200); // Даем время на инициализацию узлов
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запуске узлов: {ex.Message}");
            throw;
        }
    }

    private async Task StartNode(int nodeIndex)
    {
        try
        {
            string nodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node.dll");
            var nodeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"{nodePath} {nodeIndex}",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };

            nodeProcess.Start();
            nodeProcesses.Add(nodeProcess);
            Console.WriteLine($"Запущен узел {nodeIndex}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запуске узла {nodeIndex}: {ex.Message}");
            throw;
        }
    }

    public void CleanupNodes()
    {
        foreach (var process in nodeProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при завершении узла: {ex.Message}");
            }
        }
        nodeProcesses.Clear();
    }
} 