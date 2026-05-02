using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using MathNet.Numerics;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class MainWindowViewModel(
    ISessionService sessionService,
    IDatabaseService databaseService,
    IWindowService windowService) : ViewModelBase
{
    public ObservableCollection<ISeries> Series { get; set; } = [];

    [ObservableProperty] public partial bool LinearVisible { get; set; } = true;
    [ObservableProperty] public partial bool PolynomialVisible { get; set; } = true;

    partial void OnLinearVisibleChanged(bool value)
    {
        var series = Series.FirstOrDefault(s => s.Name == "Linear");
        series?.IsVisible = value;
    }

    partial void OnPolynomialVisibleChanged(bool value)
    {
        var series = Series.FirstOrDefault(s => s.Name == "Polynomial");
        series?.IsVisible = value;
    }

    [RelayCommand]
    private void Import()
    {
        double[] xdata = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        double[] ydata = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512];

        var (intercept, slope) = Fit.Line(xdata, ydata);

        var coefficients = Fit.Polynomial(xdata, ydata, xdata.Length - 1);

        var points = xdata.Zip(ydata, (x, y) => new ObservablePoint(x, y)).ToArray();
        var linear = xdata.Select(x => new ObservablePoint(x, intercept + slope * x)).ToArray();
        var polynomial = xdata
            .Select(x => new ObservablePoint(x, coefficients.Select((t, i) => t * Math.Pow(x, i)).Sum())).ToArray();

        Series.Clear();

        Series.Add(new LineSeries<ObservablePoint>
        {
            Name = "Data",
            Fill = null,
            LineSmoothness = 0.0,
            Values = points
        });

        Series.Add(new LineSeries<ObservablePoint>
        {
            Name = "Linear",
            Fill = null,
            LineSmoothness = 0.0,
            Values = linear,
            IsVisible = LinearVisible // Sync with current checkbox state on import
        });

        Series.Add(new LineSeries<ObservablePoint>
        {
            Name = "Polynomial",
            Fill = null,
            LineSmoothness = 1.0,
            Values = polynomial,
            IsVisible = PolynomialVisible // Sync with current checkbox state on import
        });
    }
}