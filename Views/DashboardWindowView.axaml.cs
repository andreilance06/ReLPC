using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ReLPC.ViewModels;

namespace ReLPC.Views;

public partial class DashboardWindowView : Window
{
    private readonly DispatcherTimer _gradientTimer;
    private readonly DateTime _gradientAnimationStartedAt = DateTime.Now;

    public DashboardWindowView()
    {
        InitializeComponent();
        Opened += OnOpened;

        _gradientTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _gradientTimer.Tick += OnGradientTimerTick;
        Closing += (_, _) =>
        {
            _gradientTimer.Stop();
            _gradientTimer.Tick -= OnGradientTimerTick;
        };
        _gradientTimer.Start();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is DashboardWindowViewModel vm)
        {
            vm.HostWindow = this;
            vm.RefreshRecentDatasets();
        }
    }

    private void OnRecentDatasetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardWindowViewModel vm)
            return;

        if (sender is Control { DataContext: DashboardDatasetItem item })
            vm.OpenRecentDatasetCommand.Execute(item);
    }

    private void OnGradientTimerTick(object? sender, EventArgs e)
    {
        if (SidebarPanel is null)
            return;

        var elapsedSeconds = (DateTime.Now - _gradientAnimationStartedAt).TotalSeconds;
        var sidebarFlow = (Math.Sin(elapsedSeconds * 1.15 + 1.2) + 1) / 2;
        SidebarPanel.Background = CreateAnimatedGradient(sidebarFlow);
    }

    private static LinearGradientBrush CreateAnimatedGradient(double flow)
    {
        var highlightStart = Math.Clamp(flow - 0.18, 0, 1);
        var highlightMiddle = Math.Clamp(flow, 0, 1);
        var highlightEnd = Math.Clamp(flow + 0.18, 0, 1);

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
        };

        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF2D32"), 0));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF3A34"), highlightStart));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF7550"), highlightMiddle));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF553C"), highlightEnd));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF7442"), 1));

        return brush;
    }
}
