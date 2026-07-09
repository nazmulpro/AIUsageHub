using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using AIUsageHub.ViewModels;

namespace AIUsageHub.Views;

public partial class DashboardView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(DashboardViewModel), typeof(DashboardView),
            new PropertyMetadata(null));

    public DashboardViewModel? ViewModel
    {
        get => (DashboardViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel is { } vm)
                vm.ScrollToTopRequested += () => ContentScroller?.ScrollToTop();
        };
    }
}