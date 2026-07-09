using System.Windows;
using System.Windows.Input;
using AIUsageHub.Helpers;
using AIUsageHub.ViewModels;

namespace AIUsageHub.Views;

public partial class TrayPopupWindow : Window
{
    private bool _isSettingsOpen;
    /// <summary>
    /// When true, the whole dashboard becomes non-interactive (no drag, no
    /// button clicks) so it never steals focus from the Settings window.
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            _isSettingsOpen = value;
            if (RootBorder != null)
                RootBorder.IsHitTestVisible = !value;
        }
    }

    private bool _hasUserMovedPosition;
    /// <summary>
    /// True once the user has dragged the dashboard; the moved position then
    /// persists across hide/show instead of snapping back to the tray.
    /// </summary>
    public bool HasUserMovedPosition => _hasUserMovedPosition;

    public TrayPopupWindow()
    {
        InitializeComponent();
        WindowPlacement.PositionNearTray(this, 410, 580);

        // Escape → minimize to the taskbar
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) WindowState = WindowState.Minimized;
        };
    }

    public void SetViewModel(DashboardViewModel vm)
    {
        DashboardContent.ViewModel = vm;
        DataContext = vm;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !IsSettingsOpen)
        {
            _hasUserMovedPosition = true;
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        // Position near the tray on first render; once the user has dragged
        // the dashboard, keep their position.
        if (!_hasUserMovedPosition)
            WindowPlacement.PositionNearTray(this, 410, ActualHeight > 100 ? ActualHeight : 580);
    }
}
