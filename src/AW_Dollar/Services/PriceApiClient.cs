using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AW_Dollar.Models;

namespace AW_Dollar.Services;

/// <summary>
/// Talks to the free, keyless api.priceto.day service (https://github.com/margani/pricedb),
/// which mirrors Iran's free-market currency rates from tgju.org and refreshes every
/// ~30 minutes during market hours. Prices are returned in Rial; we convert to Toman
/// (divide by 10) since that is how people in Iran usually read these numbers.
/// </summary>
public sealed class PriceApiClient : IDisposable
{
    private const string BaseUrl = "https://api.priceto.day/v1/latest/irr/";

    private readonly HttpClient _http;

    public PriceApiClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AW_Dollar-TrayApp/1.0 (+github.com/alexwatson2/AW_Dollar)");
    }

    /// <summary>
    /// Fetches the latest price for a given upstream symbol ("usd" or "euro").
    /// Throws on network/parse failure; callers decide how to fall back.
    /// </summary>
    public async Task<PriceSnapshot> GetLatestAsync(string apiSymbol, string displayNameFa, CancellationToken ct)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse>($"{BaseUrl}{apiSymbol}", ct)
            ?? throw new InvalidOperationException($"Empty response for symbol '{apiSymbol}'.");

        if (!response.Success || response.Price is null)
        {
            throw new InvalidOperationException(response.Error ?? $"API reported failure for symbol '{apiSymbol}'.");
        }

        return new PriceSnapshot
        {
            Symbol = apiSymbol,
            DisplayNameFa = displayNameFa,
            PriceToman = response.Price.Value / 10m,
            HighToman = (response.High ?? response.Price.Value) / 10m,
            LowToman = (response.Low ?? response.Price.Value) / 10m,
            UpdatedAtUtc = response.Time ?? DateTime.UtcNow,
            IsStale = false,
        };
    }

    public void Dispose() => _http.Dispose();

    private sealed class ApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("high")]
        public decimal? High { get; set; }

        [JsonPropertyName("low")]
        public decimal? Low { get; set; }

        [JsonPropertyName("time")]
        public DateTime? Time { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
