using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Models;
using ReLPC.Services;
using ReLPC.Views;

namespace ReLPC.ViewModels;

public partial class DashboardWindowViewModel : ViewModelBase
{
    // Feature: services used by dashboard actions and recent dataset loading.
    private readonly IRecentDatasetsService _recentDatasets;
    private readonly IDatabaseService _database;
    private readonly ISessionService _session;
    private readonly IWindowService _windowService;

    public DashboardWindowViewModel(
        IRecentDatasetsService recentDatasets,
        IDatabaseService database,
        ISessionService session,
        IWindowService windowService)
    {
        _recentDatasets = recentDatasets;
        _database = database;
        _session = session;
        _windowService = windowService;
    }

    // Feature: recent dataset collections shown in the dashboard.
    public ObservableCollection<DashboardDatasetItem> FeaturedDatasets { get; } = [];
    public ObservableCollection<DashboardDatasetItem> MoreDatasets { get; } = [];

    public string WelcomeName =>
        string.IsNullOrWhiteSpace(_session.CurrentUser?.Username)
            ? "USER"
            : _session.CurrentUser.Username.ToUpperInvariant();

    public string OwnerCaption =>
        string.IsNullOrWhiteSpace(_session.CurrentUser?.Username)
            ? "User >>"
            : $"{_session.CurrentUser.Username} >>";

    public bool HasFeaturedDatasets => FeaturedDatasets.Count > 0;
    public bool HasMoreDatasets => MoreDatasets.Count > 0;
    public bool HasRecentDatasets => HasFeaturedDatasets || HasMoreDatasets;

    public void RefreshRecentDatasets()
    {
        // Feature: refresh recent datasets and split them into tile/list sections.
        var userId = _session.CurrentUser?.Id ?? 0;
        var ownerCaption = OwnerCaption;

        FeaturedDatasets.Clear();
        MoreDatasets.Clear();

        try
        {
            var ordered = BuildOrderedDatasetList(userId);

            foreach (var dataset in ordered.Take(3))
                FeaturedDatasets.Add(CreateItem(dataset, ownerCaption));

            foreach (var dataset in ordered.Skip(3))
                MoreDatasets.Add(CreateItem(dataset, ownerCaption));
        }
        catch
        {
            FeaturedDatasets.Clear();
            MoreDatasets.Clear();
        }

        OnPropertyChanged(nameof(HasFeaturedDatasets));
        OnPropertyChanged(nameof(HasMoreDatasets));
        OnPropertyChanged(nameof(HasRecentDatasets));
        OnPropertyChanged(nameof(WelcomeName));
        OnPropertyChanged(nameof(OwnerCaption));
    }

    [RelayCommand]
    private void CreateNewDataset()
    {
        // Feature: dashboard command to create and open a new dataset.
        var userId = _session.CurrentUser?.Id ?? 0;
        var dataset = _database.CreateDataset(userId, $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}");
        OpenDataset(dataset);
    }

    public Window? HostWindow { get; set; }

    [RelayCommand]
    private async Task LoadDatasetAsync()
    {
        // Feature: dashboard command to choose and open an existing dataset.
        if (HostWindow is null)
            return;

        var userId = _session.CurrentUser?.Id ?? 0;
        var datasets = _database.GetDatasets(userId);
        var picker = new DatasetPickerWindowView(datasets);
        var dataset = await picker.ShowDialog<DatasetRecord?>(HostWindow);
        if (dataset is not null)
            OpenDataset(dataset);
    }

    [RelayCommand]
    private void OpenRecentDataset(DashboardDatasetItem? item)
    {
        // Feature: open a dataset selected from the recent dataset browser.
        if (item?.Dataset is null)
            return;

        OpenDataset(item.Dataset);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // Feature: dashboard logout confirmation and return to login.
        if (HostWindow is null)
            return;

        var confirm = new ConfirmLogoutWindowView();
        var shouldLogout = await confirm.ShowDialog<bool>(HostWindow);
        if (!shouldLogout)
            return;

        _session.CurrentUser = null;
        _windowService.CreateAndShowWindow(new LoginWindowViewModel(
            _session,
            _database,
            _windowService,
            _recentDatasets));
        HostWindow.Close();
    }

    private void OpenDataset(DatasetRecord dataset)
    {
        // Part: shared dashboard navigation into the main workspace.
        var userId = _session.CurrentUser?.Id ?? 0;
        _recentDatasets.RecordOpened(userId, dataset.Id);

        var fresh = _database.GetDataset(dataset.Id) ?? dataset;
        _windowService.CreateAndShowWindow(new MainWindowViewModel(
            _session,
            _database,
            _windowService,
            _recentDatasets,
            new ExportService(),
            fresh));

        _windowService.FindWindowFromDataModel(this)?.Close();
    }

    private List<DatasetRecord> BuildOrderedDatasetList(int userId)
    {
        // Part: combine recent history with all saved datasets without duplicates.
        var recent = _recentDatasets.GetRecentDatasets(userId);
        var allFromDb = _database.GetDatasets(userId);
        var ordered = new List<DatasetRecord>();
        var seen = new HashSet<int>();

        foreach (var dataset in recent)
        {
            if (seen.Add(dataset.Id))
                ordered.Add(dataset);
        }

        foreach (var dataset in allFromDb)
        {
            if (seen.Add(dataset.Id))
                ordered.Add(dataset);
        }

        return ordered.Take(20).ToList();
    }

    private static DashboardDatasetItem CreateItem(DatasetRecord dataset, string ownerCaption) =>
        new()
        {
            Dataset = dataset,
            OwnerCaption = ownerCaption
        };
}
