using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReLPC.Views;

public partial class MessageWindowView : Window
{
    public MessageWindowView()
    {
        InitializeComponent();
    }

    public MessageWindowView(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    public static async Task ShowAsync(Window owner, string title, string message)
    {
        var window = new MessageWindowView(title, message);
        await window.ShowDialog(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
