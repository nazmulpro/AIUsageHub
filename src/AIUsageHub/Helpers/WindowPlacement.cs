using System.Windows;

namespace AIUsageHub.Helpers;

public static class WindowPlacement
{
    /// Positions a window near the system tray area.
    public static void PositionNearTray(Window window, double width, double height)
    {
        // Get the taskbar tray area (typically bottom-right)
        var screen = SystemParameters.WorkArea;
        var x = screen.Right - width - 12;
        var y = screen.Bottom - height - 12;

        // Ensure window stays on screen
        if (x < screen.Left) x = screen.Left + 12;
        if (y < screen.Top) y = screen.Top + 12;

        window.Left = x;
        window.Top = y;
        window.Width = width;
        window.Height = height;
    }
}