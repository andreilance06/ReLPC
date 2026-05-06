using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class MainWindowViewModel(
    ISessionService sessionService,
    IDatabaseService databaseService,
    IWindowService windowService) : ViewModelBase
{
}