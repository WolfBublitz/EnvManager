using System;
using System.Threading;
using R3;

internal sealed class CancellationServices : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public CancellationServices(KeyboardService keyboardService)
    {
        keyboardService.CancelKeyPress.Subscribe(eventArgs =>
        {
            cancellationTokenSource.Cancel();
        });
    }

    public CancellationToken CancellationToken => cancellationTokenSource.Token;

    public void Dispose()
        => cancellationTokenSource.Dispose();
}