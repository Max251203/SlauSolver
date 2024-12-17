using Node.Startup;
using Shared.Network;

class Program
{
    static void Main(string[] args)
    {
        NodeInitializer node = null;
        try
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Необходимо указать номер узла в аргументах командной строки.");
                return;
            }

            if (!int.TryParse(args[0], out int nodeId))
            {
                Console.WriteLine("Неверный номер узла. Пожалуйста, введите целое число.");
                return;
            }

            int port = NetworkConfiguration.Ports.GetNodePort(nodeId);
            Console.WriteLine($"=== Узел {nodeId} для распределенного решения СЛАУ ===");

            node = new NodeInitializer(port);
            Console.WriteLine($"Узел запущен на порту {port}");

            // Запускаем асинхронную работу узла
            Task.Run(async () => await node.Start()).Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Критическая ошибка: {ex.Message}");
        }
        finally
        {
            node?.Stop();
        }
    }
}