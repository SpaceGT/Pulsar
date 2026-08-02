namespace Pulsar.Protocol.Interface;

public enum InterfaceOperation
{
    Hello,
    SplashShow,
    SplashTitle,
    SplashText,
    SplashProgress,
    SplashClose,
    PromptShow,
    FileOpen,
    FolderOpen,
    ClipboardGet,
    EscapePressed,
}

public class InterfaceRequest
{
    public InterfaceOperation Operation { get; set; }

    public PromptRequest Prompt { get; set; }

    public FilePickerRequest FilePicker { get; set; }

    public string Text { get; set; }

    public float? Progress { get; set; }
}

public class InterfaceResponse
{
    public string Error { get; set; }

    public PromptResult PromptResult { get; set; }

    public bool Value { get; set; }

    public string Text { get; set; }
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

    public FilePickerFilter[] Filters { get; set; }
}

public class FilePickerFilter
{
    public string Name { get; set; }

    public string[] Patterns { get; set; }
}
