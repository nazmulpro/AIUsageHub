using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIUsageHub.ViewModels;

namespace AIUsageHub.Views;

public partial class SettingsView : Window
{
    public SettingsView()
    {
        InitializeComponent();
    }

    public SettingsView(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OnGetKey(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string url && !string.IsNullOrEmpty(url))
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetKey] {ex.Message}");
            }
        }
    }
}