using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace VehicleDealAnalyzer.Services;

public class NhtsaService
{
    private readonly HttpClient _httpClient = new();

    public async Task<List<string>> GetRecallsAsync(int year, string make, string model)
    {
        string url = $"https://api.nhtsa.gov/recalls/recallByYearMakeModel?year={year}&make={make}&model={model}&format=json";
        var response = await _httpClient.GetFromJsonAsync<NhtsaResponse>(url);
        
        return response?.Results?.Select(r => $"{r.Component}: {r.Summary}").ToList() 
               ?? new List<string>();
    }
}

public class NhtsaResponse
{
    [JsonPropertyName("results")]
    public List<NhtsaRecall>? Results { get; set; }
}

public class NhtsaRecall
{
    [JsonPropertyName("Component")]
    public string Component { get; set; } = string.Empty;

    [JsonPropertyName("Summary")]
    public string Summary { get; set; } = string.Empty;
}
