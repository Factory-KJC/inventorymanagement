namespace HomeStock.Windows.WinUI;

/// <summary>WinUI 3ホストを初期化します。</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
