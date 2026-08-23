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

        // Register ViewModel and Pages
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<SavedDealsPage>();

        return builder.Build();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);

    // Global exception handler to log hidden boot errors
    AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
    {
        System.Diagnostics.Debug.WriteLine($"BOOT_ERROR: {args.Exception.Message}");
    };

    HandleIncomingIntent(Intent);
    }
    
}
