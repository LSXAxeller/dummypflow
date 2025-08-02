using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProseFlow.UI.ViewModels.Windows;
using Window = ShadUI.Window;

namespace ProseFlow.UI.Views.Windows;

public partial class FloatingActionMenuWindow : Window
{
    public FloatingActionMenuWindow()
    {
        InitializeComponent();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Position the window near the mouse cursor
        if (Screens.Primary != null)
            Position = new PixelPoint(
                (int)(Screens.Primary.WorkingArea.Center.X - (Width / 2)),
                (int)(Screens.Primary.WorkingArea.Center.Y - (Height / 2) - 100)
            );

        // Focus the search box for immediate typing
        SearchBox.Focus();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FloatingActionMenuViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                vm.CancelSelectionCommand.Execute(null);
                Close();
                break;
            case Key.Enter:
                vm.ConfirmSelectionCommand.Execute(null);
                e.Handled = true;
                Close();
                break;
            case Key.Up:
                vm.SelectPreviousActionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                vm.SelectNextActionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void WindowBase_OnDeactivated(object? sender, EventArgs e)
    {
        Close();
    }
}