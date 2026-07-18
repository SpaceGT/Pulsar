using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Pulsar.Interface.Protocol;

namespace Pulsar.Interface;

internal sealed class WindowManager(IClassicDesktopStyleApplicationLifetime desktop)
{
    private readonly SemaphoreSlim dialogLock = new(1, 1);
    private SplashWindow splash;
    private Window owner;
    private bool escapePressed;

    public Task<object> ShowSplash()
    {
        if (splash is null)
        {
            splash = new SplashWindow();
            WatchKeyboard(splash);
            splash.Closed += (_, _) => splash = null;
            splash.Show();
            splash.Activate();
        }

        return Completed();
    }

    public Task<object> SetSplashTitle(string title)
    {
        if (splash is not null)
            splash.Title = title;
        return Completed();
    }

    public Task<object> SetSplashText(string text)
    {
        splash?.SetText(text);
        return Completed();
    }

    public Task<object> SetSplashProgress(float? progress)
    {
        if (progress.HasValue)
            progress = Math.Max(0, Math.Min(1, progress.Value));
        splash?.SetProgress(progress);
        return Completed();
    }

    public Task<object> CloseSplash()
    {
        splash?.Close();
        splash = null;
        return Completed();
    }

    public async Task<object> ShowPrompt(PromptRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            PromptWindow prompt = new(request);
            return await prompt.ShowDialog<PromptResult>(GetOwner());
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<object> OpenFile(FilePickerRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            FilePickerOpenOptions options = new()
            {
                Title = request.Title,
                AllowMultiple = false,
                FileTypeFilter = ParseFilter(request.Filter),
            };

            if (Directory.Exists(request.Directory))
                options.SuggestedStartLocation = await GetOwner()
                    .StorageProvider.TryGetFolderFromPathAsync(request.Directory);

            IReadOnlyList<IStorageFile> files = await GetOwner()
                .StorageProvider.OpenFilePickerAsync(options);
            return files.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<object> OpenFolder()
    {
        await dialogLock.WaitAsync();
        try
        {
            IReadOnlyList<IStorageFolder> folders = await GetOwner()
                .StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions() { AllowMultiple = false }
                );
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<object> GetClipboard()
    {
        return await GetOwner().Clipboard.GetTextAsync() ?? string.Empty;
    }

    public async Task<bool> IsEscapePressed()
    {
        Window window = splash ?? GetOwner();
        window.Activate();
        await Task.Delay(50);
        return escapePressed;
    }

    public void Shutdown()
    {
        splash?.Close();
        owner?.Close();
        desktop.Shutdown();
    }

    private Window GetOwner()
    {
        if (splash is not null)
            return splash;
        if (owner is not null)
            return owner;

        owner = new Window()
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Position = new PixelPoint(-10000, -10000),
        };
        WatchKeyboard(owner);
        owner.Show();
        return owner;
    }

    private void WatchKeyboard(Window window)
    {
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                escapePressed = true;
        };
        window.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Escape)
                escapePressed = false;
        };
        window.Deactivated += (_, _) => escapePressed = false;
    }

    private static IReadOnlyList<FilePickerFileType> ParseFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        string[] parts = filter.Split('|');
        List<FilePickerFileType> types = [];
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            string[] patterns = parts[i + 1].Split(';');
            types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
        }
        return types;
    }

    private static Task<object> Completed() => Task.FromResult<object>(null);
}
