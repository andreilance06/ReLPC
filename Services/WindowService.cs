using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ReLPC.Services;

public interface IWindowService
{
    void CreateAndShowWindow(object viewModel);
    void ShowAsMainWindow(Window window);
    Window? FindWindowFromDataModel(object viewModel);
    Window? FindWindowFromDataModel(Type t);
}

public class WindowService : IWindowService
{
    public void CreateAndShowWindow(object viewModel)
    {
        var viewLocator = new ViewLocator();

        var view = viewLocator.Build(viewModel);

        if (view is not Window window)
            throw new Exception();

        window.DataContext = viewModel;
        ShowAsMainWindow(window);
    }

    public void ShowAsMainWindow(Window window)
    {
        DesktopSession.ShowAsMainWindow(window);
    }

    public Window? FindWindowFromDataModel(object viewModel)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.Windows.FirstOrDefault(w => w.DataContext == viewModel);
        return window;
    }

    public Window? FindWindowFromDataModel(Type t)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.Windows.FirstOrDefault(w => w.DataContext?.GetType() == t);
        return window;
    }
}