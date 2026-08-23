using SQLite;

namespace VehicleDealAnalyzer.Models;

[Table("SavedDeals")]
public class SavedDeal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string ListingUrl { get; set; } = string.Empty;
    public string VehicleTitle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string DealRating { get; set; } = string.Empty;
    public string KnownIssuesJson { get; set; } = string.Empty;
    public DateTime DateSaved { get; set; } = DateTime.UtcNow;
}
