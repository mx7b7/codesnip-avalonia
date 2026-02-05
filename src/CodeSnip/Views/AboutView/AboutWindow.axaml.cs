namespace CodeSnip.Views.AboutView;


public partial class AboutWindow : ControlsEx.Window.Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutWindowModel();
    }
}
