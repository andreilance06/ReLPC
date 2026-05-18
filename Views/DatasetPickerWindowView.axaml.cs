using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReLPC.Models;

namespace ReLPC.Views;

public partial class DatasetPickerWindowView : Window
{
    public DatasetPickerWindowView()
    {
        InitializeComponent();
    }

    public DatasetPickerWindowView(IEnumerable<DatasetRecord> datasets) : this()
    {
        DatasetsListBox.ItemsSource = datasets;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnLoadClick(object? sender, RoutedEventArgs e) =>
        Close(DatasetsListBox.SelectedItem as DatasetRecord);

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
