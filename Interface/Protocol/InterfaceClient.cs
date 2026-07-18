#nullable disable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Interface.Protocol;

public sealed class InterfaceClient : IDisposable
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<InterfaceResponse>> pending = new();
    private readonly object processLock = new();
    private readonly object writeLock = new();
    private readonly Action<string> log;

    private Process process;
    private StreamWriter input;
    private long nextId;

    public InterfaceClient(Action<string> log = null)
    {
        this.log = log;
    }

    public void ShowSplash() => Send(InterfaceMethods.SplashShow);

    public void SetSplashTitle(string title) => Send(InterfaceMethods.SplashTitle, text: title);

    public void SetSplashText(string text) => Send(InterfaceMethods.SplashText, text: text);

    public void SetSplashProgress(float? progress) =>
        Send(InterfaceMethods.SplashProgress, progress: progress);

    public void CloseSplash() => Send(InterfaceMethods.SplashClose);

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
        return Send(InterfaceMethods.PromptShow, prompt: request).PromptResult;
    }

    public string OpenFile(string title, string directory, string filter)
    {
        FilePickerRequest request = new() { Title = title, Directory = directory, Filter = filter };
        return Send(InterfaceMethods.FileOpen, filePicker: request).Text;
    }

    public string OpenFolder() => Send(InterfaceMethods.FolderOpen).Text;

    public string GetClipboard() => Send(InterfaceMethods.ClipboardGet).Text ?? string.Empty;

    public bool IsEscapePressed()
    {
        try
        {
            return Send(InterfaceMethods.EscapePressed).Value;
        }
        catch (Exception e)
        {
            log?.Invoke("Could not read keyboard state: " + e);
            return false;
        }
    }

    private InterfaceResponse Send(
        string method,
        PromptRequest prompt = null,
        FilePickerRequest filePicker = null,
        string text = null,
        float? progress = null
    )
    {
        EnsureStarted();
        InterfaceRequest request = new()
        {
            Method = method,
            Prompt = prompt,
            FilePicker = filePicker,
            Text = text,
            Progress = progress,
        };
        InterfaceResponse response = SendRequest(request).GetAwaiter().GetResult();
        if (!response.Ok)
            throw new InvalidOperationException(response.Error ?? "Pulsar interface request failed.");
        return response;
    }

    private void EnsureStarted()
    {
        lock (processLock)
        {
            if (process is not null && !process.HasExited)
                return;

            string root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string uiFolder = Path.Combine(root, "Libraries", "Interface");
            string uiPath = Path.Combine(uiFolder, "Pulsar.Interface.exe");

            ProcessStartInfo startInfo = new()
            {
                FileName = uiPath,
                Arguments = "--ipc-stdio",
                WorkingDirectory = uiFolder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process = Process.Start(startInfo);
            Process current = process;
            input = current.StandardInput;
            current.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    log?.Invoke("UI: " + e.Data);
            };
            current.BeginErrorReadLine();
            Task.Run(() => ReadResponses(current));

            Task<InterfaceResponse> hello = SendRequest(
                new InterfaceRequest() { Method = InterfaceMethods.Hello }
            );
            if (!hello.Wait(TimeSpan.FromSeconds(10)) || !hello.Result.Ok)
                throw new InvalidOperationException("Pulsar UI did not start correctly.");
        }
    }

    private Task<InterfaceResponse> SendRequest(InterfaceRequest request)
    {
        long id = Interlocked.Increment(ref nextId);
        TaskCompletionSource<InterfaceResponse> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        pending[id] = completion;

        request.Id = id;

        string json = ProtocolJson.Serialize(request);
        try
        {
            lock (writeLock)
            {
                input.WriteLine(json);
                input.Flush();
            }
        }
        catch (Exception e)
        {
            pending.TryRemove(id, out _);
            completion.TrySetException(e);
        }

        return completion.Task;
    }

    private void ReadResponses(Process current)
    {
        try
        {
            string line;
            while ((line = current.StandardOutput.ReadLine()) is not null)
            {
                InterfaceResponse response = ProtocolJson.Deserialize<InterfaceResponse>(line);
                if (response is not null && pending.TryRemove(response.Id, out var completion))
                    completion.TrySetResult(response);
            }
        }
        catch (Exception e)
        {
            log?.Invoke("Pulsar UI connection failed: " + e);
        }
        finally
        {
            IOException error = new("Pulsar UI closed the connection.");
            foreach (var item in pending)
                if (pending.TryRemove(item.Key, out var completion))
                    completion.TrySetException(error);
        }
    }

    public void Dispose()
    {
        lock (processLock)
        {
            if (process is null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    input.Close();
                    if (!process.WaitForExit(2000))
                        process.Kill();
                }
            }
            catch (Exception e)
            {
                log?.Invoke("Could not close Pulsar UI: " + e);
            }
            finally
            {
                process.Dispose();
                process = null;
                input = null;
            }
        }
    }
}
