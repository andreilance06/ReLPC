using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ReLPC.Services;

public static class DesktopSession
{
    public static void ShowAsMainWindow(Window window)
    {
        window.Show();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = window;
    }
}
