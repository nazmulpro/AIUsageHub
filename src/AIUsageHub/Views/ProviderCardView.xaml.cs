using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using AIUsageHub;
using AIUsageHub.Models;
using AIUsageHub.Services;
using AIUsageHub.Stores;

namespace AIUsageHub.Views;

public partial class ProviderCardView : UserControl
{
    public static readonly DependencyProperty CardDataProperty =
        DependencyProperty.Register(nameof(CardData), typeof(ProviderCardData), typeof(ProviderCardView),
            new PropertyMetadata(null, OnCardDataChanged));

    public ProviderCardData? CardData
    {
        get => (ProviderCardData?)GetValue(CardDataProperty);
        set => SetValue(CardDataProperty, value);
    }

    private ConfigManager? _configManager;

    public ProviderCardView()
    {
        InitializeComponent();
    }

    private static void OnCardDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProviderCardView ctrl && e.NewValue is ProviderCardData data)
        {
            if (ctrl._configManager != null)
                ctrl._configManager.SettingsChanged -= ctrl.OnSettingsChanged;

            ctrl._configManager = ((App)Application.Current).Services.GetRequiredService<ConfigManager>();
            ctrl.DataContext = new ProviderCardViewModel(data, ctrl._configManager);
            ctrl._configManager.SettingsChanged += ctrl.OnSettingsChanged;
        }
    }

    private void OnSettingsChanged()
    {
        if (DataContext is ProviderCardViewModel vm)
            vm.ApplyMinimalView();
    }

    private void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProviderCardViewModel vm && !string.IsNullOrEmpty(vm.LoginUrl))
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(vm.LoginUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] {ex.Message}");
            }
        }
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProviderCardViewModel vm) return;

        vm.IsExpanded = !vm.IsExpanded;
        AnimateSection(vm.IsExpanded);
    }

    private void AnimateSection(bool expand)
    {
        if (expand)
        {
            AdditionalSection.Visibility = Visibility.Visible;
            AdditionalSection.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
            AdditionalSection.MaxHeight = double.PositiveInfinity;
            AdditionalSection.UpdateLayout();

            var targetHeight = AdditionalSection.ActualHeight;
            if (targetHeight <= 0) return;

            var slideIn = new DoubleAnimation(0, targetHeight, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));

            Storyboard.SetTarget(slideIn, AdditionalSection);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("MaxHeight"));

            Storyboard.SetTarget(fadeIn, AdditionalSection);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

            var sb = new Storyboard();
            sb.Children.Add(slideIn);
            sb.Children.Add(fadeIn);
            sb.Begin();
        }
        else
        {
            var currentHeight = AdditionalSection.ActualHeight;
            if (currentHeight <= 0) return;

            var slideOut = new DoubleAnimation(currentHeight, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));

            Storyboard.SetTarget(slideOut, AdditionalSection);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("MaxHeight"));

            Storyboard.SetTarget(fadeOut, AdditionalSection);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

            var sb = new Storyboard();
            sb.Children.Add(slideOut);
            sb.Children.Add(fadeOut);

            sb.Completed += (_, _) =>
            {
                AdditionalSection.Visibility = Visibility.Collapsed;
                AdditionalSection.MaxHeight = 0;
            };

            sb.Begin();
        }
    }
}

/// Wrapper view model for XAML binding on ProviderCardData
public sealed class ProviderCardViewModel : INotifyPropertyChanged
{
    private const int DefaultVisibleLines = 2;
    private readonly ProviderCardData _data;
    private readonly ConfigManager _config;
    private readonly List<MetricLineViewModel> _allLines;

    public string ProviderName => _data.ProviderName;
    public string BrandColor => _data.BrandColor;
    public string BrandIcon => GetIconPath(_data.ProviderName, _config.Settings.Theme);
    public string? Plan => _data.Plan;
    public string? Error => _data.Error;
    public bool HasPlan => !string.IsNullOrEmpty(Plan);
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string? LoginUrl => _data.LoginUrl;
    public bool IsLoginError => HasError && string.Equals(Error, "Not logged in", StringComparison.OrdinalIgnoreCase);
    public bool HasGenericError => HasError && !IsLoginError;
    public bool HasOnDemand => _config.Settings.ShowMinimalView && _data.Lines.Count > DefaultVisibleLines;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToggleText));
        }
    }

    public string ToggleText => _isExpanded ? "▴" : "▾";

    public ObservableCollection<MetricLineViewModel> AlwaysVisibleLines { get; }
    public ObservableCollection<MetricLineViewModel> AdditionalLines { get; }

    public ProviderCardViewModel(ProviderCardData data, ConfigManager config)
    {
        _data = data;
        _config = config;
        _allLines = data.Lines.Select(l => new MetricLineViewModel(l)).ToList();

        AlwaysVisibleLines = new ObservableCollection<MetricLineViewModel>();
        AdditionalLines = new ObservableCollection<MetricLineViewModel>();
        ApplyMinimalView();
    }

    public void ApplyMinimalView()
    {
        var showMinimal = _config.Settings.ShowMinimalView;

        AlwaysVisibleLines.Clear();
        AdditionalLines.Clear();

        if (showMinimal)
        {
            foreach (var line in _allLines.Take(DefaultVisibleLines))
                AlwaysVisibleLines.Add(line);
            foreach (var line in _allLines.Skip(DefaultVisibleLines))
                AdditionalLines.Add(line);
        }
        else
        {
            foreach (var line in _allLines)
                AlwaysVisibleLines.Add(line);
        }

        OnPropertyChanged(nameof(HasOnDemand));
        OnPropertyChanged(nameof(ToggleText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private static string GetIconPath(string name, Models.ThemeMode theme)
    {
        var safe = name.ToLowerInvariant().Replace(".", "").Replace(" ", "");
        if (theme == Models.ThemeMode.Light && HasLightVariant(safe))
            return $"/Resources/Providers/{safe}-light.svg";
        return $"/Resources/Providers/{safe}.svg";
    }

    private static bool HasLightVariant(string safe) => safe switch
    {
        "cursor" or "grok" or "openrouter" or "codex" => true,
        _ => false
    };
}

/// Wrapper for MetricLine XAML binding
public sealed class MetricLineViewModel
{
    private readonly MetricLine _line;

    public MetricLine Line => _line;
    public string Label => _line.Label;
    public string? TextValue => _line.TextValue;
    public string? BadgeText => _line.BadgeText;
    public bool IsProgress => _line.Kind == MetricKind.Progress;
    public bool IsValues => _line.Kind == MetricKind.Values;
    public bool IsText => _line.Kind == MetricKind.Text;
    public bool IsBadge => _line.Kind == MetricKind.Badge;

    public string DollarText => FormatDollarDisplay();
    public string TokenText => FormatTokenDisplay();

    public MetricLineViewModel(MetricLine line) => _line = line;

    private string FormatDollarDisplay()
    {
        if (_line.MetricValues == null) return string.Empty;
        var dollar = _line.MetricValues.FirstOrDefault(v => v.Kind == MetricValueKind.Dollars);
        return dollar != null ? FormatHelpers.FormatDollars(dollar.Value) : string.Empty;
    }

    private string FormatTokenDisplay()
    {
        if (_line.MetricValues == null) return string.Empty;
        var token = _line.MetricValues.FirstOrDefault(v => v.Kind == MetricValueKind.Tokens);
        return token != null ? FormatHelpers.FormatTokens(token.Value) : string.Empty;
    }
}