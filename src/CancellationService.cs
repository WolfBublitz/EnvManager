using System;
using System.Threading;
using R3;

internal sealed class CancellationService : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public CancellationService(KeyboardService keyboardService)
    {
        keyboardService.CancelKeyPress.Subscribe(eventArgs =>
        {
            cancellationTokenSource.Cancel();
        });
    }

    public CancellationToken CancellationToken => cancellationTokenSource.Token;

    public void Dispose()
        => cancellationTokenSource.Dispose();

    public void Shutdown(string message)
        => cancellationTokenSource.Cancel();
}