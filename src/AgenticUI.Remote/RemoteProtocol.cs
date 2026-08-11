using AgenticUI;

namespace AgenticUI.Remote;

public static class RemoteMessageTypes
{
    public const string Authenticate = "authenticate";
    public const string Authenticated = "authenticated";
    public const string ListControls = "listControls";
    public const string Execute = "execute";
    public const string Controls = "controls";
    public const string Result = "result";
    public const string Event = "event";
    public const string Error = "error";
}

public sealed class RemoteRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "";
    public string? AuthenticationToken { get; set; }
    public string? ClientName { get; set; }
    public AgenticCommand? Command { get; set; }

    /// <summary>
    /// When listing controls, include hidden/obscured/off-screen controls.
    /// Default is false: remote agents only see currently displayable controls.
    /// </summary>
    public bool IncludeHidden { get; set; }
}

public sealed class RemoteResponse
{
    public string? RequestId { get; set; }
    public string Type { get; set; } = "";
    public IReadOnlyList<AgenticControlDescriptor>? Controls { get; set; }
    public AgenticCommandResult? Result { get; set; }
    public AgenticEvent? Event { get; set; }
    public string? Error { get; set; }
}
