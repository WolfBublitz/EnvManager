using System;
using R3;

internal class KeyboardService : IDisposable
{
    private readonly Subject<ConsoleCancelEventArgs> cancelSubject = new();

    public KeyboardService(Logger logger)
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            logger.Warning("Ctrl+C pressed.");

            eventArgs.Cancel = true;
            cancelSubject.OnNext(eventArgs);
        };
    }

    public Observable<ConsoleCancelEventArgs> CancelKeyPress => cancelSubject.AsObservable();

    public void Dispose()
    {
        cancelSubject.Dispose();
    }
}