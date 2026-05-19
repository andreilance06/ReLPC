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
    // Feature: services and state used by dataset navigation, persistence, export, and logout.
    private readonly IRecentDatasetsService _recentDatasets;
    private readonly IExportService _exportService;
    private bool _suppressDatasetSelection;
    private bool _loadingDataset;

    // Feature: collapsed/expanded side menu visibility.
    [ObservableProperty] public partial bool IsMenuExpanded { get; set; }

    // Feature: dataset explorer panel visibility.
    [ObservableProperty] public partial bool IsDatasetPanelVisible { get; set; } = true;

    // Feature: dataset name editor and selected dataset binding.
    [ObservableProperty] public partial string DatasetName { get; set; } = string.Empty;

    [ObservableProperty] public partial DatasetRecord? SelectedDataset { get; set; }

    public ObservableCollection<DatasetRecord> UserDatasets { get; } = [];

    public Window? HostWindow { get; set; }

    private int _currentDatasetId;

    public bool IsResettingDataset { get; private set; }

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

    // Feature: show/hide the dataset explorer panel from the side menu.
    [RelayCommand]
    private void ToggleDatasetPanel() => IsDatasetPanelVisible = !IsDatasetPanelVisible;

    // Feature: return from the workspace to the dashboard.
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
        // Feature: create a saved dataset and open it in the workspace.
        var userId = _sessionService.CurrentUser?.Id ?? 0;
        var dataset = _databaseService.CreateDataset(userId,
            $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}");
        LoadDataset(dataset);
    }

    [RelayCommand]
    private async Task PickAndLoadDatasetAsync()
    {
        // Feature: open the dataset picker dialog and load the selected dataset.
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
    private async Task DeleteCurrentDatasetAsync()
    {
        // Feature: confirm and delete the currently selected dataset.
        if (_currentDatasetId == 0)
        {
            if (HostWindow is not null)
                await MessageWindowView.ShowAsync(HostWindow, "Nothing to delete",
                    "There is no saved dataset selected.");
            return;
        }

        if (HostWindow is not null)
        {
            var confirm = new ConfirmDeleteDatasetWindowView();
            var shouldDelete = await confirm.ShowDialog<bool>(HostWindow);
            if (!shouldDelete)
                return;
        }

        var userId = _sessionService.CurrentUser?.Id ?? 0;
        _databaseService.DeleteDataset(_currentDatasetId, userId);
        ClearCurrentDataset();
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        // Feature: export the current dataset, points, and calculation output as a PDF.
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
            byte[] chartPng = RenderChartToPng();
            var pdfBytes = _exportService.ExportDatasetToPdf(record, chartPng);

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
        // Feature: load dataset metadata, points, saved calculations, and recent history.
        _loadingDataset = true;
        _suppressHistory = true;
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
            _suppressHistory = false;
            _loadingDataset = false;
        }

        RebuildLastValues();
        ResetHistory();
    }

    private void RefreshUserDatasetsList()
    {
        // Part: refresh the dataset explorer list for the current user.
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
        // Feature: persist dataset name, points, and calculation output.
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

    private void ClearCurrentDataset()
    {
        // Part: reset the workspace after deleting a dataset.
        _loadingDataset = true;
        _suppressHistory = true;
        IsResettingDataset = true;
        try
        {
            _currentDatasetId = 0;
            DatasetName = string.Empty;
            Inputs.Clear();
            TidyRows();
            Equation = "No Calculation Yet";
            Coefficient = "No Calculation Yet";
            Intermediates = "No Calculation Yet";
            Prediction = "No Calculation Yet";
            PredictionXText = string.Empty;
            RefreshUserDatasetsList();
        }
        finally
        {
            IsResettingDataset = false;
            _suppressHistory = false;
            _loadingDataset = false;
        }

        RebuildLastValues();
        ResetHistory();
    }

    private void AutoSaveCurrentDataset()
    {
        // Feature: save automatically after data or calculation changes.
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
        // Feature: confirm logout and return to the login screen.
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