namespace AgenticUI;

public sealed class AgenticCommandDispatcher
{
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticEventBus _events;
    private readonly IAgenticCommandAuthorizer _authorizer;

    public AgenticCommandDispatcher(AgenticControlRegistry? registry = null,
        AgenticEventBus? events = null, IAgenticCommandAuthorizer? authorizer = null)
    {
        _registry = registry ?? AgenticControlRegistry.Default;
        _events = events ?? AgenticEventBus.Default;
        _authorizer = authorizer ?? new AllowLocalCommandsAuthorizer();
    }

    public async Task<AgenticCommandResult> DispatchAsync(AgenticCommand command,
        CancellationToken cancellationToken = default)
    {
        AgenticControlDescriptor? descriptor = null;
        AgenticCommandResult result;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_registry.TryGet(command.ControlId, out var control) || control is null)
                result = AgenticCommandResult.Failure(command.RequestId, $"Unknown control '{command.ControlId}'.");
            else
            {
                descriptor = control.Describe();
                if (!descriptor.Actions.Contains(command.Action, StringComparer.OrdinalIgnoreCase))
                    result = AgenticCommandResult.Failure(command.RequestId, $"Action '{command.Action}' is not supported by '{command.ControlId}'.");
                else if (!await _authorizer.AuthorizeAsync(descriptor, command, cancellationToken).ConfigureAwait(false))
                    result = AgenticCommandResult.Failure(command.RequestId, "Command was not authorized.");
                else
                    result = await control.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            // 控件可能已在动作后抛出异常，调用方不得据此盲目重试非幂等业务动作。
            result = AgenticCommandResult.Failure(command.RequestId,
                descriptor?.IsSensitive == true ? "Sensitive control operation failed." : exception.Message);
        }
        if (descriptor?.IsSensitive == true && !result.Succeeded)
            result.Error = "Sensitive control operation failed.";
        var message = _events.Create(command.ControlId,
            result.Succeeded ? AgenticEvents.RemoteActionCompleted : AgenticEvents.RemoteActionRejected,
            AgenticEventSource.Remote, new Dictionary<string, object?>
            {
                ["action"] = command.Action, ["requestId"] = command.RequestId,
                ["error"] = descriptor?.IsSensitive == true && !result.Succeeded ? "Sensitive control operation failed." : result.Error,
                ["guidanceId"] = command.Arguments.TryGetValue("guidanceId", out var id) ? id : null,
                ["outcomeScope"] = "control"
            });
        message.IsSensitive = descriptor?.IsSensitive == true;
        await _events.PublishAsync(message).ConfigureAwait(false);
        return result;
    }
}
