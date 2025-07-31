using Avalonia.Interactivity;
using ShadUI;

namespace ProseFlow.UI.Views.Windows;

public partial class ResultWindow : Window
{
    public ResultWindow()
    {
        InitializeComponent();
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}