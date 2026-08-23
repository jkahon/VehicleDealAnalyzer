using VehicleDealAnalyzer.Views;
using VehicleDealAnalyzer.ViewModels;
using VehicleDealAnalyzer.Services;

namespace VehicleDealAnalyzer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        try
        {
            var dbService = new DatabaseService();
            var nhtsaService = new NhtsaService();
            var viewModel = new MainViewModel(nhtsaService, dbService);
            var page = new SavedDealsPage(viewModel);

            MainPage = new NavigationPage(page);
        }
        catch (Exception ex)
        {
            // If startup fails, render the exact error on-screen
            MainPage = new ContentPage
            {
                Content = new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = 20,
                        Children =
                        {
                            new Label { Text = "Startup Crash Captured:", FontAttributes = FontAttributes.Bold, TextColor = Colors.Red, FontSize = 18 },
                            new Label { Text = ex.Message, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black, Margin = new Thickness(0, 10) },
                            new Label { Text = ex.StackTrace, TextColor = Colors.DarkGray, FontSize = 12 }
                        }
                    }
                }
            };
        }
    }
}
