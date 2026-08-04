namespace AgenticUI;

public interface IAgenticControl
{
    AgenticControlDescriptor Describe();
    Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default);
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
