using System.IO.Pipes;

namespace TodoDock.Services;

/// <summary>Второй запуск сообщает первому, что нужно открыть панель.</summary>
internal static class SingleInstance
{
    private const string PipeName = "TodoDock.Pipe";

    public static void SignalExisting()
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            c.Connect(500);
            c.WriteByte(1);
        }
        catch { /* первый экземпляр недоступен — ничего не делаем */ }
    }

    public static void Listen(Action onSignal)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await using var s = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await s.WaitForConnectionAsync();
                onSignal();
            }
        });
    }
}
