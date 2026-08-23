using VehicleDealAnalyzer.Views;
using VehicleDealAnalyzer.ViewModels;
using VehicleDealAnalyzer.Services;

namespace VehicleDealAnalyzer;

public partial class App : Application
{
    public App()
    {
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

        try
        {
            var dbService = new DatabaseService();
            var nhtsaService = new NhtsaService();
            var viewModel = new MainViewModel(nhtsaService, dbService);
            
            // Set a direct ContentPage wrapped in NavigationPage
            MainPage = new NavigationPage(new SavedDealsPage(viewModel));
        }
        catch (Exception ex)
        {
            RenderErrorPage(ex);
        }
    }

    private void RenderErrorPage(Exception ex)
    {
        MainPage = new ContentPage
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
