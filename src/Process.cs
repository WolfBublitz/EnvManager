using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using R3;

internal sealed class Process(string fileName, params string[] arguments) : IDisposable
{
    private readonly Subject<string> infoSubject = new();

    private readonly Subject<string> errorSubject = new();

    public Observable<string> InfoOutput => infoSubject.AsObservable();

    public Observable<string> ErrorOutput => errorSubject.AsObservable();

    public int ExpectedExitCode { get; set; } = 0;

    public List<string> ErrorMarkers { get; } = ["fatal"];

    public DirectoryInfo? WorkingDirectory { get; set; }

    public string CommandLine => $"{fileName} {string.Join(" ", arguments)}";

    public void Dispose()
    {
        infoSubject.Dispose();
        errorSubject.Dispose();
    }

    public async Task RunAsync()
    {
        ProcessStartInfo processStartInfo = new()
        {
            FileName = fileName,
            Arguments = arguments is not null ? string.Join(" ", arguments) : string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = WorkingDirectory?.FullName,
        };

        using System.Diagnostics.Process process = new()
        { 
            StartInfo = processStartInfo
        };

        void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                for (int i = 0; i < ErrorMarkers.Count; i++)
                {
                    if (e.Data.Contains(ErrorMarkers[i], StringComparison.OrdinalIgnoreCase))
                    {
                        errorSubject.OnNext(e.Data.EscapeMarkup());
                    }
                    else
                    {
                        infoSubject.OnNext(e.Data.EscapeMarkup());
                    }
                }
            }
        };

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                for (int i = 0; i < ErrorMarkers.Count; i++)
                {
                    if (e.Data.Contains(ErrorMarkers[i], StringComparison.OrdinalIgnoreCase))
                    {
                        errorSubject.OnNext(e.Data.EscapeMarkup());
                    }
                    else
                    {
                        infoSubject.OnNext(e.Data.EscapeMarkup());
                    }
                }
            }
        };

        process.OutputDataReceived += OnOutputDataReceived;
        process.ErrorDataReceived += OnErrorDataReceived;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync().ConfigureAwait(false);

        process.OutputDataReceived -= OnOutputDataReceived;
        process.ErrorDataReceived -= OnErrorDataReceived;

        if (process.ExitCode != ExpectedExitCode)
        {
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}, expected {ExpectedExitCode}.")
            {
                ExitCode = process.ExitCode
            };
        }
    }
}