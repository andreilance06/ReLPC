using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ReLPC.Services;

public interface IWindowService
{
    public void CreateAndShowWindow(object viewModel);
    public Window? FindWindowFromDataModel(object viewModel);
    public Window? FindWindowFromDataModel(Type t);
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
        window.Show();
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