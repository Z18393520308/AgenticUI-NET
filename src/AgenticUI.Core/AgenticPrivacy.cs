using System.Text.Json;

namespace AgenticUI;

public static class AgenticPrivacy
{
    public static AgenticControlDescriptor SanitizeDescriptor(AgenticControlDescriptor control) => !control.IsSensitive ? control :
        new AgenticControlDescriptor
        {
            Id = control.Id, Name = control.Name, Kind = control.Kind, IsTemporaryId = control.IsTemporaryId,
            IsEnabled = control.IsEnabled, IsSensitive = true, Actions = control.Actions, Capabilities = control.Capabilities,
            // 只读标记是能力约束，不是用户内容。严格只接受布尔类型，禁止借同名字段夹带秘密。
            State = control.State.ToDictionary(x => x.Key, x => x.Key == "readOnly" ? SafeBoolean(x.Value) : null)
        };
    private static object? SafeBoolean(object? value) => value switch
    {
        bool flag => flag,
        JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        _ => null
    };
    /// <summary>对整个敏感载荷脱敏，包括嵌套行列数据；不依赖控件在发送时仍存活。</summary>
    public static AgenticEvent SanitizeEvent(AgenticEvent message, bool sensitive, string replacement = "***")
    {
        if (!sensitive && !message.IsSensitive) return message;
        return new AgenticEvent
        {
            Sequence = message.Sequence, ControlId = message.ControlId, Name = message.Name,
            Source = message.Source, Timestamp = message.Timestamp, IsSensitive = true,
            Data = message.Data.ToDictionary(x => x.Key,
                x => x.Key is "action" or "requestId" ? x.Value : (object?)replacement)
        };
    }
}
