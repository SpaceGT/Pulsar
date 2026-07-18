using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Pulsar.Interface.Protocol;

namespace Pulsar.Interface;

internal sealed class InterfaceServer(WindowManager windows)
{
    private const int MaxMessageLength = 1024 * 1024;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public void Start() => Task.Run(ReadRequests);

    private async Task ReadRequests()
    {
        try
        {
            string line;
            while ((line = await Console.In.ReadLineAsync()) is not null)
            {
                if (line.Length > MaxMessageLength)
                    throw new InvalidDataException("IPC message is too large.");

                InterfaceRequest request = ProtocolJson.Deserialize<InterfaceRequest>(line);
                if (request is null)
                    throw new InvalidDataException("Invalid IPC message.");

                _ = HandleRequest(request);
            }
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync(e.ToString());
        }
        finally
        {
            Dispatcher.UIThread.Post(windows.Shutdown);
        }
    }

    private async Task HandleRequest(InterfaceRequest request)
    {
        InterfaceResponse response = new() { Id = request.Id };
        try
        {
            if (request.Version != 1)
                throw new InvalidDataException("Unsupported IPC version.");

            await DispatchOnUIThread(request, response);

            response.Ok = true;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync(e.ToString());
            response.Error = e.Message;
        }

        await WriteResponse(response);
    }

    private Task DispatchOnUIThread(InterfaceRequest request, InterfaceResponse response)
    {
        TaskCompletionSource<object> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Dispatch(request, response);
                completion.SetResult(null);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });
        return completion.Task;
    }

    private async Task Dispatch(InterfaceRequest request, InterfaceResponse response)
    {
        switch (request.Method)
        {
            case InterfaceMethods.Hello:
                return;
            case InterfaceMethods.SplashShow:
                await windows.ShowSplash();
                return;
            case InterfaceMethods.SplashTitle:
                await windows.SetSplashTitle(request.Text);
                return;
            case InterfaceMethods.SplashText:
                await windows.SetSplashText(request.Text);
                return;
            case InterfaceMethods.SplashProgress:
                await windows.SetSplashProgress(request.Progress);
                return;
            case InterfaceMethods.SplashClose:
                await windows.CloseSplash();
                return;
            case InterfaceMethods.PromptShow:
                response.PromptResult = (PromptResult)await windows.ShowPrompt(request.Prompt);
                return;
            case InterfaceMethods.FileOpen:
                response.Text = (string)await windows.OpenFile(request.FilePicker);
                return;
            case InterfaceMethods.FolderOpen:
                response.Text = (string)await windows.OpenFolder();
                return;
            case InterfaceMethods.ClipboardGet:
                response.Text = (string)await windows.GetClipboard();
                return;
            case InterfaceMethods.EscapePressed:
                response.Value = await windows.IsEscapePressed();
                return;
            case InterfaceMethods.Shutdown:
                windows.Shutdown();
                return;
            default:
                throw new InvalidDataException($"Unknown IPC method '{request.Method}'.");
        }
    }

    private async Task WriteResponse(InterfaceResponse response)
    {
        string json = ProtocolJson.Serialize(response);
        await writeLock.WaitAsync();
        try
        {
            await Console.Out.WriteLineAsync(json);
            await Console.Out.FlushAsync();
        }
        finally
        {
            writeLock.Release();
        }
    }
}
