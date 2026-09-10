namespace AgenticUI.Gateway;

/// <summary>单连接串行处理的有界去重窗口；不是业务 exactly-once 或跨连接幂等承诺。</summary>
internal sealed class RecentRequestIds(int capacity)
{
    private readonly Queue<string> _order = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    public bool TryAdd(string requestId)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (!_ids.Add(requestId)) return false;
        _order.Enqueue(requestId);
        while (_order.Count > capacity) _ids.Remove(_order.Dequeue());
        return true;
    }
}
