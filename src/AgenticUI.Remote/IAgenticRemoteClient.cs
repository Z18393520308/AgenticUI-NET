using AgenticUI;

namespace AgenticUI.Remote;

public interface IAgenticRemoteClient : IDisposable
{
    event Action<AgenticEvent>? EventReceived;

    event Action<Exception>? ConnectionFaulted;

    bool IsConnected { get; }

    Task<RemoteResponse> ListControlsAsync(CancellationToken cancellationToken = default);

    Task<RemoteResponse> ListControlsAsync(
        bool includeHidden,
        CancellationToken cancellationToken = default);

    Task<RemoteResponse> ExecuteAsync(
        AgenticCommand command,
        CancellationToken cancellationToken = default);
}
