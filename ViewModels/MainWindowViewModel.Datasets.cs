using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Models;
using ReLPC.Services;
using ReLPC.Views;

namespace ReLPC.ViewModels;

public partial class MainWindowViewModel
{
    private readonly IRecentDatasetsService _recentDatasets;
    private readonly IExportService _exportService;
    private bool _suppressDatasetSelection;
    private bool _loadingDataset;

    [ObservableProperty] public partial bool IsMenuExpanded { get; set; }

    [ObservableProperty] public partial bool IsDatasetPanelVisible { get; set; } = true;

    [ObservableProperty] public partial string DatasetName { get; set; } = string.Empty;

    [ObservableProperty] public partial DatasetRecord? SelectedDataset { get; set; }

    public ObservableCollection<DatasetRecord> UserDatasets { get; } = [];

    public Window? HostWindow { get; set; }

    private int _currentDatasetId;

    partial void OnSelectedDatasetChanged(DatasetRecord? value)
    {
        if (_suppressDatasetSelection || value is null || value.Id == _currentDatasetId)
            return;

        LoadDataset(value);
    }

    partial void OnDatasetNameChanged(string value)
    {
        if (_loadingDataset)
            return;

        PersistDatasetNameOnly();
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuExpanded = !IsMenuExpanded;

    [RelayCommand]
    private void ToggleDatasetPanel() => IsDatasetPanelVisible = !IsDatasetPanelVisible;

    [RelayCommand]
    private void GoToDashboard()
    {
        _windowService.CreateAndShowWindow(new DashboardWindowViewModel(
            _recentDatasets,
            _databaseService,
            _sessionService,
            _windowService));
        _windowService.FindWindowFromDataModel(this)?.Close();
    }

    [RelayCommand]
    private void CreateNewDataset()
    {
        var userId = _sessionService.CurrentUser?.Id ?? 0;
        var dataset = _databaseService.CreateDataset(userId,
            $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}");
        LoadDataset(dataset);
    }

    [RelayCommand]
    private async Task PickAndLoadDatasetAsync()
    {
        if (HostWindow is null)
            return;

        var userId = _sessionService.CurrentUser?.Id ?? 0;
        var picker = new DatasetPickerWindowView(_databaseService.GetDatasets(userId));
        var dataset = await picker.ShowDialog<DatasetRecord?>(HostWindow);
        if (dataset is not null)
            LoadDataset(dataset);
    }

    [RelayCommand]
    private void SaveDataset() => SaveCurrentDataset();

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (HostWindow is null)
            return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(HostWindow);
            if (topLevel?.StorageProvider.CanSave != true)
            {
                await MessageWindowView.ShowAsync(HostWindow, "Export unavailable",
                    "Saving files is not supported on this platform.");
                return;
            }

            var safeName = SanitizeFileName(DatasetName.Trim());
            if (string.IsNullOrEmpty(safeName))
                safeName = "dataset";

            var options = new FilePickerSaveOptions
            {
                Title = "Export Analysis Report",
                SuggestedFileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                DefaultExtension = "pdf",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF")
                    {
                        Patterns = ["*.pdf"],
                        MimeTypes = ["application/pdf"]
                    }
                ]
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file is null)
                return;

            var record = BuildDatasetRecordForExport();
            var pdfBytes = _exportService.ExportDatasetToPdf(record);

            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(pdfBytes);
                await stream.FlushAsync();
            }

            await MessageWindowView.ShowAsync(
                HostWindow,
                "Export complete",
                $"Your analysis report was saved as:\n{file.Name}");
        }
        catch (Exception ex)
        {
            await MessageWindowView.ShowAsync(
                HostWindow,
                "Export failed",
                $"Could not create the PDF report.\n\n{ex.Message}");
        }
    }

    public void LoadDataset(DatasetRecord dataset)
    {
        _loadingDataset = true;
        try
        {
            _currentDatasetId = dataset.Id;
            DatasetName = dataset.Name ?? string.Empty;

            var userId = _sessionService.CurrentUser?.Id ?? 0;
            _recentDatasets.RecordOpened(userId, dataset.Id);

            Inputs.Clear();
            foreach (var point in dataset.Points ?? [])
            {
                Inputs.Add(new Point(ParseCell(point.X), ParseCell(point.Y)));
            }

            if (Inputs.Count == 0)
            {
                for (var i = 0; i < 3; i++)
                    Inputs.Add(new Point());
            }

            TidyRows();

            Equation = dataset.Equation ?? "No Calculation Yet";
            Coefficient = dataset.Coefficient ?? "No Calculation Yet";
            Intermediates = dataset.IntermediateComputations ?? "No Calculation Yet";

            RefreshUserDatasetsList();
            Calculate();
        }
        finally
        {
            _loadingDataset = false;
        }
    }

    private void RefreshUserDatasetsList()
    {
        _suppressDatasetSelection = true;
        try
        {
            var userId = _sessionService.CurrentUser?.Id ?? 0;
            UserDatasets.Clear();
            foreach (var record in _databaseService.GetDatasets(userId))
                UserDatasets.Add(record);

            SelectedDataset = UserDatasets.FirstOrDefault(d => d.Id == _currentDatasetId);
        }
        finally
        {
            _suppressDatasetSelection = false;
        }
    }

    private void SaveCurrentDataset()
    {
        var userId = _sessionService.CurrentUser?.Id ?? 0;

        if (_currentDatasetId == 0)
        {
            var trimmed = DatasetName.Trim();
            var name = string.IsNullOrEmpty(trimmed)
                ? $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}"
                : trimmed;
            var created = _databaseService.CreateDataset(userId, name);
            _currentDatasetId = created.Id;
            DatasetName = created.Name;
        }

        var dataset = _databaseService.GetDataset(_currentDatasetId);
        if (dataset is null || dataset.UserId != userId)
            return;

        var nameTrimmed = DatasetName.Trim();
        if (!string.IsNullOrEmpty(nameTrimmed))
            dataset.Name = nameTrimmed;

        dataset.Points = Inputs
            .Select(point => new DatasetPointRecord
            {
                X = FormatCell(point.X),
                Y = FormatCell(point.Y)
            })
            .ToList();
        dataset.Equation = Equation;
        dataset.Coefficient = Coefficient;
        dataset.IntermediateComputations = Intermediates;

        _databaseService.UpsertDataset(dataset);
        RefreshUserDatasetsList();
    }

    private void AutoSaveCurrentDataset()
    {
        if (_loadingDataset)
            return;

        if (_currentDatasetId == 0 && !HasDatasetContent())
            return;

        SaveCurrentDataset();
    }

    private bool HasDatasetContent()
    {
        if (!string.IsNullOrWhiteSpace(DatasetName))
            return true;

        return Inputs.Any(point => point.X.HasValue || point.Y.HasValue);
    }

    private void PersistDatasetNameOnly()
    {
        if (_currentDatasetId == 0)
            return;

        var userId = _sessionService.CurrentUser?.Id ?? 0;
        var dataset = _databaseService.GetDataset(_currentDatasetId);
        if (dataset is null || dataset.UserId != userId)
            return;

        var trimmed = DatasetName.Trim();
        if (string.IsNullOrEmpty(trimmed) || dataset.Name == trimmed)
            return;

        dataset.Name = trimmed;
        _databaseService.UpsertDataset(dataset);
        RefreshUserDatasetsList();
    }

    private DatasetRecord BuildDatasetRecordForExport()
    {
        var userId = _sessionService.CurrentUser?.Id ?? 0;
        return new DatasetRecord
        {
            Id = _currentDatasetId,
            UserId = userId,
            Name = DatasetName.Trim(),
            Equation = Equation,
            Coefficient = Coefficient,
            IntermediateComputations = Intermediates,
            Points = Inputs
                .Select(point => new DatasetPointRecord
                {
                    X = FormatCell(point.X),
                    Y = FormatCell(point.Y)
                })
                .ToList()
        };
    }

    private static double? ParseCell(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

    private static string FormatCell(double? value) =>
        value.HasValue ? value.Value.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (HostWindow is null)
            return;

        var confirm = new ConfirmLogoutWindowView();
        var shouldLogout = await confirm.ShowDialog<bool>(HostWindow);
        if (!shouldLogout)
            return;

        _sessionService.CurrentUser = null;
        _windowService.CreateAndShowWindow(new LoginWindowViewModel(
            _sessionService,
            _databaseService,
            _windowService,
            _recentDatasets));
        _windowService.FindWindowFromDataModel(this)?.Close();
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}
