namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        ServerInitializer server = null;
        try
        {
            Console.WriteLine("=== Сервер для распределенного решения СЛАУ ===");

            server = new ServerInitializer(11000);
            Console.WriteLine("Сервер запущен на порту 11000");

            await server.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Критическая ошибка: {ex.Message}");
        }
        finally
        {
            server?.Cleanup();
        }
    }
}