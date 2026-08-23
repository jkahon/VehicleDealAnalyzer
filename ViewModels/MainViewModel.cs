using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VehicleDealAnalyzer.Models;
using VehicleDealAnalyzer.Services;

namespace VehicleDealAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NhtsaService _nhtsaService;
    private readonly DatabaseService _databaseService;

    [ObservableProperty] private string _rawListingText = string.Empty;
    [ObservableProperty] private string _vehicleTitle = string.Empty;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private string _dealRating = "Pending";
    [ObservableProperty] private List<string> _knownIssues = new();
    [ObservableProperty] private List<SavedDeal> _savedDeals = new();

    public MainViewModel(NhtsaService nhtsaService, DatabaseService databaseService)
    {
        _nhtsaService = nhtsaService;
        _databaseService = databaseService;
    }

    [RelayCommand]
    public async Task LoadSavedDealsAsync()
    {
        SavedDeals = await _databaseService.GetSavedDealsAsync();
    }

    [RelayCommand]
    public async Task SaveCurrentDealAsync()
    {
        if (string.IsNullOrWhiteSpace(VehicleTitle)) return;

        var deal = new SavedDeal
        {
            ListingUrl = RawListingText,
            VehicleTitle = VehicleTitle,
            Price = Price,
            DealRating = DealRating,
            KnownIssuesJson = JsonSerializer.Serialize(KnownIssues),
            DateSaved = DateTime.UtcNow
        };

        await _databaseService.SaveDealAsync(deal);
        await LoadSavedDealsAsync();
    }

    [RelayCommand]
    public async Task DeleteSavedDealAsync(SavedDeal deal)
    {
        if (deal == null) return;
        await _databaseService.DeleteDealAsync(deal);
        SavedDeals.Remove(deal);
    }
}
