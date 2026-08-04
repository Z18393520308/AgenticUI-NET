namespace AgenticUI;

public sealed class AgenticCommandDispatcher
{
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticEventBus _events;
    private readonly IAgenticCommandAuthorizer _authorizer;

    public AgenticCommandDispatcher(
        AgenticControlRegistry? registry = null,
        AgenticEventBus? events = null,
        IAgenticCommandAuthorizer? authorizer = null)
    {
        _registry = registry ?? AgenticControlRegistry.Default;
        _events = events ?? AgenticEventBus.Default;
        _authorizer = authorizer ?? new AllowLocalCommandsAuthorizer();
    }

    public async Task<AgenticCommandResult> DispatchAsync(
        AgenticCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGet(command.ControlId, out var control) || control is null)
        {
            return AgenticCommandResult.Failure(command.RequestId, $"Unknown control '{command.ControlId}'.");
        }

        var descriptor = control.Describe();
        if (!descriptor.Actions.Contains(command.Action, StringComparer.OrdinalIgnoreCase))
        {
            return AgenticCommandResult.Failure(
                command.RequestId,
                $"Action '{command.Action}' is not supported by '{command.ControlId}'.");
        }

        if (!await _authorizer.AuthorizeAsync(descriptor, command, cancellationToken).ConfigureAwait(false))
        {
            await _events.PublishAsync(
                command.ControlId,
                AgenticEvents.RemoteActionRejected,
                AgenticEventSource.Remote,
                new Dictionary<string, object?> { ["action"] = command.Action }).ConfigureAwait(false);
            return AgenticCommandResult.Failure(command.RequestId, "Command was not authorized.");
        }

        var result = await control.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        await _events.PublishAsync(
            command.ControlId,
            result.Succeeded ? AgenticEvents.RemoteActionCompleted : AgenticEvents.RemoteActionRejected,
            AgenticEventSource.Remote,
            new Dictionary<string, object?>
            {
                ["action"] = command.Action,
                ["requestId"] = command.RequestId,
                ["error"] = result.Error
            }).ConfigureAwait(false);
        return result;
    }
}
