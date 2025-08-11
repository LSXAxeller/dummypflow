using Avalonia.Controls;
using Avalonia.Interactivity;
using ProseFlow.UI.ViewModels.Onboarding;
using Window = ShadUI.Window;

namespace ProseFlow.UI.Views.Onboarding;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
    }

    private void Button_SkipOnboard(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}