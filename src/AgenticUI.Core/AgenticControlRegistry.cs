namespace AgenticUI;

public sealed class AgenticControlRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WeakReference<IAgenticControl>> _controls =
        new(StringComparer.OrdinalIgnoreCase);
    private long _temporaryId;

    public static AgenticControlRegistry Default { get; } = new();

    public string Register(IAgenticControl control, string? stableId = null)
    {
        if (control is null)
        {
            throw new ArgumentNullException(nameof(control));
        }
        var id = string.IsNullOrWhiteSpace(stableId)
            ? $"temporary.{Interlocked.Increment(ref _temporaryId)}"
            : stableId!.Trim();

        lock (_gate)
        {
            PruneLocked();
            if (_controls.TryGetValue(id, out var existing) &&
                existing.TryGetTarget(out var target) &&
                !ReferenceEquals(target, control))
            {
                throw new InvalidOperationException($"Agentic control ID '{id}' is already registered.");
            }

            _controls[id] = new WeakReference<IAgenticControl>(control);
        }

        return id;
    }

    public void Unregister(string id, IAgenticControl control)
    {
        lock (_gate)
        {
            if (_controls.TryGetValue(id, out var reference) &&
                reference.TryGetTarget(out var target) &&
                ReferenceEquals(target, control))
            {
                _controls.Remove(id);
            }
        }
    }

    public bool TryGet(string id, out IAgenticControl? control)
    {
        lock (_gate)
        {
            if (_controls.TryGetValue(id, out var reference) && reference.TryGetTarget(out control))
            {
                return true;
            }

            _controls.Remove(id);
            control = null;
            return false;
        }
    }

    public IReadOnlyList<AgenticControlDescriptor> Snapshot(bool remotelyDiscoverableOnly = false)
    {
        IAgenticControl[] controls;
        lock (_gate)
        {
            PruneLocked();
            controls = _controls.Values
                .Select(reference => reference.TryGetTarget(out var control) ? control : null)
                .Where(control => control is not null)
                .Cast<IAgenticControl>()
                .ToArray();
        }

        // Describe / discoverability may synchronously marshal to a UI thread. Never hold
        // the registry lock while waiting for that thread because UI unload/handle events
        // also register and unregister controls.
        IEnumerable<IAgenticControl> query = controls;
        if (remotelyDiscoverableOnly)
        {
            query = query.Where(control =>
            {
                try
                {
                    return control.IsRemotelyDiscoverable();
                }
                catch
                {
                    return false;
                }
            });
        }

        return query
            .Select(control => control.Describe())
            .OrderBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void PruneLocked()
    {
        var dead = _controls.Where(x => !x.Value.TryGetTarget(out _)).Select(x => x.Key).ToArray();
        foreach (var id in dead)
        {
            _controls.Remove(id);
        }
    }
}
