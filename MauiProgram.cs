using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using VehicleDealAnalyzer.Services;
using VehicleDealAnalyzer.ViewModels;
using VehicleDealAnalyzer.Views;

namespace VehicleDealAnalyzer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<NhtsaService>();
        builder.Services.AddSingleton<ShareIntentStore>();

        // Register ViewModel and Pages
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<SavedDealsPage>();

        return builder.Build();
    }
}
