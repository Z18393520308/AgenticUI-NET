namespace AgenticUI;

public sealed class AgenticEventBus
{
    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = new();
    private long _sequence;

    public static AgenticEventBus Default { get; } = new();

    public IDisposable Subscribe(Func<AgenticEvent, ValueTask> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
        var subscription = new Subscription(this, handler);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    public AgenticEvent Create(
        string controlId,
        string name,
        AgenticEventSource source,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        return new AgenticEvent
        {
            Sequence = Interlocked.Increment(ref _sequence),
            ControlId = controlId,
            Name = name,
            Source = source,
            Timestamp = DateTimeOffset.UtcNow,
            Data = data ?? new Dictionary<string, object?>()
        };
    }

    public async ValueTask PublishAsync(
        string controlId,
        string name,
        AgenticEventSource source = AgenticEventSource.User,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        await PublishAsync(Create(controlId, name, source, data)).ConfigureAwait(false);
    }

    public async ValueTask PublishAsync(AgenticEvent message)
    {
        Func<AgenticEvent, ValueTask>[] handlers;
        lock (_gate)
        {
            handlers = _subscriptions.Where(x => !x.IsDisposed).Select(x => x.Handler).ToArray();
        }

        foreach (var handler in handlers)
        {
            await handler(message).ConfigureAwait(false);
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private AgenticEventBus? _owner;
        public Subscription(AgenticEventBus owner, Func<AgenticEvent, ValueTask> handler)
        {
            _owner = owner;
            Handler = handler;
        }

        public Func<AgenticEvent, ValueTask> Handler { get; }
        public bool IsDisposed => _owner is null;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Remove(this);
        }
    }
}
