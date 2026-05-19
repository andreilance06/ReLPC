using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReLPC.ViewModels;

namespace ReLPC.Views;

public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.HostWindow = this;
    }

    private void DataGrid_OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.DatasetChanged();
        }
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel { IsResettingDataset: true })
            return;

        if (sender is not DataGrid dataGrid) return;
        // Ignore navigation keys (arrows, tab, enter, escape) so they can still move around
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right or
            Key.Tab or Key.Enter or Key.Escape)
        {
            return;
        }

        // If the user presses a normal character/number key and we aren't editing yet, force it open
        if (dataGrid is { CurrentColumn: not null, IsReadOnly: false })
        {
            TryBeginEdit(dataGrid);
        }
    }

    private void DataGrid_OnCurrentCellChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel { IsResettingDataset: true })
            return;

        if (sender is DataGrid { CurrentColumn: not null, IsReadOnly: false } dataGrid)
        {
            TryBeginEdit(dataGrid);
        }
    }

    private static void TryBeginEdit(DataGrid dataGrid)
    {
        try
        {
            dataGrid.BeginEdit();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
