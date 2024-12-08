using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

public class CustomThreadPool
{
    private readonly Thread[] _threads;
    private readonly BlockingCollection<Action> _taskQueue = new();

    public CustomThreadPool(int threadCount)
    {
        if (threadCount <= 0)
            throw new ArgumentException("Количество потоков должно быть больше 0.");

        _threads = new Thread[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            _threads[i] = new Thread(Worker)
            {
                IsBackground = true
            };
            _threads[i].Start();
        }
    }

    public void EnqueueTask(Action task)
    {
        _taskQueue.Add(task);
    }

    public void Shutdown()
    {
        _taskQueue.CompleteAdding();
        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }

    private void Worker()
    {
        foreach (var task in _taskQueue.GetConsumingEnumerable())
        {
            try
            {
                task();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в задаче: {ex.Message}");
            }
        }
    }
}
