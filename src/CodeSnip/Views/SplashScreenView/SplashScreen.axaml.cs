using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace CodeSnip.Views.SplashScreenView;

public partial class SplashScreen : Window
{
    private readonly Action? _mainAction;

    public SplashScreen()
    {
        InitializeComponent();
    }

    public SplashScreen(Action mainAction) : this()
    {
        _mainAction = mainAction;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    { 
        await DummyLoad(); 
    }
    private async Task DummyLoad()
    {
        // FADE IN
        SplashBorder.Opacity = 1;
        await Task.Delay(1000);

        _mainAction?.Invoke();

        // FADE OUT
        SplashBorder.Opacity = 0;
        await Task.Delay(1000);

        Close();
    }
}