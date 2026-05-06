using System;
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

    [ObservableProperty] public partial string? SignupStatus { get; set; }

    public string PasswordChar1 => HidePassword1 ? "\u25cf" : string.Empty;

    public string PasswordChar2 => HidePassword2 ? "\u25cf" : string.Empty;

    [RelayCommand]
    private void AttemptSignup()
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) ||
            string.IsNullOrEmpty(PasswordConfirmation))
        {
            SignupStatus = "Fill in all user details!";
            return;
        }

        if (!Username.All(c => char.IsDigit(c) || c == '-'))
        {
            SignupStatus = "Invalid ID! Example ID: 12-3456-789";
            return;
        }

        if (Password != PasswordConfirmation)
        {
            SignupStatus = "Passwords don't match!";
            return;
        }

        if (databaseService.GetProfileByUsername(Username) is not null)
        {
            SignupStatus = "ID already registered!";
            return;
        }

        var profile = new UserProfile
        {
            Username = Username,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Password),
        };

        try
        {
            databaseService.UpsertUser(profile);
            SignupStatus = "Successfully signed up!";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            SignupStatus = "An error occured while signing up!";
        }
    }
}