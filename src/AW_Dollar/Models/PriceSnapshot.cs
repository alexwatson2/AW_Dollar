namespace AW_Dollar.Models;

/// <summary>
/// Trend of the latest price compared to the previous successful refresh.
/// </summary>
public enum PriceTrend
{
    Unknown = 0,
    Up = 1,
    Down = 2,
    Flat = 3,
}

/// <summary>
/// A single point-in-time price for one currency, expressed in Toman
/// (Iranian free market rate, as published by tgju.org via api.priceto.day).
/// </summary>
public sealed class PriceSnapshot
{
    public required string Symbol { get; init; }
    public required string DisplayNameFa { get; init; }

    /// <summary>Price in Toman (Rial / 10).</summary>
    public decimal PriceToman { get; init; }
    public decimal HighToman { get; init; }
    public decimal LowToman { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public PriceTrend Trend { get; set; } = PriceTrend.Unknown;

    /// <summary>True when this snapshot could not be refreshed and is a stale/cached value.</summary>
    public bool IsStale { get; init; }

    public static PriceSnapshot Loading(string symbol, string displayNameFa) => new()
    {
        Symbol = symbol,
        DisplayNameFa = displayNameFa,
        PriceToman = 0,
        HighToman = 0,
        LowToman = 0,
        UpdatedAtUtc = DateTime.MinValue,
        IsStale = false,
    };
}
