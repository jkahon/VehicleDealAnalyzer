using VehicleDealAnalyzer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace VehicleDealAnalyzer;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;

        // Catch exceptions that happen before or during InitializeComponent
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL_APP_DOMAIN_ERROR: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL_TASK_ERROR: {e.Exception}");
        };

        InitializeComponent();

        // Force Light Theme to prevent Dark Mode black-out bugs
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var savedDealsPage = _services.GetRequiredService<SavedDealsPage>();
            return new Window(new NavigationPage(savedDealsPage));
        }
        catch (Exception ex)
        {
            return new Window(RenderErrorPage(ex));
        }
    }

    private static Page RenderErrorPage(Exception ex)
    {
        return new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 25,
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = "Startup Crash Caught:", FontAttributes = FontAttributes.Bold, TextColor = Colors.Red, FontSize = 20 },
                        new Label { Text = ex.Message, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black, FontSize = 14 },
                        new Label { Text = ex.StackTrace, TextColor = Colors.DarkSlateGray, FontSize = 11 }
                    }
                }
            }
        };
    }
}
