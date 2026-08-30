using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Pulsar.Protocol;
using Pulsar.Protocol.Interface;

namespace Pulsar.Interface;

internal sealed class InterfaceServer(WindowManager windows)
{
    private readonly IpcStream ipc = new(Console.OpenStandardInput(), Console.OpenStandardOutput());

    public void Start() => Task.Run(ReadRequests);

    private void ReadRequests()
    {
        try
        {
            while (ipc.TryRead(out InterfaceRequest request))
            {
                if (request is null)
                    throw new InvalidDataException("Invalid IPC message.");

                _ = HandleRequest(request);
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine(e);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
        finally
        {
            Dispatcher.UIThread.Post(windows.Shutdown);
        }
    }

    private async Task HandleRequest(InterfaceRequest request)
    {
        InterfaceResponse response = new();
        try
        {
            await DispatchOnUIThread(request, response);
        }
        catch (Exception e)
        {
            response.Error = e.ToString();
        }

        try
        {
            ipc.Write(response);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
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
        switch (request.Operation)
        {
            case InterfaceOperation.Hello:
                return;
            case InterfaceOperation.SplashShow:
                windows.ShowSplash();
                return;
            case InterfaceOperation.SplashTitle:
                windows.SetSplashTitle(request.Text);
                return;
            case InterfaceOperation.SplashText:
                windows.SetSplashText(request.Text);
                return;
            case InterfaceOperation.SplashProgress:
                windows.SetSplashProgress(request.Progress);
                return;
            case InterfaceOperation.SplashClose:
                windows.CloseSplash();
                return;
            case InterfaceOperation.PromptShow:
                response.PromptResult = await windows.ShowPrompt(request.Prompt);
                return;
            case InterfaceOperation.FileOpen:
                response.Text = await windows.OpenFile(request.FilePicker);
                return;
            case InterfaceOperation.FolderOpen:
                response.Text = await windows.OpenFolder(request.FolderPicker);
                return;
            case InterfaceOperation.ClipboardGet:
                response.Text = await windows.GetClipboard();
                return;
            case InterfaceOperation.EscapePressed:
                response.Value = windows.TakeEscapePressed();
                return;
            default:
                throw new InvalidDataException(
                    $"Unknown interface operation '{request.Operation}'."
                );
        }
    }
}
