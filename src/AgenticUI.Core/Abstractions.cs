namespace AgenticUI;

public interface IAgenticControl
{
    AgenticControlDescriptor Describe();
    Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the control is currently shown to the user (on the active surface,
    /// within clipped viewport, and not fully obscured). Used by remote listControls.
    /// </summary>
    bool IsRemotelyDiscoverable();
}

public interface IAgenticEventSink
{
    ValueTask WriteAsync(AgenticEvent message, CancellationToken cancellationToken = default);
}

public interface IAgenticCommandAuthorizer
{
    ValueTask<bool> AuthorizeAsync(
        AgenticControlDescriptor control,
        AgenticCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class AllowLocalCommandsAuthorizer : IAgenticCommandAuthorizer
{
    public ValueTask<bool> AuthorizeAsync(
        AgenticControlDescriptor control,
        AgenticCommand command,
        CancellationToken cancellationToken = default) => new(true);
}
