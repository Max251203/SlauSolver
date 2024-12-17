using Node.Services;

namespace Node.Startup;

public class NodeInitializer
{
    private readonly CommunicationService communicationService;
    private bool isRunning = true;

    public NodeInitializer(int port)
    {
        communicationService = new CommunicationService(port);
    }

    public async Task Start()
    {
        try
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Stop();
            };

            await communicationService.StartReceiving();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запуске узла: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        isRunning = false;
        communicationService.Close();
    }
} 