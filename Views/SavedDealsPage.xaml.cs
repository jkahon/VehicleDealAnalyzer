using VehicleDealAnalyzer.Services;
using VehicleDealAnalyzer.ViewModels;

namespace VehicleDealAnalyzer.Views;

public partial class SavedDealsPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly ShareIntentStore _shareIntentStore;
    private bool _initialized;

    public SavedDealsPage(MainViewModel viewModel, ShareIntentStore shareIntentStore)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _shareIntentStore = shareIntentStore;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _shareIntentStore.PendingItemReceived += OnPendingItemReceived;

        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            await _viewModel.LoadSavedDealsAsync();
            await _viewModel.LoadNextSharedPostAsync();
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Startup load failed: {ex.Message}";
        }
    }

    protected override void OnDisappearing()
    {
        _shareIntentStore.PendingItemReceived -= OnPendingItemReceived;
        base.OnDisappearing();
    }

    private void OnPendingItemReceived(object? sender, EventArgs e)
    {
        if (_viewModel.HasAnalyzedPost || _viewModel.IsAnalyzing)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await _viewModel.LoadNextSharedPostAsync());
    }
}
