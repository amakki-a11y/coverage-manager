using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Thread-safe latest price per symbol. Updated by OnTick callback.
/// </summary>
public class PriceCache
{
    private readonly ConcurrentDictionary<string, PriceQuote> _prices = new();

    public void Update(string symbol, decimal bid, decimal ask)
    {
        _prices[symbol] = new PriceQuote
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Timestamp = DateTime.UtcNow
        };
    }

    public PriceQuote? Get(string symbol) =>
        _prices.TryGetValue(symbol, out var q) ? q : null;

    public IReadOnlyList<PriceQuote> GetAll() =>
        _prices.Values.ToList().AsReadOnly();
}
