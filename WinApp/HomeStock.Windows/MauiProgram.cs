using HomeStock.Windows.Services;
using HomeStock.Windows.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HomeStock.Windows;

/// <summary>MAUIホストとWindowsクライアントの依存関係を構成します。</summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        builder.Services.AddSingleton<HomeStockApiClient>();
        builder.Services.AddSingleton<ContinuousReceivingService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<BarcodeInputBuffer>();
        builder.Services.AddSingleton<IUserInteraction, MauiUserInteraction>();
        builder.Services.AddSingleton<IInventoryExportService, WindowsInventoryExportService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
