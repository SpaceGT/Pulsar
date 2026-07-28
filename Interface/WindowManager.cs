using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pulsar.Protocol.Interface;

namespace Pulsar.Interface;

internal sealed class WindowManager(IClassicDesktopStyleApplicationLifetime desktop)
{
    private readonly SemaphoreSlim dialogLock = new(1, 1);
    private SplashWindow splash;
    private Window owner;

    public void ShowSplash()
    {
        if (splash is not null)
            return;

        splash = new SplashWindow();
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

    public async Task<PromptResult> ShowPrompt(PromptRequest request)
    {
        await dialogLock.WaitAsync();
        try
        {
            PromptWindow prompt = new(request);
            return await prompt.ShowDialog<PromptResult>(GetHiddenOwner());
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
            FilePickerOpenOptions options = new()
            {
                Title = request.Title,
                AllowMultiple = false,
                FileTypeFilter = request.Filters
                    ?.Select(filter =>
                        new FilePickerFileType(filter.Name) { Patterns = filter.Patterns }
                    )
                    .ToArray(),
            };

            if (Directory.Exists(request.Directory))
                options.SuggestedStartLocation = await GetInteractiveOwner()
                    .StorageProvider.TryGetFolderFromPathAsync(request.Directory);

            IReadOnlyList<IStorageFile> files = await GetInteractiveOwner()
                .StorageProvider.OpenFilePickerAsync(options);
            return files.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<string> OpenFolder()
    {
        await dialogLock.WaitAsync();
        try
        {
            IReadOnlyList<IStorageFolder> folders = await GetInteractiveOwner()
                .StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { AllowMultiple = false }
                );
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            dialogLock.Release();
        }
    }

    public async Task<string> GetClipboard()
    {
        return await GetInteractiveOwner().Clipboard.GetTextAsync() ?? string.Empty;
    }

    public void Shutdown()
    {
        foreach (Window window in desktop.Windows.ToArray())
            window.Close();
        desktop.Shutdown();
    }

    private Window GetInteractiveOwner()
    {
        if (splash is not null)
            return splash;
        return GetHiddenOwner();
    }

    private Window GetHiddenOwner()
    {
        if (owner is not null)
            return owner;

        owner = new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        owner.Show();
        return owner;
    }
}
