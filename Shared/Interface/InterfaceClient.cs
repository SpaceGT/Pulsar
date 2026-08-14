using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Protocol;
using Pulsar.Protocol.Interface;
using Pulsar.Shared;

namespace Pulsar.Interface;

public sealed class InterfaceClient(string interfacePath) : IDisposable
{
    private const int ExitTimeout = 2000;
    private const int StartupTimeout = 10000;

    private readonly object processLock = new();

    private Process process;
    private IpcStream ipc;
    private Task<string> errorOutput;

    public void ShowSplash() => Send(InterfaceOperation.SplashShow);

    public void SetSplashTitle(string title) => Send(InterfaceOperation.SplashTitle, text: title);

    public void SetSplashText(string text) => Send(InterfaceOperation.SplashText, text: text);

    public void SetSplashProgress(float? progress) =>
        Send(InterfaceOperation.SplashProgress, progress: progress);

    public void CloseSplash() => Send(InterfaceOperation.SplashClose);

    public PromptResult ShowPrompt(
        string message,
        PromptButtons buttons = PromptButtons.Ok,
        PromptIcon icon = PromptIcon.None,
        string caption = "Pulsar"
    )
    {
        PromptRequest request = new()
        {
            Caption = caption,
            Message = message,
            Buttons = buttons,
            Icon = icon,
        };
        return Send(InterfaceOperation.PromptShow, prompt: request).PromptResult;
    }

    public string OpenFile(string title, string directory, FilePickerFilter[] filters)
    {
        FilePickerRequest request = new()
        {
            Title = title,
            Directory = directory,
            Filters = filters,
        };
        return Send(InterfaceOperation.FileOpen, filePicker: request).Text;
    }

    public string OpenFolder(string title)
    {
        FolderPickerRequest request = new() { Title = title };
        return Send(InterfaceOperation.FolderOpen, folderPicker: request).Text;
    }

    public string GetClipboard() => Send(InterfaceOperation.ClipboardGet).Text ?? string.Empty;

    public bool TakeEscapePressed() => Send(InterfaceOperation.EscapePressed).Value;

    public void Dispose()
    {
        lock (processLock)
            Stop();
    }

    private InterfaceResponse Send(
        InterfaceOperation operation,
        PromptRequest prompt = null,
        FilePickerRequest filePicker = null,
        FolderPickerRequest folderPicker = null,
        string text = null,
        float? progress = null
    )
    {
        // Dialog and input operations follow the splash operations.
        // NoPrompt skips them without disabling the splash.
        if (Flags.NoPrompt && operation >= InterfaceOperation.PromptShow)
            return new();

        lock (processLock)
        {
            try
            {
                EnsureStarted();
                return Exchange(
                    new InterfaceRequest
                    {
                        Operation = operation,
                        Prompt = prompt,
                        FilePicker = filePicker,
                        FolderPicker = folderPicker,
                        Text = text,
                        Progress = progress,
                    }
                );
            }
            catch
            {
                Stop();
                throw;
            }
        }
    }

    private void EnsureStarted()
    {
        if (process is not null)
        {
            if (!process.HasExited)
                return;

            Stop();
        }

        if (!File.Exists(interfacePath))
            throw new FileNotFoundException("Unable to find the Pulsar interface.", interfacePath);

        ProcessStartInfo startInfo = new()
        {
            FileName = interfacePath,
            WorkingDirectory = Path.GetDirectoryName(interfacePath),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Unable to start the Pulsar interface.");

            errorOutput = process.StandardError.ReadToEndAsync();
            ipc = new IpcStream(
                process.StandardOutput.BaseStream,
                process.StandardInput.BaseStream
            );

            Task<InterfaceResponse> hello = Task.Run(() =>
                Exchange(new InterfaceRequest { Operation = InterfaceOperation.Hello })
            );
            Task completed = Task.WhenAny(hello, Task.Delay(StartupTimeout))
                .GetAwaiter()
                .GetResult();
            if (completed != hello)
                throw new TimeoutException(
                    "The Pulsar interface process timed out during startup."
                );

            hello.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            string error = Stop();
            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"The interface process failed:\n{error}", e);

            throw;
        }
    }

    private InterfaceResponse Exchange(InterfaceRequest request)
    {
        IpcStream connection = ipc;
        connection.Write(request);
        InterfaceResponse response =
            connection.Read<InterfaceResponse>()
            ?? throw new InvalidDataException("The Pulsar interface returned no response.");
        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new InvalidOperationException(response.Error);

        return response;
    }

    private string Stop()
    {
        if (process is null)
            return null;

        Process child = process;
        Task<string> errors = errorOutput;
        process = null;
        ipc = null;
        errorOutput = null;

        try
        {
            child.StandardInput.Close();
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to close Pulsar interface input: " + e);
        }

        bool exited = false;
        try
        {
            exited = child.HasExited || child.WaitForExit(ExitTimeout);
            if (!exited)
            {
                child.Kill();
                exited = child.WaitForExit(ExitTimeout);
            }
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to stop Pulsar interface process: " + e);
        }

        string error = null;
        try
        {
            if (errors is not null && (errors.IsCompleted || (exited && errors.Wait(ExitTimeout))))
            {
                error = errors.GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(error))
                    LogFile.Error(error);
            }
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to read Pulsar interface error output: " + e);
        }

        child.Dispose();
        return error;
    }
}
