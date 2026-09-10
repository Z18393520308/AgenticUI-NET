namespace AgenticUI;

public static class AgenticActionPolicy
{
    /// <summary>只读和纯展示可以观察禁用控件，但不能借此执行修改。</summary>
    public static bool IsObservation(string action) => action is
        AgenticActions.GetText or AgenticActions.GetValue or AgenticActions.GetChecked or
        AgenticActions.GetRow or AgenticActions.GetRows or AgenticActions.GetColumns or AgenticActions.GetCell or
        AgenticActions.Highlight or AgenticActions.ClearHighlight;
}
