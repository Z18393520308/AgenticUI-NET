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
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public string Type { get; init; } = "";
    public string? AuthenticationToken { get; init; }
    public string? ClientName { get; init; }
    public AgenticCommand? Command { get; init; }
}

public sealed class RemoteResponse
{
    public string? RequestId { get; init; }
    public string Type { get; init; } = "";
    public IReadOnlyList<AgenticControlDescriptor>? Controls { get; init; }
    public AgenticCommandResult? Result { get; init; }
    public AgenticEvent? Event { get; init; }
    public string? Error { get; init; }
}
