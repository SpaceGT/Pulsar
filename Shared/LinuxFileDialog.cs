using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Pulsar.Shared;

// xdg-desktop-portal FileChooser interface.
// Spec: https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.FileChooser.html
[DBusInterface("org.freedesktop.portal.FileChooser")]
public interface IFileChooser : IDBusObject
{
    Task<ObjectPath> OpenFileAsync(
        string parentWindow,
        string title,
        IDictionary<string, object> options
    );
}

[DBusInterface("org.freedesktop.portal.Request")]
public interface IRequest : IDBusObject
{
    Task<IDisposable> WatchResponseAsync(
        Action<(uint response, IDictionary<string, object> results)> handler
    );
}

public static class LinuxFileDialog
{
    private static Connection connection;
    private static string localName;
    private static readonly object connectionLock = new();

    private static Connection GetConnection(out string uniqueName)
    {
        // Tmds.DBus connections are not thread-safe to construct concurrently.
        lock (connectionLock)
        {
            if (connection is not null)
            {
                uniqueName = localName;
                return connection;
            }

            Connection conn = new(Address.Session);
            ConnectionInfo info = conn.ConnectAsync().GetAwaiter().GetResult();
            connection = conn;
            localName = info.LocalName;
            uniqueName = localName;
            return connection;
        }
    }

    public static async Task<string> OpenFileAsync(
        string title,
        string currentFolder,
        string winFormsFilter
    )
    {
        IDictionary<string, object> options = new Dictionary<string, object>();
        AddCurrentFolder(options, currentFolder);
        AddFilters(options, winFormsFilter);
        return await InvokeAsync(title, options);
    }

    public static async Task<string> SelectFolderAsync(string title, string currentFolder)
    {
        IDictionary<string, object> options = new Dictionary<string, object> { ["directory"] = true };
        AddCurrentFolder(options, currentFolder);
        return await InvokeAsync(title, options);
    }

    private static async Task<string> InvokeAsync(string title, IDictionary<string, object> options)
    {
        Connection conn = GetConnection(out string uniqueName);

        // Predict the Request object path so we can subscribe to its Response
        // signal before issuing the call (avoids a race with fast cancellation).
        // Spec: /org/freedesktop/portal/desktop/request/SENDER/TOKEN
        // where SENDER is the unique bus name with the leading ':' stripped and
        // dots replaced by underscores.
        string token = "pulsar_" + Guid.NewGuid().ToString("N");
        string sender = uniqueName.TrimStart(':').Replace('.', '_');
        ObjectPath requestPath = new(
            $"/org/freedesktop/portal/desktop/request/{sender}/{token}"
        );

        options["handle_token"] = token;

        IRequest request = conn.CreateProxy<IRequest>(
            "org.freedesktop.portal.Desktop",
            requestPath
        );

        TaskCompletionSource<(uint response, IDictionary<string, object> results)> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        using IDisposable watcher = await request.WatchResponseAsync(args => tcs.TrySetResult(args));

        IFileChooser fileChooser = conn.CreateProxy<IFileChooser>(
            "org.freedesktop.portal.Desktop",
            new ObjectPath("/org/freedesktop/portal/desktop")
        );

        // parent_window left empty: Space Engineers is OpenGL/SDL and we have
        // no portable way to obtain an X11/Wayland handle string for it.
        await fileChooser.OpenFileAsync("", title, options);

        (uint response, IDictionary<string, object> results) = await tcs.Task;

        // 0 = success, 1 = user cancelled, 2 = other error.
        if (response != 0)
            return null;

        if (!results.TryGetValue("uris", out object urisObj) || urisObj is not string[] uris || uris.Length == 0)
            return null;

        return UriToLocalPath(uris[0]);
    }

    private static void AddCurrentFolder(IDictionary<string, object> options, string currentFolder)
    {
        if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
            return;

        // Portal expects a NUL-terminated UTF-8 byte array for current_folder.
        byte[] utf8 = Encoding.UTF8.GetBytes(currentFolder);
        byte[] withNul = new byte[utf8.Length + 1];
        Buffer.BlockCopy(utf8, 0, withNul, 0, utf8.Length);
        options["current_folder"] = withNul;
    }

    // Translates a WinForms-style filter ("Name (*.ext)|*.ext|...") into the
    // portal's filters option, signature a(sa(us)). Glob filter type is 0.
    private static void AddFilters(IDictionary<string, object> options, string winFormsFilter)
    {
        if (string.IsNullOrEmpty(winFormsFilter))
            return;

        string[] parts = winFormsFilter.Split('|');
        if (parts.Length < 2)
            return;

        List<(string name, (uint type, string pattern)[] patterns)> filters = [];
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            string name = parts[i];
            string[] globs = parts[i + 1].Split(';');
            (uint, string)[] entries = new (uint, string)[globs.Length];
            for (int j = 0; j < globs.Length; j++)
                entries[j] = (0u, globs[j].Trim());
            filters.Add((name, entries));
        }

        if (filters.Count == 0)
            return;

        options["filters"] = filters.ToArray();
    }

    private static string UriToLocalPath(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        const string scheme = "file://";
        if (!uri.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return null;

        string path = uri.Substring(scheme.Length);
        // Strip an optional host component (file://host/path).
        int slash = path.IndexOf('/');
        if (slash > 0)
            path = path.Substring(slash);

        return Uri.UnescapeDataString(path);
    }
}
