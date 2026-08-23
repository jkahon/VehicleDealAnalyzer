using VehicleDealAnalyzer.Views;

namespace VehicleDealAnalyzer;

public partial class App : Application
{
    public App(SavedDealsPage mainPage)
    {
        InitializeComponent();

        MainPage = new NavigationPage(mainPage);
    }
}
