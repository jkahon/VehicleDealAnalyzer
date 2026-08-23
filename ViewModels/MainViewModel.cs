using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VehicleDealAnalyzer.Models;
using VehicleDealAnalyzer.Services;

namespace VehicleDealAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly string[] KnownMakes =
    [
        "Acura", "Audi", "BMW", "Buick", "Cadillac", "Chevrolet", "Chevy", "Chrysler", "Dodge", "Ford",
        "GMC", "Honda", "Hyundai", "Infiniti", "Jeep", "Kia", "Lexus", "Lincoln", "Mazda", "Mercedes",
        "Mercury", "Mini", "Mitsubishi", "Nissan", "Ram", "Subaru", "Tesla", "Toyota", "Volkswagen", "Volvo"
    ];

    private readonly NhtsaService _nhtsaService;
    private readonly DatabaseService _databaseService;
    private readonly ShareIntentStore _shareIntentStore;

    [ObservableProperty] private string _rawListingText = string.Empty;
    [ObservableProperty] private string _vehicleTitle = string.Empty;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _dealScore;
    [ObservableProperty] private string _dealRating = "Pending";
    [ObservableProperty] private List<string> _knownIssues = new();
    [ObservableProperty] private List<SavedDeal> _savedDeals = new();
    [ObservableProperty] private string _mileageText = "Unknown";
    [ObservableProperty] private string _titleStatus = "Unknown";
    [ObservableProperty] private string _sellerSignals = "Unknown";
    [ObservableProperty] private string _vinText = "Unknown";
    [ObservableProperty] private string _drivetrainText = "Unknown";
    [ObservableProperty] private string _trimText = "Unknown";
    [ObservableProperty] private string _comparablePriceText = "Unknown";
    private string _currentPostText = string.Empty;
    private string _currentListingUrl = string.Empty;
    private string _analysisSummary = "Paste a Facebook share link or share a post into the app.";
    private string _statusMessage = "Waiting for a post.";
    private bool _hasAnalyzedPost;
    private bool _isAnalyzing;

    public MainViewModel(NhtsaService nhtsaService, DatabaseService databaseService, ShareIntentStore shareIntentStore)
    {
        _nhtsaService = nhtsaService;
        _databaseService = databaseService;
        _shareIntentStore = shareIntentStore;
    }

    public string CurrentPostText
    {
        get => _currentPostText;
        set => SetProperty(ref _currentPostText, value);
    }

    public string CurrentListingUrl
    {
        get => _currentListingUrl;
        set => SetProperty(ref _currentListingUrl, value);
    }

    public string AnalysisSummary
    {
        get => _analysisSummary;
        set => SetProperty(ref _analysisSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasAnalyzedPost
    {
        get => _hasAnalyzedPost;
        set => SetProperty(ref _hasAnalyzedPost, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => SetProperty(ref _isAnalyzing, value);
    }

    [RelayCommand]
    public async Task LoadSavedDealsAsync()
    {
        SavedDeals = await _databaseService.GetSavedDealsAsync();
    }

    [RelayCommand]
    public async Task SaveCurrentDealAsync()
    {
        if (!HasAnalyzedPost || string.IsNullOrWhiteSpace(CurrentPostText))
        {
            StatusMessage = "Analyze a post before keeping it.";
            return;
        }

        var deal = new SavedDeal
        {
            ListingUrl = CurrentListingUrl,
            VehicleTitle = string.IsNullOrWhiteSpace(VehicleTitle) ? "Unidentified vehicle" : VehicleTitle,
            Price = Price,
            DealRating = DealRating,
            KnownIssuesJson = JsonSerializer.Serialize(KnownIssues),
            DateSaved = DateTime.UtcNow
        };

        await _databaseService.SaveDealAsync(deal);
        await LoadSavedDealsAsync();
        StatusMessage = "Post kept. Loading next shared post.";
        ResetCurrentPost();
        await LoadNextSharedPostAsync();
    }

    [RelayCommand]
    public async Task DeleteSavedDealAsync(SavedDeal deal)
    {
        if (deal == null) return;
        await _databaseService.DeleteDealAsync(deal);
        SavedDeals.Remove(deal);
    }

    [RelayCommand]
    public async Task AnalyzeCurrentInputAsync()
    {
        await AnalyzePostAsync(RawListingText, "Manual input");
    }

    [RelayCommand]
    public async Task OpenCurrentLinkInBrowserAsync()
    {
        var link = !string.IsNullOrWhiteSpace(CurrentListingUrl)
            ? CurrentListingUrl
            : ExtractListingUrl(RawListingText);

        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            StatusMessage = "Paste or analyze a valid Marketplace link first.";
            return;
        }

        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        StatusMessage = "Opened the listing in your browser. Copy details back here for fuller analysis.";
    }

    [RelayCommand]
    public async Task LoadNextSharedPostAsync()
    {
        if (!_shareIntentStore.TryDequeue(out var sharedText) || string.IsNullOrWhiteSpace(sharedText))
        {
            if (!HasAnalyzedPost)
            {
                StatusMessage = "No shared Facebook post is waiting. Paste a link or share one into the app.";
            }

            return;
        }

        RawListingText = sharedText;
        await AnalyzePostAsync(sharedText, "Android Share sheet");
    }

    [RelayCommand]
    public void RemoveCurrentPost()
    {
        StatusMessage = "Post removed. Ready for the next one.";
        ResetCurrentPost();

        if (_shareIntentStore.TryDequeue(out var sharedText) && !string.IsNullOrWhiteSpace(sharedText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                RawListingText = sharedText;
                await AnalyzePostAsync(sharedText, "Android Share sheet");
            });
        }
    }

    private async Task AnalyzePostAsync(string input, string source)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            StatusMessage = "Paste a Facebook share link or post text first.";
            return;
        }

        IsAnalyzing = true;

        try
        {
            var normalizedInput = input.Trim();
            var issues = new List<string>();
            var lowerInput = normalizedInput.ToLowerInvariant();
            var listingUrl = ExtractListingUrl(normalizedInput);
            var extractedPrice = ExtractPrice(normalizedInput);
            var extractedVehicle = ExtractVehicle(normalizedInput);
            var mileage = ExtractMileage(normalizedInput);
            var titleAnalysis = AnalyzeTitleStatus(lowerInput);
            var sellerAnalysis = AnalyzeSellerSignals(lowerInput);
            var vin = ExtractVin(normalizedInput);
            var drivetrain = ExtractDrivetrain(lowerInput);
            var trim = ExtractTrim(normalizedInput, extractedVehicle);
            var comparable = EstimateComparablePrice(extractedVehicle, mileage.Value, drivetrain, trim);

            CurrentPostText = normalizedInput;
            CurrentListingUrl = listingUrl;
            VehicleTitle = extractedVehicle.Title;
            Price = extractedPrice;
            MileageText = mileage.DisplayText;
            TitleStatus = titleAnalysis.DisplayText;
            SellerSignals = sellerAnalysis.DisplayText;
            VinText = vin;
            DrivetrainText = drivetrain;
            TrimText = trim;
            ComparablePriceText = comparable.DisplayText;
            HasAnalyzedPost = true;

            var score = 50;

            if (string.IsNullOrWhiteSpace(listingUrl))
            {
                issues.Add("No direct Facebook share URL was detected in the shared content.");
            }

            if (normalizedInput.StartsWith("http", StringComparison.OrdinalIgnoreCase) && normalizedInput.Length < 120)
            {
                issues.Add("Only a link was shared, so analysis is limited until more post text is included.");
                score -= 10;
                StatusMessage = "Link detected. Open it in the browser, then copy the listing text back here for better analysis.";
            }

            if (extractedPrice <= 0)
            {
                issues.Add("Price could not be detected from the post.");
                score -= 10;
            }
            else if (extractedPrice < 8000)
            {
                score += 10;
            }
            else if (extractedPrice > 25000)
            {
                score -= 8;
            }

            AddIssueIfPresent(lowerInput, issues, ref score, "salvage", -25, "Salvage title mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "rebuilt", -20, "Rebuilt title mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "as is", -15, "Vehicle is being sold as-is.");
            AddIssueIfPresent(lowerInput, issues, ref score, "check engine", -20, "Check engine light mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "transmission", -15, "Transmission issue may be mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "needs tow", -30, "Vehicle may not be drivable.");
            AddIssueIfPresent(lowerInput, issues, ref score, "won't start", -30, "Vehicle may not start.");
            AddIssueIfPresent(lowerInput, issues, ref score, "mechanic special", -20, "Listed as a mechanic special.");
            AddIssueIfPresent(lowerInput, issues, ref score, "firm", -3, "Seller says the price is firm.");
            AddIssueIfPresent(lowerInput, issues, ref score, "obo", 4, "Seller is open to offers.");
            AddIssueIfPresent(lowerInput, issues, ref score, "must sell", -8, "Urgent sale language may indicate pressure or urgency.");
            AddIssueIfPresent(lowerInput, issues, ref score, "today only", -10, "Time-pressure language detected.");
            AddIssueIfPresent(lowerInput, issues, ref score, "flood", -30, "Flood damage mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "water damage", -25, "Water damage mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "rust", -12, "Rust mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "accident", -10, "Accident history may be mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "frame damage", -30, "Frame damage mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "hail damage", -8, "Hail damage mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "clean title", 10, "Clean title mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "one owner", 8, "One-owner vehicle mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "service records", 8, "Service records mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "maintenance records", 8, "Maintenance records mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "garage kept", 6, "Garage-kept vehicle mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "new tires", 4, "Recent new tires mentioned.");
            AddIssueIfPresent(lowerInput, issues, ref score, "new brakes", 4, "Recent brake work mentioned.");

            score += mileage.ScoreAdjustment;
            score += titleAnalysis.ScoreAdjustment;
            score += sellerAnalysis.ScoreAdjustment;
            score += comparable.ScoreAdjustment;

            if (extractedPrice > 0 && comparable.High > 0)
            {
                if (extractedPrice < comparable.Low)
                {
                    score += 8;
                    issues.Add("Asking price appears below the rough comparable range.");
                }
                else if (extractedPrice > comparable.High)
                {
                    score -= 8;
                    issues.Add("Asking price appears above the rough comparable range.");
                }
            }

            if (!string.Equals(vin, "Unknown", StringComparison.Ordinal))
            {
                score += 2;
            }

            if (!string.Equals(drivetrain, "Unknown", StringComparison.Ordinal) &&
                (drivetrain.Contains("AWD", StringComparison.OrdinalIgnoreCase) || drivetrain.Contains("4WD", StringComparison.OrdinalIgnoreCase) || drivetrain.Contains("4x4", StringComparison.OrdinalIgnoreCase)))
            {
                score += 3;
            }

            if (mileage.Note is not null)
            {
                issues.Add(mileage.Note);
            }

            if (titleAnalysis.Note is not null)
            {
                issues.Add(titleAnalysis.Note);
            }

            foreach (var note in sellerAnalysis.Notes)
            {
                issues.Add(note);
            }

            if (comparable.Note is not null)
            {
                issues.Add(comparable.Note);
            }

            if (!extractedVehicle.HasYearMakeModel)
            {
                issues.Add("Year, make, and model could not be fully identified, so recall analysis is limited.");
                score -= 10;
            }
            else
            {
                try
                {
                    var recalls = await _nhtsaService.GetRecallsAsync(extractedVehicle.Year, extractedVehicle.Make, extractedVehicle.Model);
                    if (recalls.Count > 0)
                    {
                        issues.Add($"Found {recalls.Count} recall record(s) for the detected vehicle.");
                        score -= Math.Min(20, recalls.Count * 3);
                    }
                }
                catch
                {
                    issues.Add("Recall lookup was unavailable, so this score uses local text analysis only.");
                }
            }

            score = Math.Clamp(score, 0, 100);
            DealScore = score;
            DealRating = score >= 70 ? "Promising" : score >= 50 ? "Needs Review" : "High Risk";
            KnownIssues = issues;
            AnalysisSummary = BuildAnalysisSummary(source, extractedVehicle.Title, extractedPrice, score, issues.Count, MileageText, TitleStatus, SellerSignals, DrivetrainText, TrimText, ComparablePriceText);
            if (!normalizedInput.StartsWith("http", StringComparison.OrdinalIgnoreCase) || normalizedInput.Length >= 120)
            {
                StatusMessage = $"Analysis complete via {source}.";
            }
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private void ResetCurrentPost()
    {
        RawListingText = string.Empty;
        CurrentPostText = string.Empty;
        CurrentListingUrl = string.Empty;
        VehicleTitle = string.Empty;
        Price = 0;
        DealScore = 0;
        DealRating = "Pending";
        MileageText = "Unknown";
        TitleStatus = "Unknown";
        SellerSignals = "Unknown";
        VinText = "Unknown";
        DrivetrainText = "Unknown";
        TrimText = "Unknown";
        ComparablePriceText = "Unknown";
        KnownIssues = [];
        AnalysisSummary = "Paste a Facebook share link or share a post into the app.";
        HasAnalyzedPost = false;
    }

    private static string BuildAnalysisSummary(string source, string title, decimal price, int score, int issueCount, string mileageText, string titleStatus, string sellerSignals, string drivetrainText, string trimText, string comparablePriceText)
    {
        var identifiedVehicle = string.IsNullOrWhiteSpace(title) ? "Unknown vehicle" : title;
        var priceText = price > 0 ? price.ToString("C0") : "price unavailable";
        return $"{identifiedVehicle} from {source}. Estimated value signal: {priceText}. Mileage: {mileageText}. Title: {titleStatus}. Seller: {sellerSignals}. Drivetrain: {drivetrainText}. Trim: {trimText}. Comparable range: {comparablePriceText}. Deal score: {score}/100 with {issueCount} flagged item(s).";
    }

    private static string ExtractListingUrl(string input)
    {
        var match = Regex.Match(input, @"https?://\S+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.TrimEnd('.', ',', ';', ')') : string.Empty;
    }

    private static decimal ExtractPrice(string input)
    {
        var match = Regex.Match(input, @"\$?\s*(\d{1,3}(?:,\d{3})+|\d{4,6})(?:\.\d{2})?");
        if (!match.Success)
        {
            return 0;
        }

        var digits = match.Groups[1].Value.Replace(",", string.Empty);
        return decimal.TryParse(digits, out var price) ? price : 0;
    }

    private static MileageAnalysis ExtractMileage(string input)
    {
        var match = Regex.Match(input, @"\b(\d{2,3}(?:,\d{3})+|\d{5,6})\s*(?:miles?|mi)\b", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(input, @"\b(\d{2,3}(?:\.\d)?)\s*k\b", RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var mileageInThousands))
            {
                var shorthandMileage = (int)Math.Round(mileageInThousands * 1000, MidpointRounding.AwayFromZero);
                return BuildMileageAnalysis(shorthandMileage);
            }
        }

        if (!match.Success)
        {
            return new MileageAnalysis(0, "Unknown", 0, null);
        }

        var digits = match.Groups[1].Value.Replace(",", string.Empty);
        if (!int.TryParse(digits, out var mileage))
        {
            return new MileageAnalysis(0, "Unknown", 0, null);
        }

        return BuildMileageAnalysis(mileage);
    }

    private static MileageAnalysis BuildMileageAnalysis(int mileage)
    {
        if (mileage >= 220000)
        {
            return new MileageAnalysis(mileage, $"{mileage:N0} miles", -18, "Very high mileage detected.");
        }

        if (mileage >= 150000)
        {
            return new MileageAnalysis(mileage, $"{mileage:N0} miles", -10, "High mileage detected.");
        }

        if (mileage <= 80000)
        {
            return new MileageAnalysis(mileage, $"{mileage:N0} miles", 8, "Relatively low mileage detected.");
        }

        return new MileageAnalysis(mileage, $"{mileage:N0} miles", 0, null);
    }

    private static string ExtractVin(string input)
    {
        var match = Regex.Match(input, @"\b([A-HJ-NPR-Z0-9]{17})\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : "Unknown";
    }

    private static string ExtractDrivetrain(string input)
    {
        if (input.Contains("4x4", StringComparison.OrdinalIgnoreCase)) return "4x4";
        if (input.Contains("4wd", StringComparison.OrdinalIgnoreCase)) return "4WD";
        if (input.Contains("awd", StringComparison.OrdinalIgnoreCase) || input.Contains("all wheel drive", StringComparison.OrdinalIgnoreCase)) return "AWD";
        if (input.Contains("fwd", StringComparison.OrdinalIgnoreCase) || input.Contains("front wheel drive", StringComparison.OrdinalIgnoreCase)) return "FWD";
        if (input.Contains("rwd", StringComparison.OrdinalIgnoreCase) || input.Contains("rear wheel drive", StringComparison.OrdinalIgnoreCase)) return "RWD";
        return "Unknown";
    }

    private static string ExtractTrim(string input, ExtractedVehicle vehicle)
    {
        var trims = new[]
        {
            "LX", "EX", "EX-L", "Sport", "Touring", "Limited", "Platinum", "XLT", "Lariat", "LT", "LTZ",
            "Denali", "SE", "SEL", "Titanium", "Premium", "SR", "SR5", "TRD Off Road", "TRD Sport", "TRD Pro"
        };

        foreach (var trim in trims.OrderByDescending(candidate => candidate.Length))
        {
            if (Regex.IsMatch(input, $@"\b{Regex.Escape(trim)}\b", RegexOptions.IgnoreCase))
            {
                return trim;
            }
        }

        if (!string.IsNullOrWhiteSpace(vehicle.Model))
        {
            var match = Regex.Match(input, $@"\b{Regex.Escape(vehicle.Model)}\s+([A-Za-z0-9\-]+(?:\s+[A-Za-z0-9\-]+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var candidate = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 20)
                {
                    return candidate;
                }
            }
        }

        return "Unknown";
    }

    private static ComparablePriceAnalysis EstimateComparablePrice(ExtractedVehicle vehicle, int mileage, string drivetrain, string trim)
    {
        if (!vehicle.HasYearMakeModel)
        {
            return new ComparablePriceAnalysis("Unknown", 0, null);
        }

        var basePrice = vehicle.Year >= 2020 ? 24000 :
                        vehicle.Year >= 2015 ? 17000 :
                        vehicle.Year >= 2010 ? 11000 : 7000;

        if (vehicle.Make is "Toyota" or "Honda" or "Lexus")
        {
            basePrice += 2500;
        }

        if (vehicle.Make is "BMW" or "Audi" or "Mercedes")
        {
            basePrice += 3500;
        }

        if (drivetrain is "AWD" or "4WD" or "4x4")
        {
            basePrice += 1500;
        }

        if (trim is "Limited" or "Touring" or "Platinum" or "Denali" or "LTZ" or "TRD Pro")
        {
            basePrice += 2000;
        }

        if (mileage > 0)
        {
            if (mileage <= 80000)
            {
                basePrice += 1500;
            }
            else if (mileage >= 180000)
            {
                basePrice -= 2500;
            }
        }

        var low = Math.Max(2500, basePrice - 2500);
        var high = basePrice + 2500;
        return new ComparablePriceAnalysis($"{low:C0} - {high:C0}", 0, null, low, high);
    }

    private static SignalAnalysis AnalyzeTitleStatus(string input)
    {
        if (input.Contains("salvage", StringComparison.OrdinalIgnoreCase))
        {
            return new SignalAnalysis("Salvage", -15, "Title status appears to be salvage.");
        }

        if (input.Contains("rebuilt", StringComparison.OrdinalIgnoreCase) || input.Contains("reconstructed", StringComparison.OrdinalIgnoreCase))
        {
            return new SignalAnalysis("Rebuilt", -12, "Title status appears to be rebuilt/reconstructed.");
        }

        if (input.Contains("clean title", StringComparison.OrdinalIgnoreCase))
        {
            return new SignalAnalysis("Clean", 8, null);
        }

        if (input.Contains("lien", StringComparison.OrdinalIgnoreCase))
        {
            return new SignalAnalysis("Lien noted", -8, "A lien may still be attached to the vehicle.");
        }

        return new SignalAnalysis("Unknown", 0, null);
    }

    private static SellerSignalAnalysis AnalyzeSellerSignals(string input)
    {
        var labels = new List<string>();
        var notes = new List<string>();
        var scoreAdjustment = 0;

        if (input.Contains("one owner", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("one owner");
        }

        if (input.Contains("dealer", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("dealer");
            scoreAdjustment -= 3;
        }

        if (input.Contains("private seller", StringComparison.OrdinalIgnoreCase) || input.Contains("owner", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("private seller");
            scoreAdjustment += 2;
        }

        if (input.Contains("service records", StringComparison.OrdinalIgnoreCase) || input.Contains("maintenance records", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("records");
        }

        if (input.Contains("must sell", StringComparison.OrdinalIgnoreCase) || input.Contains("today only", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("urgent sale");
            notes.Add("Seller urgency detected; verify the deal carefully.");
        }

        if (input.Contains("obo", StringComparison.OrdinalIgnoreCase) || input.Contains("best offer", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("negotiable");
            scoreAdjustment += 3;
        }

        if (labels.Count == 0)
        {
            labels.Add("limited seller context");
        }

        return new SellerSignalAnalysis(string.Join(", ", labels.Distinct()), scoreAdjustment, notes);
    }

    private static ExtractedVehicle ExtractVehicle(string input)
    {
        var yearMatch = Regex.Match(input, @"\b(19\d{2}|20\d{2})\b");
        var year = yearMatch.Success && int.TryParse(yearMatch.Value, out var parsedYear) ? parsedYear : 0;

        var make = KnownMakes.FirstOrDefault(candidate =>
            Regex.IsMatch(input, $@"\b{Regex.Escape(candidate)}\b", RegexOptions.IgnoreCase));

        if (string.IsNullOrWhiteSpace(make))
        {
            return new ExtractedVehicle(year, string.Empty, string.Empty);
        }

        var vehicleMatch = Regex.Match(
            input,
            $@"\b(?:{year})?\s*{Regex.Escape(make)}\s+([A-Za-z0-9\-]+(?:\s+[A-Za-z0-9\-]+)?)",
            RegexOptions.IgnoreCase);

        var model = vehicleMatch.Success ? vehicleMatch.Groups[1].Value.Trim() : string.Empty;
        return new ExtractedVehicle(year, NormalizeMake(make), model);
    }

    private static string NormalizeMake(string make)
    {
        return string.Equals(make, "Chevy", StringComparison.OrdinalIgnoreCase) ? "Chevrolet" : make;
    }

    private static void AddIssueIfPresent(string input, List<string> issues, ref int score, string term, int scoreDelta, string message)
    {
        if (!input.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        issues.Add(message);
        score += scoreDelta;
    }

    private readonly record struct MileageAnalysis(int Value, string DisplayText, int ScoreAdjustment, string? Note);

    private readonly record struct SignalAnalysis(string DisplayText, int ScoreAdjustment, string? Note);

    private readonly record struct SellerSignalAnalysis(string DisplayText, int ScoreAdjustment, IReadOnlyList<string> Notes);

    private readonly record struct ComparablePriceAnalysis(string DisplayText, int ScoreAdjustment, string? Note, int Low = 0, int High = 0);

    private readonly record struct ExtractedVehicle(int Year, string Make, string Model)
    {
        public string Title => string.Join(" ", new[] { Year > 0 ? Year.ToString() : string.Empty, Make, Model }.Where(part => !string.IsNullOrWhiteSpace(part)));

        public bool HasYearMakeModel => Year > 0 && !string.IsNullOrWhiteSpace(Make) && !string.IsNullOrWhiteSpace(Model);
    }
}
