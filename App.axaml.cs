using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReLPC.Services;
using ReLPC.ViewModels;
using ReLPC.Views;

namespace ReLPC;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var sessionService = new SessionService();
            var databaseService = new LiteDBService();
            var windowService = new WindowService();
            desktop.MainWindow = new LoginWindowView()
                { DataContext = new LoginWindowViewModel(sessionService, databaseService, windowService) };
        }

        base.OnFrameworkInitializationCompleted();
    }
}