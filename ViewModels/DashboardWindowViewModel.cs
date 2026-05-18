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
        var userId = _session.CurrentUser?.Id ?? 0;
        var ownerCaption = OwnerCaption;
        var ordered = BuildOrderedDatasetList(userId);

        FeaturedDatasets.Clear();
        MoreDatasets.Clear();

        foreach (var dataset in ordered.Take(3))
            FeaturedDatasets.Add(CreateItem(dataset, ownerCaption));

        foreach (var dataset in ordered.Skip(3))
            MoreDatasets.Add(CreateItem(dataset, ownerCaption));

        OnPropertyChanged(nameof(HasFeaturedDatasets));
        OnPropertyChanged(nameof(HasMoreDatasets));
        OnPropertyChanged(nameof(HasRecentDatasets));
        OnPropertyChanged(nameof(WelcomeName));
        OnPropertyChanged(nameof(OwnerCaption));
    }

    [RelayCommand]
    private void CreateNewDataset()
    {
        var userId = _session.CurrentUser?.Id ?? 0;
        var dataset = _database.CreateDataset(userId, $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}");
        OpenDataset(dataset);
    }

    public Window? HostWindow { get; set; }

    [RelayCommand]
    private async Task LoadDatasetAsync()
    {
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
        if (item?.Dataset is null)
            return;

        OpenDataset(item.Dataset);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
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
