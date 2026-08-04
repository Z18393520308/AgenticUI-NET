using System.Windows;

namespace AgenticUI.Wpf;

public static class AgenticProperties
{
    private static readonly DependencyProperty AdapterProperty = DependencyProperty.RegisterAttached(
        "Adapter",
        typeof(WpfControlAdapter),
        typeof(AgenticProperties));

    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id",
        typeof(string),
        typeof(AgenticProperties),
        new PropertyMetadata(null, OnMetadataChanged));

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(AgenticProperties),
        new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty SensitiveProperty = DependencyProperty.RegisterAttached(
        "Sensitive",
        typeof(bool),
        typeof(AgenticProperties),
        new PropertyMetadata(false, OnMetadataChanged));

    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.RegisterAttached(
        "DisplayName",
        typeof(string),
        typeof(AgenticProperties),
        new PropertyMetadata(null, OnMetadataChanged));

    public static readonly DependencyProperty InstructionNumberProperty = DependencyProperty.RegisterAttached(
        "InstructionNumber",
        typeof(int),
        typeof(AgenticProperties),
        new PropertyMetadata(0));

    public static readonly DependencyProperty HintProperty = DependencyProperty.RegisterAttached(
        "Hint",
        typeof(string),
        typeof(AgenticProperties),
        new PropertyMetadata(null));

    public static string? GetId(DependencyObject target) => (string?)target.GetValue(IdProperty);
    public static void SetId(DependencyObject target, string? value) => target.SetValue(IdProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetSensitive(DependencyObject target) => (bool)target.GetValue(SensitiveProperty);
    public static void SetSensitive(DependencyObject target, bool value) => target.SetValue(SensitiveProperty, value);
    public static string? GetDisplayName(DependencyObject target) => (string?)target.GetValue(DisplayNameProperty);
    public static void SetDisplayName(DependencyObject target, string? value) =>
        target.SetValue(DisplayNameProperty, value);
    public static int GetInstructionNumber(DependencyObject target) => (int)target.GetValue(InstructionNumberProperty);
    public static void SetInstructionNumber(DependencyObject target, int value) =>
        target.SetValue(InstructionNumberProperty, value);
    public static string? GetHint(DependencyObject target) => (string?)target.GetValue(HintProperty);
    public static void SetHint(DependencyObject target, string? value) => target.SetValue(HintProperty, value);

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            throw new InvalidOperationException("AgenticUI can only be enabled on FrameworkElement instances.");
        }

        if ((bool)args.NewValue)
        {
            if (element.GetValue(AdapterProperty) is not WpfControlAdapter adapter)
            {
                adapter = new WpfControlAdapter(element);
                element.SetValue(AdapterProperty, adapter);
            }

            adapter.Attach();
        }
        else if (element.GetValue(AdapterProperty) is WpfControlAdapter adapter)
        {
            adapter.Detach();
            element.ClearValue(AdapterProperty);
        }
    }

    private static void OnMetadataChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject.GetValue(AdapterProperty) is WpfControlAdapter adapter)
        {
            adapter.RefreshRegistration();
        }
    }
}
