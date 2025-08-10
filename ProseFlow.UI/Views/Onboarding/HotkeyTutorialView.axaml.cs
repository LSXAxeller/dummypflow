using Avalonia.Controls;
using Avalonia.Input;
using ProseFlow.UI.ViewModels.Onboarding;

namespace ProseFlow.UI.Views.Onboarding;

public partial class HotkeyTutorialView : UserControl
{
    public HotkeyTutorialView()
    {
        InitializeComponent();
    }

    private void OnControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HotkeyTutorialViewModel vm || e.Key != Key.J ||
            e.KeyModifiers != KeyModifiers.Control) return;
        vm.ShowMenuCommand.Execute(null);
        e.Handled = true;
    }
}