using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Models;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class SignUpWindowViewModel(
    ISessionService sessionService,
    IDatabaseService databaseService,
    IWindowService windowService) : ViewModelBase
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? PasswordConfirmation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar1))]
    public partial bool HidePassword1 { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar2))]
    public partial bool HidePassword2 { get; set; } = true;

    public string PasswordChar1 => HidePassword1 ? "\u25cf" : string.Empty;

    public string PasswordChar2 => HidePassword2 ? "\u25cf" : string.Empty;

    [RelayCommand]
    private void AttemptSignup()
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) ||
            string.IsNullOrEmpty(PasswordConfirmation))
            return;

        if (!Username.All(c => char.IsDigit(c) || c == '-'))
            return;

        if (Password != PasswordConfirmation)
            return;

        var profile = new UserProfile
        {
            Username = Username,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Password),
        };

        databaseService.UpsertUser(profile);

        windowService.FindWindowFromDataModel(this)?.Close();
    }
}