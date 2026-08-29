namespace HomeStock.Windows;

/// <summary>Home Stock WindowsクライアントのMAUIアプリケーションを表します。</summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<MainPage>())
    {
        Title = "Home Stock",
        Width = 1280,
        Height = 820,
        MinimumWidth = 1000,
        MinimumHeight = 680,
    };
}
