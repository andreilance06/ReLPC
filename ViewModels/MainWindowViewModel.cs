using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using MathNet.Numerics;
using ReLPC.Models;
using ReLPC.Services;
using SkiaSharp;

namespace ReLPC.ViewModels;

public enum RegressionMode
{
    Linear = 0,
    Polynomial = 1,
    Both = 2
}

public partial class MainWindowViewModel : ViewModelBase
{
    // Feature: data entry grid rows shown in the main workspace.
    public ObservableCollection<Point> Inputs { get; } = [];
    public ObservableCollection<Point> Outputs { get; } = [];

    // Feature: live chart series shown in the results panel.
    public ObservableCollection<ISeries> Series { get; } = [];

    private readonly ISessionService _sessionService;
    private readonly IDatabaseService _databaseService;
    private readonly IWindowService _windowService;

    // Feature: calculation result text shown in the results panel.
    [ObservableProperty] public partial string Equation { get; set; }
    [ObservableProperty] public partial string Coefficient { get; set; }
    [ObservableProperty] public partial string Intermediates { get; set; }
    [ObservableProperty] public partial string Prediction { get; set; }

    // Feature: regression settings and prediction input bindings.
    [NotifyPropertyChangedFor(nameof(ShowDegreeInput))]
    [ObservableProperty]
    public partial int SelectedRegressionIndex { get; set; }

    [ObservableProperty] public partial string DegreeText { get; set; }
    [ObservableProperty] public partial string PredictionXText { get; set; }

    public bool ShowDegreeInput => SelectedRegressionIndex is 1 or 2;

    private double[]? _linearCoeffs;
    private double[]? _polyCoeffs;
    private int _polyDegree;

    public MainWindowViewModel(
        ISessionService sessionService,
        IDatabaseService databaseService,
        IWindowService windowService,
        IRecentDatasetsService recentDatasets,
        IExportService exportService,
        DatasetRecord? initialDataset = null)
    {
        _sessionService = sessionService;
        _databaseService = databaseService;
        _windowService = windowService;
        _recentDatasets = recentDatasets;
        _exportService = exportService;
        Inputs.CollectionChanged += OnInputsCollectionChanged;

        DegreeText = "2";
        PredictionXText = "";
        Equation = "No Calculation Yet";
        Coefficient = "No Calculation Yet";
        Intermediates = "No Calculation Yet";
        Prediction = "No Calculation Yet";

        _suppressHistory = true;
        try
        {
            if (initialDataset is not null)
                LoadDataset(initialDataset);
            else
                TidyRows();
        }
        finally
        {
            _suppressHistory = false;
        }

        RebuildLastValues();
        ResetHistory();
        RefreshUserDatasetsList();
    }

    public byte[] RenderChartToPng(int width = 800, int height = 450)
    {
        var chart = new SKCartesianChart
        {
            Width = width,
            Height = height,
            Series = Series.ToArray(),
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            Background = SKColors.White,
        };

        using var image = chart.GetImage();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void TidyRows()
    {
        // Part: keep one blank row available at the end of the data entry grid.
        if (Inputs.Count == 0)
        {
            Inputs.Add(new Point());
            return;
        }

        while (Inputs.Count > 1 && Inputs[^1].IsEmpty && Inputs[^2].IsEmpty)
            Inputs.RemoveAt(Inputs.Count - 1);

        if (!Inputs[^1].IsEmpty)
            Inputs.Add(new Point());
    }

    public void DatasetChanged()
    {
        TidyRows();
        Calculate();
        AutoSaveCurrentDataset();
        FlushPendingChanges();
    }

    partial void OnSelectedRegressionIndexChanged(int value) => RecalculateAndSave();
    partial void OnDegreeTextChanged(string value) => RecalculateAndSave();
    partial void OnPredictionXTextChanged(string value) => UpdatePrediction();

    private void RecalculateAndSave()
    {
        Calculate();
        AutoSaveCurrentDataset();
    }

    private void OnInputsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            int idx = e.OldStartingIndex;
            foreach (Point point in e.OldItems)
            {
                point.PropertyChanged -= OnInputPointPropertyChanged;
                if (!_suppressHistory)
                {
                    RecordRowRemove(idx, point);
                    _lastValues.Remove(point);
                }

                idx++;
            }
        }

        if (e.NewItems is not null)
        {
            int idx = e.NewStartingIndex;
            foreach (Point point in e.NewItems)
            {
                point.PropertyChanged += OnInputPointPropertyChanged;
                if (!_suppressHistory)
                {
                    RecordRowInsert(idx, point);
                    _lastValues[point] = (point.X, point.Y);
                }

                idx++;
            }
        }
    }

    private void OnInputPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressHistory) return;
        if (e.PropertyName is not (nameof(Point.X) or nameof(Point.Y))) return;

        var point = (Point)sender!;
        RecordCellChange(point, e.PropertyName);
        _lastValues[point] = (point.X, point.Y);
        DatasetChanged();
    }

    private void Calculate()
    {
        // Feature: read valid data rows, run the selected regression mode, and update result text.
        _linearCoeffs = null;
        _polyCoeffs = null;

        var pts = Inputs
            .Where(p => p is { X: not null, Y: not null })
            .Select(p => (X: p.X!.Value, Y: p.Y!.Value))
            .ToList();

        if (pts.Count < 2)
        {
            Equation = "Enter at least two valid X/Y rows";
            Coefficient = "-";
            Intermediates = "-";
            UpdatePrediction();
            UpdateChart(pts);
            return;
        }

        var xs = pts.Select(p => p.X).ToArray();
        var ys = pts.Select(p => p.Y).ToArray();
        var mode = (RegressionMode)SelectedRegressionIndex;

        var eqSb = new StringBuilder();
        var coeffSb = new StringBuilder();
        var intSb = new StringBuilder();

        if (mode is RegressionMode.Linear or RegressionMode.Both)
            BuildLinear(xs, ys, eqSb, coeffSb, intSb);

        if (mode is RegressionMode.Polynomial or RegressionMode.Both)
            BuildPolynomial(xs, ys, eqSb, coeffSb, intSb);

        Equation = eqSb.Length > 0 ? eqSb.ToString().TrimEnd() : "No Calculation Yet";
        Coefficient = coeffSb.Length > 0 ? coeffSb.ToString().TrimEnd() : "-";
        Intermediates = intSb.Length > 0 ? intSb.ToString().TrimEnd() : "-";

        UpdatePrediction();
        UpdateChart(pts);

        if (_currentDatasetId > 0 && !_loadingDataset)
        {
            var userId = _sessionService.CurrentUser?.Id ?? 0;
            var dataset = _databaseService.GetDataset(_currentDatasetId);
            if (dataset is not null && dataset.UserId == userId)
            {
                dataset.Equation = Equation;
                dataset.Coefficient = Coefficient;
                dataset.IntermediateComputations = Intermediates;
            }
        }
    }

    private void BuildLinear(double[] xs, double[] ys, StringBuilder eq, StringBuilder co, StringBuilder it)
    {
        // Feature: linear regression calculation and display text.
        // Fit.Line returns (a, b) such that y = a + b·x
        var (a, b) = Fit.Line(xs, ys);
        _linearCoeffs = [a, b];

        var modelled = xs.Select(x => a + b * x);
        var r2 = GoodnessOfFit.RSquared(modelled, ys);

        var sign = a < 0 ? "−" : "+";
        eq.AppendLine($"Linear:  y = {Fmt(b)}x {sign} {Fmt(Math.Abs(a))}");

        co.AppendLine("Linear:");
        co.AppendLine($"  slope (m)     = {Fmt(b)}");
        co.AppendLine($"  intercept (b) = {Fmt(a)}");
        co.AppendLine($"  R²            = {Fmt(r2)}");

        int n = xs.Length;
        double sumX = xs.Sum();
        double sumY = ys.Sum();
        double sumXY = xs.Zip(ys, (x, y) => x * y).Sum();
        double sumX2 = xs.Select(x => x * x).Sum();
        double meanX = sumX / n;
        double meanY = sumY / n;

        it.AppendLine("Linear intermediates:");
        it.AppendLine($"  n     = {n}");
        it.AppendLine($"  Σx    = {Fmt(sumX)}");
        it.AppendLine($"  Σy    = {Fmt(sumY)}");
        it.AppendLine($"  Σxy   = {Fmt(sumXY)}");
        it.AppendLine($"  Σx²   = {Fmt(sumX2)}");
        it.AppendLine($"  x̄     = {Fmt(meanX)}");
        it.AppendLine($"  ȳ     = {Fmt(meanY)}");
        it.AppendLine($"  m = (nΣxy − ΣxΣy) / (nΣx² − (Σx)²) = {Fmt(b)}");
        it.AppendLine($"  b = ȳ − m·x̄ = {Fmt(a)}");
    }

    private void BuildPolynomial(double[] xs, double[] ys, StringBuilder eq, StringBuilder co, StringBuilder it)
    {
        // Feature: polynomial regression calculation and display text.
        if (eq.Length > 0) eq.AppendLine();
        if (co.Length > 0) co.AppendLine();
        if (it.Length > 0) it.AppendLine();

        if (!int.TryParse(DegreeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int degree) || degree < 1)
        {
            eq.AppendLine("Polynomial: invalid degree (must be an integer ≥ 1)");
            co.AppendLine("Polynomial: -");
            it.AppendLine("Polynomial: -");
            return;
        }

        if (xs.Length <= degree)
        {
            eq.AppendLine($"Polynomial: need at least {degree + 1} valid rows for degree {degree}");
            co.AppendLine("Polynomial: -");
            it.AppendLine("Polynomial: -");
            return;
        }

        // Fit.Polynomial(x, y, order) → [c0, c1, ..., cn] with y ≈ c0 + c1x + … + cnxⁿ
        var coeffs = Fit.Polynomial(xs, ys, degree);
        _polyCoeffs = coeffs;
        _polyDegree = degree;

        var modelled = xs.Select(x => EvaluatePolynomial(coeffs, x));
        var r2 = GoodnessOfFit.RSquared(modelled, ys);

        eq.AppendLine($"Polynomial (degree {degree}):  {BuildPolynomialString(coeffs)}");

        co.AppendLine($"Polynomial (degree {degree}):");
        for (int i = 0; i < coeffs.Length; i++)
            co.AppendLine($"  c{ToSubscript(i)} = {Fmt(coeffs[i])}");
        co.AppendLine($"  R² = {Fmt(r2)}");

        it.AppendLine($"Polynomial intermediates (normal equations):");
        it.AppendLine($"  n = {xs.Length}");
        for (int k = 1; k <= 2 * degree; k++)
        {
            var sk = xs.Select(x => Math.Pow(x, k)).Sum();
            it.AppendLine($"  Σx{ToSuperscript(k)} = {Fmt(sk)}");
        }

        for (int k = 0; k <= degree; k++)
        {
            var sk = xs.Zip(ys, (x, y) => Math.Pow(x, k) * y).Sum();
            it.AppendLine($"  Σx{ToSuperscript(k)}·y = {Fmt(sk)}");
        }
    }

    private void UpdatePrediction()
    {
        // Feature: prediction output for the user-entered X value.
        var mode = (RegressionMode)SelectedRegressionIndex;
        bool hasLinear = _linearCoeffs is not null && mode is RegressionMode.Linear or RegressionMode.Both;
        bool hasPoly = _polyCoeffs is not null && mode is RegressionMode.Polynomial or RegressionMode.Both;

        if (!hasLinear && !hasPoly)
        {
            Prediction = "No model available";
            return;
        }

        if (string.IsNullOrWhiteSpace(PredictionXText))
        {
            Prediction = "Enter an X value to predict";
            return;
        }

        if (!double.TryParse(PredictionXText, NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
        {
            Prediction = "Invalid X value";
            return;
        }

        var sb = new StringBuilder();
        if (hasLinear)
        {
            var a = _linearCoeffs![0];
            var b = _linearCoeffs[1];
            sb.AppendLine($"Linear:      y({Fmt(x)}) = {Fmt(a + b * x)}");
        }

        if (hasPoly)
        {
            sb.AppendLine($"Polynomial:  y({Fmt(x)}) = {Fmt(EvaluatePolynomial(_polyCoeffs!, x))}");
        }

        Prediction = sb.ToString().TrimEnd();
    }

    private void UpdateChart(System.Collections.Generic.List<(double X, double Y)> pts)
    {
        Series.Clear();

        if (pts.Count == 0) return;

        Series.Add(new ScatterSeries<ObservablePoint>
        {
            Name = "Data",
            Values = pts.Select(p => new ObservablePoint(p.X, p.Y)).ToArray(),
            GeometrySize = 10,
            Fill = new SolidColorPaint(new SKColor(0xFF, 0x31, 0x31)),
            Stroke = null,
            YToolTipLabelFormatter = point => $"Y = {point.Model?.Y:0.####}"
        });

        if (pts.Count < 2) return;

        var mode = (RegressionMode)SelectedRegressionIndex;
        double minX = pts.Min(p => p.X);
        double maxX = pts.Max(p => p.X);
        if (maxX <= minX) maxX = minX + 1;

        if (_linearCoeffs is not null && mode is RegressionMode.Linear or RegressionMode.Both)
        {
            var a = _linearCoeffs[0];
            var b = _linearCoeffs[1];
            Series.Add(new LineSeries<ObservablePoint>
            {
                Name = "Linear",
                Values =
                [
                    new ObservablePoint(minX, a + b * minX),
                    new ObservablePoint(maxX, a + b * maxX)
                ],
                Fill = null,
                GeometrySize = 10,
                Stroke = new SolidColorPaint(new SKColor(0x17, 0x9E, 0xFF)) { StrokeThickness = 2 },
            });
        }

        if (_polyCoeffs is not null && mode is RegressionMode.Polynomial or RegressionMode.Both)
        {
            const int samples = 120;
            var pointsOut = new ObservablePoint[samples];
            double step = (maxX - minX) / (samples - 1);
            for (int i = 0; i < samples; i++)
            {
                double x = minX + step * i;
                pointsOut[i] = new ObservablePoint(x, EvaluatePolynomial(_polyCoeffs, x));
            }

            Series.Add(new LineSeries<ObservablePoint>
            {
                Name = "Polynomial",
                Values = pointsOut,
                Fill = null,
                GeometrySize = 10,
                LineSmoothness = 1,
                Stroke = new SolidColorPaint(new SKColor(0x2E, 0xC4, 0x6B)) { StrokeThickness = 2 },
            });
        }
    }

    private static double EvaluatePolynomial(double[] c, double x)
    {
        double result = 0;
        double xp = 1;
        foreach (var coeff in c)
        {
            result += coeff * xp;
            xp *= x;
        }

        return result;
    }

    private static string BuildPolynomialString(double[] c)
    {
        var sb = new StringBuilder("y = ");
        bool first = true;
        for (int i = c.Length - 1; i >= 0; i--)
        {
            double coeff = c[i];
            if (coeff == 0 && i != 0 && !first) continue;

            string sign;
            if (first)
            {
                sign = coeff < 0 ? "−" : "";
                first = false;
            }
            else
            {
                sign = coeff < 0 ? " − " : " + ";
            }

            string absVal = Fmt(Math.Abs(coeff));
            string term = i switch
            {
                0 => absVal,
                1 => $"{absVal}x",
                _ => $"{absVal}x{ToSuperscript(i)}"
            };
            sb.Append(sign).Append(term);
        }

        return sb.ToString();
    }

    private static string ToSuperscript(int value) => ConvertDigits(value, "⁰¹²³⁴⁵⁶⁷⁸⁹");

    private static string ToSubscript(int value) => ConvertDigits(value, "₀₁₂₃₄₅₆₇₈₉");

    private static string ConvertDigits(int value, string digits)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var chars = text.Select(ch => char.IsDigit(ch) ? digits[ch - '0'] : ch).ToArray();
        return new string(chars);
    }

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}