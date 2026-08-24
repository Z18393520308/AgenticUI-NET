using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AgenticUI.Wpf;

internal static class WpfDisplayability
{
    public static bool IsDisplayable(FrameworkElement element)
    {
        if (element is null ||
            !element.IsVisible ||
            element.ActualWidth < 2 ||
            element.ActualHeight < 2 ||
            PresentationSource.FromVisual(element) is null)
        {
            return false;
        }

        var window = Window.GetWindow(element);
        if (window is null ||
            !window.IsVisible ||
            window.WindowState == WindowState.Minimized ||
            window.ActualWidth < 2 ||
            window.ActualHeight < 2)
        {
            return false;
        }

        // Modal dialog owns exclusive remote interaction (same as UI modality rules).
        if (!IsInActiveInteractionScope(window))
        {
            return false;
        }

        if (IsOnInactiveTabItem(element))
        {
            return false;
        }

        GeneralTransform transform;
        try
        {
            transform = element.TransformToAncestor(window);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        var windowBounds = new Rect(0, 0, window.ActualWidth, window.ActualHeight);
        bounds = Rect.Intersect(bounds, windowBounds);
        bounds = ClipToScrollViewers(element, window, bounds);
        if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2)
        {
            return false;
        }

        var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        IInputElement? hitElement;
        try
        {
            hitElement = window.InputHitTest(center);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // InputHitTest is window-local; foreign overlays are already excluded.
        if (hitElement is not DependencyObject hit)
        {
            return false;
        }

        return IsAssociated(element, hit);
    }

    /// <summary>
    /// When a modal window is open, only controls on that topmost modal window are interactable.
    /// </summary>
    public static bool IsInActiveInteractionScope(Window window)
    {
        var modal = GetTopModalWindow();
        if (modal is null)
        {
            return true;
        }

        return ReferenceEquals(window, modal);
    }

    private static Window? GetTopModalWindow()
    {
        if (Application.Current is null || !ComponentDispatcher.IsThreadModal)
        {
            return null;
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        Window? activeEnabled = null;
        Window? lastEnabled = null;
        foreach (Window open in Application.Current.Windows)
        {
            if (!ReferenceEquals(open.Dispatcher, dispatcher) || !open.IsVisible)
            {
                continue;
            }

            var handle = new WindowInteropHelper(open).Handle;
            if (handle == IntPtr.Zero || !IsWindowEnabled(handle))
            {
                // Owner windows are disabled while a modal dialog is open.
                continue;
            }

            lastEnabled = open;
            if (open.IsActive)
            {
                activeEnabled = open;
            }
        }

        return activeEnabled ?? lastEnabled;
    }

    public static bool IsPointInteractable(FrameworkElement element, Point elementPoint)
    {
        if (!IsDisplayable(element) ||
            elementPoint.X < 0 ||
            elementPoint.Y < 0 ||
            elementPoint.X >= element.ActualWidth ||
            elementPoint.Y >= element.ActualHeight)
        {
            return false;
        }

        var window = Window.GetWindow(element);
        if (window is null)
        {
            return false;
        }

        Point windowPoint;
        try
        {
            windowPoint = element.TranslatePoint(elementPoint, window);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        IInputElement? hitElement;
        try
        {
            hitElement = window.InputHitTest(windowPoint);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return hitElement is DependencyObject hit && IsAssociated(element, hit);
    }

    private static bool IsOnInactiveTabItem(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null;)
        {
            if (current is TabItem tabItem && !tabItem.IsSelected)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) ??
                      (current is FrameworkElement frameworkElement ? frameworkElement.Parent : null);
        }

        return false;
    }

    private static Rect ClipToScrollViewers(DependencyObject element, Window window, Rect bounds)
    {
        for (var current = element; current is not null && !ReferenceEquals(current, window);)
        {
            if (current is ScrollViewer scrollViewer)
            {
                try
                {
                    var toWindow = scrollViewer.TransformToAncestor(window);
                    var viewport = toWindow.TransformBounds(
                        new Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight));
                    bounds = Rect.Intersect(bounds, viewport);
                    if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2)
                    {
                        return Rect.Empty;
                    }
                }
                catch (InvalidOperationException)
                {
                    return Rect.Empty;
                }
            }

            current = VisualTreeHelper.GetParent(current) ??
                      (current is FrameworkElement fe ? fe.Parent : null);
        }

        return bounds;
    }

    private static bool IsAssociated(DependencyObject target, DependencyObject hit)
    {
        for (DependencyObject? current = hit; current is not null;)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) ??
                      (current is FrameworkElement fe ? fe.Parent : null);
        }

        for (DependencyObject? current = target; current is not null;)
        {
            if (ReferenceEquals(current, hit))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) ??
                      (current is FrameworkElement fe ? fe.Parent : null);
        }

        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);
}
