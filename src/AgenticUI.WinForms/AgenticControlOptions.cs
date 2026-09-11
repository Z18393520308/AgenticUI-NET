namespace AgenticUI.WinForms;

public sealed class AgenticControlOptions
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public bool IsSensitive { get; set; }
    public int InstructionNumber { get; set; }
    public string? Hint { get; set; }
    /// <summary>仅复杂自绘选项需要配置；普通控件自动使用 GetItemText / DisplayMember。</summary>
    public Func<object?, string?>? ItemTextProvider { get; set; }
    /// <summary>可选的稳定业务键解析器；默认复用 ValueMember，不公开整个业务对象。</summary>
    public Func<object?, string?>? ItemKeyProvider { get; set; }
}
