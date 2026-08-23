using VehicleDealAnalyzer.ViewModels;

namespace VehicleDealAnalyzer.Views;

public partial class SavedDealsPage : ContentPage
{
    public SavedDealsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
