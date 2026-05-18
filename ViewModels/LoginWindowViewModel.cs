using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class LoginWindowViewModel(
    ISessionService sessionService,
    IDatabaseService databaseService,
    IWindowService windowService,
    IRecentDatasetsService recentDatasetsService) : ViewModelBase
{
    [ObservableProperty] public partial string Username { get; set; } = "";

    [ObservableProperty] public partial string Password { get; set; } = "";

    [ObservableProperty] public partial string? LoginStatus { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AttemptLogin()
    {
        LoginStatus = "";
        var nextStatus = "";
        // Goldilocks Delay
        var minimumDelay = Task.Delay(1000);

        try
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                nextStatus = "Enter your student ID.";
                return;
            }

            if (string.IsNullOrEmpty(Password))
            {
                nextStatus = "Enter your password.";
                return;
            }

            var id = Username.Trim();

            var profile = await Task.Run(() => databaseService.GetProfileByUsername(id));
            if (profile is null)
            {
                nextStatus = "Unknown student ID.";
                return;
            }

            var isCorrect = await Task.Run(() => !BCrypt.Net.BCrypt.EnhancedVerify(Password, profile.PasswordHash));
            if (isCorrect)
            {
                nextStatus = "Incorrect password.";
                return;
            }

            sessionService.CurrentUser = profile;
            windowService.CreateAndShowWindow(new DashboardWindowViewModel(
                recentDatasetsService,
                databaseService,
                sessionService,
                windowService));

            windowService.FindWindowFromDataModel(this)?.Close();
        }
        finally
        {
            await minimumDelay;
            LoginStatus = nextStatus;
        }
    }

    [RelayCommand]
    private void OpenSignupWindow()
    {
        var window = windowService.FindWindowFromDataModel(typeof(SignUpWindowViewModel));
        if (window is not null)
            window.Activate();
        else
            windowService.CreateAndShowWindow(new SignUpWindowViewModel(sessionService, databaseService,
                windowService));
    }
}