using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class LoginWindowViewModel(
    ISessionService sessionService,
    IDatabaseService databaseService,
    IWindowService windowService) : ViewModelBase
{
    [ObservableProperty] public partial string Username { get; set; } = "";

    [ObservableProperty] public partial string Password { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar))]
    public partial bool HidePassword { get; set; } = true;

    public string PasswordChar => HidePassword ? "\u25cf" : string.Empty;

    [RelayCommand]
    private void AttemptLogin()
    {
        var profile = databaseService.GetProfileByUsername(Username);
        if (profile is null) return;

        if (!BCrypt.Net.BCrypt.EnhancedVerify(Password, profile.PasswordHash)) return;

        sessionService.CurrentUser = profile;
        windowService.CreateAndShowWindow(new MainWindowViewModel(sessionService, databaseService, windowService));
        windowService.FindWindowFromDataModel(this)?.Close();
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