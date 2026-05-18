using System;
using System.Linq;
using System.Threading.Tasks;
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

    [ObservableProperty] public partial string? SignupStatus { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AttemptSignup()
    {
        SignupStatus = "";
        var nextStatus = "";
        // Goldilocks Delay
        var minimumDelay = Task.Delay(1000);

        try
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) ||
                string.IsNullOrEmpty(PasswordConfirmation))
            {
                nextStatus = "Fill in all user details!";
                return;
            }

            if (!Username.All(c => char.IsDigit(c) || c == '-'))
            {
                nextStatus = "Invalid ID! Example ID: 12-3456-789";
                return;
            }

            if (Password != PasswordConfirmation)
            {
                nextStatus = "Passwords don't match!";
                return;
            }

            var id = Username.Trim();
            var existingProfile = await Task.Run(() => databaseService.GetProfileByUsername(id));
            if (existingProfile is not null)
            {
                nextStatus = "ID already registered!";
                return;
            }

            var profile = new UserProfile
            {
                Username = Username,
                PasswordHash = await Task.Run(() => BCrypt.Net.BCrypt.EnhancedHashPassword(Password)),
            };


            await Task.Run(() => databaseService.UpsertUser(profile));
            nextStatus = "Successfully signed up!";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            nextStatus = "An error occured while signing up!";
        }
        finally
        {
            await minimumDelay;
            SignupStatus = nextStatus;
        }
    }
}