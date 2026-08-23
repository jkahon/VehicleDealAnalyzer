using SQLite;
using VehicleDealAnalyzer.Models;

namespace VehicleDealAnalyzer.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;

    private async Task InitAsync()
    {
        if (_database is not null) return;

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "vehicle_deals.db3");
        _database = new SQLiteAsyncConnection(dbPath);
        await _database.CreateTableAsync<SavedDeal>();
    }

    public async Task<List<SavedDeal>> GetSavedDealsAsync()
    {
        await InitAsync();
        return await _database!.Table<SavedDeal>().OrderByDescending(d => d.DateSaved).ToListAsync();
    }

    public async Task<int> SaveDealAsync(SavedDeal deal)
    {
        await InitAsync();
        if (deal.Id != 0)
        {
            return await _database!.UpdateAsync(deal);
        }
        return await _database!.InsertAsync(deal);
    }

    public async Task<int> DeleteDealAsync(SavedDeal deal)
    {
        await InitAsync();
        return await _database!.DeleteAsync(deal);
    }
}
