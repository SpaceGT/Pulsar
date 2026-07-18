#nullable disable

using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Pulsar.Interface.Protocol;

public static class InterfaceMethods
{
    public const string Hello = "hello";
    public const string SplashShow = "splash.show";
    public const string SplashTitle = "splash.title";
    public const string SplashText = "splash.text";
    public const string SplashProgress = "splash.progress";
    public const string SplashClose = "splash.close";
    public const string PromptShow = "prompt.show";
    public const string FileOpen = "file.open";
    public const string FolderOpen = "folder.open";
    public const string ClipboardGet = "clipboard.get";
    public const string EscapePressed = "keyboard.escapePressed";
    public const string Shutdown = "shutdown";
}

public class InterfaceRequest
{
    public int Version { get; set; } = 1;
    public long Id { get; set; }
    public string Method { get; set; }
    public PromptRequest Prompt { get; set; }
    public FilePickerRequest FilePicker { get; set; }
    public string Text { get; set; }
    public float? Progress { get; set; }
}

public class InterfaceResponse
{
    public int Version { get; set; } = 1;
    public long Id { get; set; }
    public bool Ok { get; set; }
    public PromptResult PromptResult { get; set; }
    public bool Value { get; set; }
    public string Text { get; set; }
    public string Error { get; set; }
}

public static class ProtocolJson
{
    public static string Serialize<T>(T value)
    {
        using MemoryStream stream = new();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static T Deserialize<T>(string json)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
}

public enum PromptButtons
{
    Ok,
    YesNo,
    YesNoCancel,
}

public enum PromptIcon
{
    None,
    Error,
    Warning,
    Question,
    Information,
}

public enum PromptResult
{
    Cancel,
    Ok,
    Yes,
    No,
}

public class PromptRequest
{
    public string Caption { get; set; }
    public string Message { get; set; }
    public PromptButtons Buttons { get; set; }
    public PromptIcon Icon { get; set; }
}

public class FilePickerRequest
{
    public string Title { get; set; }
    public string Directory { get; set; }
    public string Filter { get; set; }
}
