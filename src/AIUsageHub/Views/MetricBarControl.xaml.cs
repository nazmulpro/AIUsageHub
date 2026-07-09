using System.Windows;
using System.Windows.Controls;
using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Views;

public partial class MetricBarControl : UserControl
{
    public static readonly DependencyProperty MetricProperty =
        DependencyProperty.Register(nameof(Metric), typeof(MetricLine), typeof(MetricBarControl),
            new PropertyMetadata(null, OnMetricChanged));

    public MetricLine? Metric
    {
        get => (MetricLine?)GetValue(MetricProperty);
        set => SetValue(MetricProperty, value);
    }

    public MetricBarControl()
    {
        InitializeComponent();
    }

    private static void OnMetricChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricBarControl ctrl && e.NewValue is MetricLine metric)
        {
            ctrl.DataContext = new MetricBarViewModel(metric);
        }
    }

    private class MetricBarViewModel
    {
        private readonly MetricLine _m;
        public string Label => _m.Label;
        public string PercentText => FormatHelpers.FormatPercent(_m.Used, _m.Limit);
        public double Used => _m.Used;
        public double Limit => _m.Limit;
        public string VerdictColor => FormatHelpers.GetBarVerdict(_m.Used, _m.Limit, _m.ResetsAt, _m.PeriodDurationMs);
        public string CountdownText => FormatHelpers.FormatCountdown(_m.ResetsAt);
        public string PaceText => FormatHelpers.FormatPaceIndicator(_m.Used, _m.Limit, _m.ResetsAt, _m.PeriodDurationMs);
        public bool HasPace => !string.IsNullOrEmpty(PaceText);

        public MetricBarViewModel(MetricLine m) => _m = m;
    }
}