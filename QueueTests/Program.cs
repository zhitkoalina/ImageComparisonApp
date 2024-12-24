using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private const string ServerUrl = "http://localhost:8080/";
    private const int TotalRequests = 10000; // Количество запросов
    private const int ConcurrentClients = 100; // Количество одновременно работающих клиентов
    private const int ProgressUpdateInterval = 1000; // Интервал обновления прогресса

    private static double totalProcessingTime = 0.0;
    private static int successfulRequests = 0; // Количество успешно выполненных запросов

    private static readonly object lockObject = new();

    static async Task Main(string[] args)
    {
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Clear();

        Console.WriteLine("Запуск теста производительности...\n");

        var stopwatch = Stopwatch.StartNew();
        var tasks = Task.WhenAll(RunParallelRequestsAsync());
        await tasks;
        stopwatch.Stop();

        double totalTestTime = stopwatch.Elapsed.TotalSeconds;
        double arrivalRate = TotalRequests / totalTestTime;
        double successRate = (double)successfulRequests / TotalRequests;

        Console.WriteLine($"\n\nВсего запросов: {TotalRequests}");
        Console.WriteLine($"Успешных запросов: {successfulRequests}");
        Console.WriteLine($"Коэффициент загрузки прибытия: {arrivalRate:F2} запрос/сек");
        Console.WriteLine($"Коэффициент соотношения входящих к выходящим: {successRate:P2}");

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        Console.ReadKey();
    }

    private static Task[] RunParallelRequestsAsync()
    {
        var tasks = new Task[ConcurrentClients];
        for (int i = 0; i < ConcurrentClients; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var requestsPerClient = TotalRequests / ConcurrentClients;
                await SimulateClientAsync(requestsPerClient);
            });
        }
        return tasks;
    }

    private static async Task SimulateClientAsync(int requests)
    {
        using var httpClient = new HttpClient();
        for (int i = 0; i < requests; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await httpClient.GetAsync(ServerUrl);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    lock (lockObject)
                    {
                        successfulRequests++;
                        totalProcessingTime += stopwatch.Elapsed.TotalSeconds;
                    }

                    if (successfulRequests % ProgressUpdateInterval == 0)
                    {
                        PrintIntermediateProgress(successfulRequests);
                    }
                }
            }
            catch
            {
                // Игнорировать ошибки
            }
        }
    }

    private static void PrintIntermediateProgress(int completedRequests)
    {
        lock (lockObject)
        {
            Console.WriteLine($"Выполнено запросов: {completedRequests} | Успешных: {successfulRequests}");
        }
    }
}