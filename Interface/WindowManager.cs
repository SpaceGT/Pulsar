using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Pulsar.Protocol.Interface;

namespace Pulsar.Interface;

internal sealed class WindowManager(IClassicDesktopStyleApplicationLifetime desktop)
{
    private readonly SemaphoreSlim dialogLock = new(1, 1);
    private bool escapePressed;
    private SplashWindow splash;

    public void ShowSplash()
    {
        if (splash is not null)
            return;

        splash = new SplashWindow();
        splash.KeyDown += (_, e) => escapePressed |= e.Key == Key.Escape;
        splash.Closed += (_, _) => splash = null;
        splash.Show();
        splash.Activate();
    }

    public void SetSplashTitle(string title)
    {
        if (splash is not null)
            splash.Title = title;
    }

    public void SetSplashText(string text) => splash?.SetText(text);

    public void SetSplashProgress(float? progress)
    {
        if (progress.HasValue)
            progress = Math.Max(0, Math.Min(1, progress.Value));
        splash?.SetProgress(progress);
    }

    public void CloseSplash()
    {
        splash?.Close();
        splash = null;
    }

    public bool TakeEscapePressed()
    {
        bool pressed = escapePressed;
        escapePressed = false;
        return pressed;
    }

    public async Task<PromptResult> ShowPrompt(PromptRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            PromptWindow prompt = new(request);
            prompt.Show();
            prompt.Activate();
            return await prompt.Completion;
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<string> OpenFile(FilePickerRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            using FallbackOwner owner = new();
            FilePickerOpenOptions options = new()
            {
                Title = request.Title,
                AllowMultiple = false,
                FileTypeFilter = request
                    .Filters?.Select(filter => new FilePickerFileType(filter.Name)
                    {
                        Patterns = filter.Patterns,
                    })
                    .ToArray(),
            };

            if (Directory.Exists(request.Directory))
                options.SuggestedStartLocation =
                    await owner.StorageProvider.TryGetFolderFromPathAsync(request.Directory);

            var files = await owner.StorageProvider.OpenFilePickerAsync(options);
            return files.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<string> OpenFolder(FolderPickerRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            using FallbackOwner owner = new();
            FolderPickerOpenOptions options = new()
            {
                Title = request.Title,
                AllowMultiple = false,
            };
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(options);
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<string> GetClipboard()
    {
        using FallbackOwner owner = new(dialog: false);
        return await owner.Clipboard.GetTextAsync() ?? string.Empty;
    }

    public void Shutdown()
    {
        foreach (Window window in desktop.Windows.ToArray())
            window.Close();
        desktop.Shutdown();
    }

    private sealed class FallbackOwner : Window, IDisposable
    {
        public FallbackOwner(bool dialog = true)
        {
            Title = "Pulsar";
            Width = 1;
            Height = 1;
            Opacity = 0;
            ShowInTaskbar = dialog;
            ShowActivated = dialog;
            SystemDecorations = SystemDecorations.None;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Show();
        }

        public void Dispose() => Close();
    }
}
