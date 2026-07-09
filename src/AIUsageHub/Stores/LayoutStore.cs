using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Stores;

public sealed class LayoutStore : INotifyPropertyChanged
{
    private readonly ConfigManager _config;
    private List<WidgetDescriptor> _widgets = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public List<WidgetDescriptor> Widgets
    {
        get => _widgets;
        private set { _widgets = value; OnPropertyChanged(); }
    }

    public LayoutStore(ConfigManager config)
    {
        _config = config;
        LoadLayout();
    }

    private void LoadLayout()
    {
        // Default layout: all providers with standard metrics
        Widgets = new List<WidgetDescriptor>();
    }

    public bool IsMetricVisible(string provider, string metric)
        => Widgets.FirstOrDefault(w => w.ProviderName == provider && w.MetricLabel == metric)?.IsVisible ?? true;

    public bool IsPinnedToTray(string provider, string metric)
        => Widgets.FirstOrDefault(w => w.ProviderName == provider && w.MetricLabel == metric)?.IsPinnedToTray ?? false;

    public void TogglePin(string provider, string metric)
    {
        var widget = Widgets.FirstOrDefault(w => w.ProviderName == provider && w.MetricLabel == metric);
        if (widget != null)
        {
            Widgets = Widgets.Select(w =>
                w.ProviderName == provider && w.MetricLabel == metric
                    ? w with { IsPinnedToTray = !w.IsPinnedToTray }
                    : w).ToList();
        }
    }

    public void ToggleVisibility(string provider, string metric)
    {
        var widget = Widgets.FirstOrDefault(w => w.ProviderName == provider && w.MetricLabel == metric);
        if (widget != null)
        {
            Widgets = Widgets.Select(w =>
                w.ProviderName == provider && w.MetricLabel == metric
                    ? w with { IsVisible = !w.IsVisible }
                    : w).ToList();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}